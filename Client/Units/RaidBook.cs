#nullable enable
using System.Collections.Generic;

namespace Perun.Client.Units;

// Busy and warming state for async loads. A lease belongs to the raid that started it, so a load that
// finishes after the raid has changed can't clear the next raid's markers or switch force-loading off under it.
internal sealed class RaidBook<TKey> where TKey : notnull
{
    private readonly HashSet<TKey> _warming = new();
    private int _busy;

    public int Generation { get; private set; }

    public bool Busy => _busy > 0;

    public int BusyCount => _busy;

    public int WarmingCount => _warming.Count;

    public bool IsWarming(TKey key) => _warming.Contains(key);

    public Lease Begin()
    {
        _busy++;
        return new Lease(Generation, default, false);
    }

    public Lease BeginWarm(TKey key)
    {
        _busy++;
        _warming.Add(key);
        return new Lease(Generation, key, true);
    }

    public bool Owns(Lease lease) => !lease.Ended && lease.Generation == Generation;

    public bool End(Lease lease)
    {
        if (!Owns(lease))
        {
            lease.Ended = true;
            return false;
        }
        lease.Ended = true;
        _busy--;
        if (lease.Warms)
        {
            _warming.Remove(lease.Key!);
        }
        return true;
    }

    public void Reset()
    {
        Generation++;
        _warming.Clear();
        _busy = 0;
    }

    public sealed class Lease
    {
        internal Lease(int generation, TKey? key, bool warms)
        {
            Generation = generation;
            Key = key;
            Warms = warms;
        }

        public int Generation { get; }
        public TKey? Key { get; }
        public bool Warms { get; }
        public bool Ended { get; internal set; }
    }
}
