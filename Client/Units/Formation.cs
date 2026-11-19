#nullable enable
using UnityEngine;

namespace Perun.Client.Units;

internal static class Formation
{
    private const float GoldenAngle = 137.5f;

    public static Vector3 Offset(int index, float spacing)
    {
        if (index <= 0)
        {
            return Vector3.zero;
        }
        float angle = index * GoldenAngle * Mathf.Deg2Rad;
        float radius = spacing * Mathf.Sqrt(index);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }
}
