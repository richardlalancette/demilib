﻿// Author: Daniele Giardini - http://www.demigiant.com
// Created: 2015/11/21 18:29
// License Copyright (c) Daniele Giardini

using System.Collections.Generic;
using DG.DeAudio.Core;
using DG.DeAudio.Events;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace DG.DeAudio
{
    /// <summary>
    /// Global AudioManager. Only use its static methods.
    /// <para>Must be instantiated only once per project (either manually or via code), and its GameObject is set automatically to DontDestroyOnLoad.</para>
    /// </summary>
    public class DeAudioManager : MonoBehaviour
    {
        public bool logInfo = false;
        public DeAudioGroup[] fooAudioGroups;
        public float fooGlobalVolume = 1;
        /// <summary>Used internally inside Unity Editor, as a trick to update DeAudioManager's inspector at every frame</summary>
        public int inspectorUpdater;

        internal static DeAudioManager I;
        public const string Version = "0.5.320";
        internal const string LogPrefix = "DAM :: ";
        internal static DeAudioGroup globalGroup; // Group created when playing a clip without any group indication. Also stored as the final _audioGroups value
        static Tween _fadeTween;

        public static float globalVolume {
            get { return I.fooGlobalVolume; }
            set { SetVolume(value); }
        }
        static DeAudioGroup[] _audioGroups;

        #region Unity Methods

        void Awake()
        {
            if (I != null) {
                Debug.LogWarning(LogPrefix + "Multiple DeAudioManager instances were found. The newest one will be destroyed");
                Destroy(this.gameObject);
            }

            I = this;
            DontDestroyOnLoad(this.gameObject);

            // Initialize audioGroups
            globalGroup = new DeAudioGroup(DeAudioGroupId.INTERNAL_Global);
            globalGroup.Init(this.transform, "Global [auto]");
            int len = I.fooAudioGroups == null ? 0 : I.fooAudioGroups.Length;
            _audioGroups = new DeAudioGroup[len + 1];
            for (int i = 0; i < len; ++i) {
                DeAudioGroup g = I.fooAudioGroups[i];
                g.Init(this.transform);
                _audioGroups[i] = g;
            }
            _audioGroups[len] = globalGroup;
        }

        void Update()
        {
            if (Application.isEditor) inspectorUpdater++;
        }

        void OnDestroy()
        {
            if (I != this) return;
            _fadeTween.Kill();
            int len = _audioGroups.Length;
            for (int i = 0; i < len; ++i) _audioGroups[i].Dispose();
            I = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a DeAudioManager instance (if it's not present already) and sets it as DontDestroyOnLoad.
        /// <para>Use this method if you want to use DeAudioManager without setting up any DeAudioGroup.
        /// Though the recommended way is to create a prefab with the required settings and instantiate it once at startup.</para>
        /// </summary>
        public static void Init()
        {
            if (I != null) return;

            GameObject go = new GameObject("DeAudioManager");
            go.AddComponent<DeAudioManager>();
        }

        /// <summary>
        /// Plays the given <see cref="DeAudioClipData"/> on the stored group, with the stored volume, pitch and loop settings.
        /// A <see cref="DeAudioGroup"/> with the given ID must exist in order for the sound to actually play.
        /// <para>Returns the <see cref="DeAudioSource"/> instance used to play, or NULL if the clip couldn't be played</para>
        /// </summary>
        public static DeAudioSource Play(DeAudioClipData clipData)
        { return Play(clipData.groupId, clipData.clip, clipData.volume, clipData.pitch, clipData.loop); }
        /// <summary>
        /// Plays the given sound with the given options and using the given group id.
        /// A <see cref="DeAudioGroup"/> with the given ID must exist in order for the sound to actually play.
        /// <para>Returns the <see cref="DeAudioSource"/> instance used to play, or NULL if the clip couldn't be played</para>
        /// </summary>
        public static DeAudioSource Play(DeAudioGroupId groupId, AudioClip clip, float volume = 1, float pitch = 1, bool loop = false)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group == null) {
                Debug.LogWarning(LogPrefix + "Clip can't be played because no group with the given groupId (" + groupId + ") was created");
                return null;
            }
            return group.Play(clip, volume, pitch, loop);
        }
        /// <summary>
        /// Plays the given sound external to any group, using the given options.
        /// <para>Returns the <see cref="DeAudioSource"/> instance used to play, or NULL if the clip couldn't be played</para>
        /// </summary>
        public static DeAudioSource Play(AudioClip clip, float volume = 1, float pitch = 1, bool loop = false)
        {
            return globalGroup.Play(clip, volume, pitch, loop);
        }

        /// <summary>Stops all sounds</summary>
        public static void Stop()
        { IterateOnAllGroups(OperationType.Stop); }
        /// <summary>Stops all sounds for the given group</summary>
        public static void Stop(DeAudioGroupId groupId)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group != null) group.Stop();
        }
        /// <summary>Stops all sounds for the given clip</summary>
        public static void Stop(AudioClip clip)
        { IterateOnAllGroups(OperationType.StopByClip, clip); }

        /// <summary>Sets the global volume (same as setting <see cref="globalVolume"/> directly</summary>
        public static void SetVolume(float volume)
        {
            I.fooGlobalVolume = volume;
            DeAudioNotificator.DispatchDeAudioEvent(DeAudioEventType.GlobalVolumeChange);
        }
        /// <summary>Sets the volume for the given group</summary>
        public static void SetVolume(DeAudioGroupId groupId, float volume)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group != null) group.SetVolume(volume);
        }
        /// <summary>Sets the volume for the given clip</summary>
        public static void SetVolume(AudioClip clip, float volume)
        { IterateOnAllGroups(OperationType.SetVolumeByClip, clip, volume); }

        /// <summary>Unlocks all <see cref="DeAudioSource"/> instances</summary>
        public static void Unlock()
        { IterateOnAllGroups(OperationType.Unlock); }
        /// <summary>Unlocks all <see cref="DeAudioSource"/> instances for the given group</summary>
        public static void Unlock(DeAudioGroupId groupId)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group != null) group.Unlock();
        }
        /// <summary>Unlocks all <see cref="DeAudioSource"/> instances for the given clip</summary>
        public static void Unlock(AudioClip clip)
        { IterateOnAllGroups(OperationType.UnlockByClip, clip); }

        /// <summary>
        /// Returns the <see cref="DeAudioGroup"/> with the given ID, or NULL if there is none
        /// </summary>
        public static DeAudioGroup GetAudioGroup(DeAudioGroupId groupId)
        {
            int len = _audioGroups.Length;
            for (int i = 0; i < len; ++i) {
                DeAudioGroup g = _audioGroups[i];
                if (g.id == groupId) return g;
            }
            return null;
        }

        /// <summary>
        /// Returns the AudioMixerGroup for <see cref="DeAudioGroup"/> with the given ID, or null if there is none
        /// </summary>
        public static AudioMixerGroup GetMixerGroup(DeAudioGroupId groupId)
        {
            DeAudioGroup g = GetAudioGroup(groupId);
            if (g == null) return null;
            return g.mixerGroup;
        }

        #region Tweens

        /// <summary>Fades out the global volume</summary>
        public static void FadeOut(float duration = 1.5f, bool ignoreTimeScale = true, bool stopOnComplete = true, TweenCallback onComplete = null)
        { FadeTo(0, duration, ignoreTimeScale, stopOnComplete, onComplete); }
        /// <summary>Fades in the global volume</summary>
        public static void FadeIn(float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeTo(1, duration, ignoreTimeScale, false, onComplete); }
        /// <summary>Fades the global volume to the given value</summary>
        public static void FadeTo(float to, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeTo(to, duration, ignoreTimeScale, false, onComplete); }
        static void FadeTo(float to, float duration, bool ignoreTimeScale, bool stopOnComplete, TweenCallback onComplete)
        {
            _fadeTween.Kill();
            _fadeTween = DOTween.To(() => globalVolume, x => globalVolume = x, to, duration)
                .SetTarget(I).SetUpdate(ignoreTimeScale).SetEase(Ease.Linear);
            if (stopOnComplete) _fadeTween.OnStepComplete(Stop);
            if (onComplete != null) _fadeTween.OnComplete(onComplete);
        }
        /// <summary>Fades out the volume of every source without touching global and group volumes</summary>
        public static void FadeSourcesOut(float duration = 1.5f, bool ignoreTimeScale = true, bool stopOnComplete = true, TweenCallback onComplete = null)
        { FadeSourcesTo(0, duration, ignoreTimeScale, stopOnComplete, onComplete); }
        /// <summary>Fades in the volume of every source without touching global and group volumes</summary>
        public static void FadeSourcesIn(float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeSourcesTo(1, duration, ignoreTimeScale, false, onComplete); }
        /// <summary>Fades the volume of every source to the given value without touching global and group volumes</summary>
        public static void FadeSourcesTo(float to, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeSourcesTo(to, duration, ignoreTimeScale, false, onComplete); }
        static void FadeSourcesTo(float to, float duration, bool ignoreTimeScale, bool stopOnComplete, TweenCallback onComplete)
        {
            int len = _audioGroups.Length;
            for (int i = 0; i < len; ++i) _audioGroups[i].FadeSourcesTo(to, duration, ignoreTimeScale, stopOnComplete, onComplete);
        }

        /// <summary>Fades out the given group's volume</summary>
        public static void FadeOut(DeAudioGroupId groupId, float duration = 1.5f, bool ignoreTimeScale = true, bool stopOnComplete = true, TweenCallback onComplete = null)
        { FadeTo(groupId, 0, duration, ignoreTimeScale, stopOnComplete, onComplete); }
        /// <summary>Fades in the given group's volume</summary>
        public static void FadeIn(DeAudioGroupId groupId, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeTo(groupId, 1, duration, ignoreTimeScale, false, onComplete); }
        /// <summary>Fades the given group's volume to the given value</summary>
        public static void FadeTo(DeAudioGroupId groupId, float to, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeTo(groupId, to, duration, ignoreTimeScale, false, onComplete); }
        static void FadeTo(DeAudioGroupId groupId, float to, float duration, bool ignoreTimeScale, bool stopOnComplete, TweenCallback onComplete)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group != null) group.FadeTo(to, duration, ignoreTimeScale, stopOnComplete, onComplete);
        }
        /// <summary>Fades out the volume of each source in the given group (not the given group's volume)</summary>
        public static void FadeSourcesOut(DeAudioGroupId groupId, float duration = 1.5f, bool ignoreTimeScale = true, bool stopOnComplete = true, TweenCallback onComplete = null)
        { FadeSourcesTo(groupId, 0, duration, ignoreTimeScale, stopOnComplete, onComplete); }
        /// <summary>Fades in the volume of each source in the given group (not the given group's volume)</summary>
        public static void FadeSourcesIn(DeAudioGroupId groupId, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeSourcesTo(groupId, 1, duration, ignoreTimeScale, false, onComplete); }
        /// <summary>Fades the volume of each source in the given group (not the given group's volume) to the given value</summary>
        public static void FadeSourcesTo(DeAudioGroupId groupId, float to, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeSourcesTo(groupId, to, duration, ignoreTimeScale, false, onComplete); }
        static void FadeSourcesTo(DeAudioGroupId groupId, float to, float duration, bool ignoreTimeScale, bool stopOnComplete, TweenCallback onComplete)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group != null) group.FadeSourcesTo(to, duration, ignoreTimeScale, stopOnComplete, onComplete);
        }

        /// <summary>Fades out the given clip's volume</summary>
        public static void FadeOut(AudioClip clip, float duration = 1.5f, bool ignoreTimeScale = true, bool stopOnComplete = true, TweenCallback onComplete = null)
        { FadeTo(clip, 0, duration, ignoreTimeScale, stopOnComplete, onComplete); }
        /// <summary>Starts playing the given clip with a fade-in volume effect</summary>
        public static void FadeIn(DeAudioGroupId groupId, AudioClip clip, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { Play(groupId, clip, 0).FadeTo(1, duration, ignoreTimeScale, onComplete); }
        /// <summary>Starts playing the given clip external to any group, with a fade-in volume effect</summary>
        public static void FadeIn(AudioClip clip, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { Play(clip, 0).FadeTo(1, duration, ignoreTimeScale, onComplete); }
        /// <summary>Starts playing the given <see cref="DeAudioClipData"/> with a fade-in volume effect</summary>
        public static void FadeIn(DeAudioClipData clipData, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { Play(clipData.groupId, clipData.clip, 0, clipData.pitch, clipData.loop).FadeTo(clipData.volume, duration, ignoreTimeScale, false, onComplete); }
        /// <summary>Fades the given clip's volume to the given value</summary>
        public static void FadeTo(AudioClip clip, float to, float duration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { FadeTo(clip, to, duration, ignoreTimeScale, false, onComplete); }
        static void FadeTo(AudioClip clip, float to, float duration, bool ignoreTimeScale, bool stopOnComplete, TweenCallback onComplete)
        {
            int len = _audioGroups.Length;
            for (int i = 0; i < len; ++i) {
                DeAudioGroup group = _audioGroups[i];
                int slen = group.sources.Count;
                for (int c = 0; c < slen; c++) {
                    DeAudioSource s = group.sources[c];
                    if (s.clip == clip) s.FadeTo(to, duration, ignoreTimeScale, stopOnComplete, onComplete);
                }
            }
        }

        /// <summary>
        /// Fades out then stops all sources in the given <see cref="DeAudioClipData"/> group,
        /// while starting the given <see cref="DeAudioClipData"/> with a fade-in effect.
        /// <para>Returns the <see cref="DeAudioSource"/> instance used to play, or NULL if the clip couldn't be played</para>
        /// </summary>
        public static DeAudioSource Crossfade(DeAudioClipData clipData, float fadeDuration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        { return Crossfade(clipData.groupId, clipData.clip, clipData.volume, clipData.pitch, clipData.loop, fadeDuration, ignoreTimeScale, onComplete); }
        /// <summary>
        /// Fades out then stops all sources in the given group, while starting the given clip with a fade-in effect.
        /// <para>Returns the <see cref="DeAudioSource"/> instance used to play, or NULL if the clip couldn't be played</para>
        /// </summary>
        public static DeAudioSource Crossfade(DeAudioGroupId groupId, AudioClip clip, float volume = 1, float pitch = 1, bool loop = false, float fadeDuration = 1.5f, bool ignoreTimeScale = true, TweenCallback onComplete = null)
        {
            DeAudioGroup group = GetAudioGroup(groupId);
            if (group == null) {
                Debug.LogWarning(LogPrefix + "Crossfade can't happend and clip can't be played because no group with the given groupId (" + groupId + ") was created");
                return null;
            }
            return group.Crossfade(clip, volume, pitch, loop, fadeDuration, ignoreTimeScale, onComplete);
        }

        #endregion

        #endregion

        #region Methods

        static void IterateOnAllGroups(OperationType operationType, AudioClip clip = null, float floatValue = 0)
        {
            int len = _audioGroups.Length;
            for (int i = 0; i < len; ++i) {
                DeAudioGroup group = _audioGroups[i];
                switch (operationType) {
                case OperationType.Stop:
                    group.Stop();
                    break;
                case OperationType.StopByClip:
                    group.Stop(clip);
                    break;
                case OperationType.SetVolumeByClip:
                    group.SetVolume(clip, floatValue);
                    break;
                case OperationType.Unlock:
                    group.Unlock();
                    break;
                case OperationType.UnlockByClip:
                    group.Unlock(clip);
                    break;
                }
            }
        }

        #endregion
    }
}