using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetThem.Game
{
    /// <summary>Original synthesized music and effects; no downloaded recordings or audio-thread allocations.</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private AudioSource music, effects, laser;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> nextSound = new Dictionary<string, float>();
        private bool focused = true;
        public bool MusicEnabled { get; private set; }
        public bool EffectsEnabled { get; private set; }

        private AudioSource Source(float volume, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            source.loop = loop;
            return source;
        }
        private AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            clips.Add(name, clip);
            return clip;
        }
        private void Awake()
        {
            MusicEnabled = PlayerPrefs.GetInt("audio.music", 1) != 0;
            EffectsEnabled = PlayerPrefs.GetInt("audio.effects", 1) != 0;
            music = Source(.32f, true); effects = Source(.5f, false); laser = Source(.1f, true);
            music.clip = Clip("Garden groove", Compose());
            laser.clip = Clip("laser-loop", Tone(1, 110, 110, 0, false));
            Clip("punch", Tone(.16f, 150, 45, .4f));
            Clip("arrow", Tone(.18f, 1100, 280, .25f));
            Clip("pet", Tone(.1f, 700, 950, 0));
            Clip("drone", Tone(.08f, 480, 240, .08f));
            Clip("hurt", Tone(.2f, 180, 65, .3f));
            Clip("upgrade", Tone(.35f, 520, 1040, 0));
            Clip("boss", Tone(.5f, 130, 260, .1f));
            Clip("win", Tone(.65f, 440, 880, 0));
            Clip("lose", Tone(.6f, 300, 75, .05f));
            music.mute = !MusicEnabled;
            music.Play();
        }
        public void ToggleMusic()
        {
            MusicEnabled = !MusicEnabled;
            music.mute = !MusicEnabled;
            PlayerPrefs.SetInt("audio.music", MusicEnabled ? 1 : 0); PlayerPrefs.Save();
        }
        public void ToggleEffects()
        {
            EffectsEnabled = !EffectsEnabled;
            if (!EffectsEnabled) { effects.Stop(); laser.Stop(); }
            PlayerPrefs.SetInt("audio.effects", EffectsEnabled ? 1 : 0); PlayerPrefs.Save();
        }
        public void Sound(string name)
        {
            if (!focused || !EffectsEnabled || !clips.TryGetValue(name, out var clip)) return;
            if (nextSound.TryGetValue(name, out float next) && Time.unscaledTime < next) return;
            nextSound[name] = Time.unscaledTime + .08f;
            effects.PlayOneShot(clip);
        }
        public void SetCombat(bool active, bool firing, bool boss)
        {
            music.volume = Mathf.MoveTowards(music.volume, active ? .32f : .17f, Time.unscaledDeltaTime * .3f);
            music.pitch = Mathf.MoveTowards(music.pitch, active && boss ? 1.12f : 1, Time.unscaledDeltaTime * .1f);
            bool audible = focused && active && firing && EffectsEnabled;
            if (audible && !laser.isPlaying) laser.Play();
            else if (!audible && laser.isPlaying) laser.Stop();
        }
        private void OnApplicationFocus(bool focus)
        {
            focused = focus;
            if (!focus) { music.Pause(); effects.Stop(); laser.Stop(); }
            else music.UnPause();
        }
        private void OnApplicationPause(bool pause) { OnApplicationFocus(!pause); }
        private void OnDestroy() { foreach (var clip in clips.Values) Destroy(clip); }

        private static float[] Tone(float seconds, float start, float end, float noise, bool envelope = true)
        {
            var data = new float[(int)(seconds * Rate)];
            var random = new System.Random(71);
            double phase = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / data.Length;
                phase += 2 * Math.PI * (start + (end - start) * t) / Rate;
                float fade = envelope ? Mathf.Min(1, t * 60) * (1 - t) * (1 - t) : 1;
                data[i] = ((float)Math.Sin(phase) * .55f + (float)(random.NextDouble() * 2 - 1) * noise) * fade;
            }
            return data;
        }
        private static float[] Compose()
        {
            // Eight bars at 120 BPM. Pentatonic melody, warm bass, kick and quiet shuffled hats.
            const float beat = .5f;
            var data = new float[Rate * 16];
            int[] roots = { 48, 53, 55, 48, 57, 53, 55, 48 };
            int[] melody = { 0, 7, 12, 9, 7, 4, 2, 7 };
            var random = new System.Random(1709);
            for (int step = 0; step < 64; step++)
            {
                int root = roots[step / 8];
                int offset = (int)(step * beat * .5f * Rate);
                double note = 440 * Math.Pow(2, (root + 12 + melody[step % 8] - 69) / 12.0);
                double bass = 440 * Math.Pow(2, (root - 12 - 69) / 12.0);
                for (int j = 0; j < Rate / 4; j++)
                {
                    double t = (double)j / Rate;
                    double attack = Math.Min(1, t * 180);
                    double bell = (Math.Sin(2 * Math.PI * note * t) + .22 * Math.Sin(4 * Math.PI * note * t)) * Math.Exp(-t * 15) * .15;
                    double low = step % 2 == 0 ? Math.Sin(2 * Math.PI * bass * t) * Math.Exp(-t * 9) * .18 : 0;
                    double kick = step % 4 == 0 ? Math.Sin(2 * Math.PI * (48 * t + 1.5 * (1 - Math.Exp(-t * 40)))) * Math.Exp(-t * 26) * .16 : 0;
                    double hat = (random.NextDouble() * 2 - 1) * Math.Exp(-t * 95) * (step % 2 == 0 ? .025 : .04);
                    double tail = Math.Min(1, (.25 - t) * 160);
                    data[offset + j] = (float)((bell + low + kick + hat) * attack * tail);
                }
            }
            return data;
        }
    }
}
