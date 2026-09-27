using System;
using System.Collections.Generic;
using Sanguo.Events;
using UnityEngine;

namespace Sanguo.Audio
{
    public enum Sfx : byte
    {
        Click = 0,
        Card = 1,
        Damage = 2,
        Heal = 3,
        Death = 4,
        Turn = 5,
        Skill = 6,
        Victory = 7
    }

    /// <summary>
    /// Sound effects synthesised at runtime (placeholder audio; no files needed). Listens to game events,
    /// so audio stays decoupled from rules and UI. Replace clips with licensed assets later.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private IDisposable _subscription;
        private int _next;

        public float SfxVolume { get; set; } = 0.8f;
        public int ViewerId { get; set; } = -1;

        private void Awake()
        {
            for (int i = 0; i < 6; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _sources.Add(s);
            }
            _clips[Sfx.Click] = Tone("click", new[] { 1200f }, 0.04f, 0.25f);
            _clips[Sfx.Card] = Noise("card", 0.07f, 0.35f);
            _clips[Sfx.Damage] = Tone("damage", new[] { 140f, 90f }, 0.22f, 0.6f);
            _clips[Sfx.Heal] = Tone("heal", new[] { 520f, 660f, 880f }, 0.3f, 0.4f);
            _clips[Sfx.Death] = Tone("death", new[] { 330f, 247f, 165f }, 0.6f, 0.5f);
            _clips[Sfx.Turn] = Tone("turn", new[] { 784f }, 0.18f, 0.25f);
            _clips[Sfx.Skill] = Tone("skill", new[] { 440f, 880f }, 0.25f, 0.35f);
            _clips[Sfx.Victory] = Tone("victory", new[] { 523f, 659f, 784f, 1046f }, 0.8f, 0.45f);
        }

        public void Play(Sfx sfx)
        {
            if (SfxVolume <= 0f || !_clips.TryGetValue(sfx, out var clip)) return;
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            source.PlayOneShot(clip, SfxVolume);
        }

        /// <summary>Plays sounds for a game's event stream (replaces any previous subscription).</summary>
        public void Attach(GameEventBus events, int viewerId)
        {
            _subscription?.Dispose();
            ViewerId = viewerId;
            _subscription = events?.SubscribeAll(OnEvent);
        }

        private void OnEvent(GameEvent e)
        {
            switch (e)
            {
                case CardPlayedEvent _: Play(Sfx.Card); break;
                case DamageAppliedEvent _: Play(Sfx.Damage); break;
                case HealAppliedEvent _: Play(Sfx.Heal); break;
                case PlayerDiedEvent _: Play(Sfx.Death); break;
                case SkillActivatedEvent _: Play(Sfx.Skill); break;
                case TurnStartedEvent t when t.PlayerId == ViewerId: Play(Sfx.Turn); break;
                case GameEndedEvent _: Play(Sfx.Victory); break;
            }
        }

        private void OnDestroy() => _subscription?.Dispose();

        /// <summary>Consecutive sine notes with a quick attack and exponential decay.</summary>
        private static AudioClip Tone(string name, float[] notes, float seconds, float gain)
        {
            int total = (int)(SampleRate * seconds);
            var data = new float[total];
            int per = Math.Max(1, total / notes.Length);
            for (int i = 0; i < total; i++)
            {
                int n = Math.Min(notes.Length - 1, i / per);
                float t = (i - n * per) / (float)SampleRate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 8f);
                data[i] = Mathf.Sin(2f * Mathf.PI * notes[n] * t) * env * gain;
            }
            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Noise(string name, float seconds, float gain)
        {
            int total = (int)(SampleRate * seconds);
            var data = new float[total];
            var rng = new System.Random(7);
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 40f) * gain;
            }
            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
