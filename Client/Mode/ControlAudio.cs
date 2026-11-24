using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Audio.SpatialSystem;
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

    private static readonly FieldInfo StepSourceField = Reflect.Field(Feature.ControlAudio, typeof(Player), "NestedStepSoundSource");

    // The step sound source is set up once at spawn for the body's point of view: a third-person body gets the observed
    // mixer and occlusion from the listener, which muffles its steps once the camera sits in its own head.
    public static void MatchSteps(Player player)
    {
        if (StepSourceField == null)
        {
            return;
        }
        (StepSourceField.GetValue(player) as BetterSource)?.Release();
        player.InitAudioSources();
    }

    private static readonly FieldInfo RoomStorageField = Reflect.Field(Feature.ControlAudio, typeof(SpatialAudioSystem), "_audioRoomStorage");
    private static readonly FieldInfo ListenerIndoorField = Reflect.Field(Feature.ControlAudio, typeof(AudioRoomStorage), "list_3");
    private static readonly FieldInfo ListenerOutdoorField = Reflect.Field(Feature.ControlAudio, typeof(AudioRoomStorage), "list_4");
    private static readonly FieldInfo PlayerIndoorField = Reflect.Field(Feature.ControlAudio, typeof(AudioRoomStorage), "_playerCurrentIndoorRooms");
    private static readonly FieldInfo PlayerOutdoorField = Reflect.Field(Feature.ControlAudio, typeof(AudioRoomStorage), "_playerCurrentOutdoorRooms");

    // The listener's rooms decide how muffled every other sound is (indoor, outdoor, through walls). The game moves them
    // only when "your player" passes a room trigger, so they would stay where the old body stood; they are handed over
    // with the switch, and the old body's rooms become an ordinary player's.
    public static void SwitchYourPlayer(Player from, Player to)
    {
        SpatialAudioSystem audio = Singleton<SpatialAudioSystem>.Instantiated && !Reflect.Lacks(Feature.ControlAudio) ? Singleton<SpatialAudioSystem>.Instance : null;
        List<SpatialAudioRoom> fromRooms = null;
        List<SpatialAudioRoom> toRooms = null;
        try
        {
            object storage = audio != null ? RoomStorageField.GetValue(audio) : null;
            if (storage != null)
            {
                fromRooms = Rooms(ListenerIndoorField.GetValue(storage) as IList, ListenerOutdoorField.GetValue(storage) as IList);
                toRooms = to != null ? Rooms(Entry(PlayerIndoorField.GetValue(storage), to.Id), Entry(PlayerOutdoorField.GetValue(storage), to.Id)) : null;
                Leave(audio, fromRooms, from);
                Leave(audio, toRooms, to);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Control: couldn't read the audio rooms: {ex.Message}");
            fromRooms = toRooms = null;
        }

        if (from != null)
        {
            from.IsYourPlayer = false;
        }
        if (to != null)
        {
            to.IsYourPlayer = true;
        }

        try
        {
            Enter(audio, toRooms, to);
            Enter(audio, fromRooms, from);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Control: couldn't move the audio rooms: {ex.Message}");
        }
    }

    private static IList Entry(object rooms, int playerId) => rooms is IDictionary map && map.Contains(playerId) ? map[playerId] as IList : null;

    private static List<SpatialAudioRoom> Rooms(IList indoor, IList outdoor)
    {
        List<SpatialAudioRoom> rooms = new();
        foreach (IList list in new[] { indoor, outdoor })
        {
            if (list == null)
            {
                continue;
            }
            foreach (object room in list)
            {
                if (room is SpatialAudioRoom audioRoom)
                {
                    rooms.Add(audioRoom);
                }
            }
        }
        return rooms;
    }

    private static void Leave(SpatialAudioSystem audio, List<SpatialAudioRoom> rooms, Player player)
    {
        if (rooms == null || player == null)
        {
            return;
        }
        foreach (SpatialAudioRoom room in rooms)
        {
            audio.RemovePlayerCurrentRoom(room, player);
        }
    }

    // Rooms are added at the front of their list, so they go back in reverse to keep the current one first.
    private static void Enter(SpatialAudioSystem audio, List<SpatialAudioRoom> rooms, Player player)
    {
        if (rooms == null || player == null)
        {
            return;
        }
        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            audio.AddPlayerCurrentRoom(rooms[i], player);
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
