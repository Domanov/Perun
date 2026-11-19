using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.EnvironmentEffect;
using EFT.HealthSystem;
using UnityEngine;

namespace Perun.Client.Mode;

internal static class ControlAudio
{
    public static void FollowCamera()
    {
        if (!CameraManager.Exist || CameraManager.Instance.Camera == null)
        {
            Plugin.Log.LogWarning("Control: no camera for the audio listener to follow");
            return;
        }
        Transform camera = CameraManager.Instance.Camera.transform;
        if (Singleton<AudioListenerConsistencyManager>.Instantiated)
        {
            Singleton<AudioListenerConsistencyManager>.Instance.Follow(camera);
        }
        if (Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.AudioSourceCulling != null)
        {
            Singleton<GameWorld>.Instance.AudioSourceCulling.SetListenerTransform(camera);
        }
    }

    // The game switches the global indoor/outdoor mix only when "your player" crosses an indoor trigger, which never
    // happens when control jumps to another body, so it has to be set for the new body directly.
    public static void MatchEnvironment(Player player)
    {
        if (player != null && player.IsYourPlayer && EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.UpdateEnvironmentForPlayer(player);
        }
    }

    // Tinnitus is one global BetterAudio effect, not tied to a player: it keeps ringing until its end time.
    private static readonly FieldInfo TinnitusEndField = Reflect.Field(Feature.ControlScreenEffects, typeof(BetterAudio), "float_2");

    public static void StopTinnitus()
    {
        if (TinnitusEndField != null && Singleton<BetterAudio>.Instantiated && (float)TinnitusEndField.GetValue(Singleton<BetterAudio>.Instance) > Time.time)
        {
            TinnitusEndField.SetValue(Singleton<BetterAudio>.Instance, Time.time);
        }
    }

    public static void ResumeTinnitus(Player player)
    {
        IContusion contusion = player?.HealthController?.FindActiveEffect<IContusion>();
        if (contusion != null && contusion.TimeLeft > 0f)
        {
            player.TryStartContusion(contusion.TimeLeft);
        }
    }

    // Headset and helmet hearing is one global template, and the listener drives the game's hearing checks; both are
    // only set for "your player" when it spawns or changes gear, so a takeover has to switch them itself.
    public static void MatchHearing(Player player)
    {
        if (player != null && player.IsYourPlayer && Singleton<BetterAudio>.Instantiated)
        {
            player.UpdatePhonesReally();
            BetterAudio audio = Singleton<BetterAudio>.Instance;
            if (!ReferenceEquals(audio.ListenerPlayer, player))
            {
                audio.UnsubscribeProtagonist();
                audio.SetProtagonist(player);
            }
        }
    }
}
