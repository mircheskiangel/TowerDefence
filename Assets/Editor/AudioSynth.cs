using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    // Editor-only audio generator, the sound counterpart of PixelArt / IsoArt: every clip of the game is
    // synthesized here from oscillators, noise, envelopes, filters and an echo, then written as a 16-bit
    // mono WAV into Assets/Audio/Generated/ with fixed import settings. Fixed seeds and no
    // UnityEngine.Random, so a rebuild writes byte-identical files. Music loops are rendered into a buffer
    // of exactly N bars; notes and echoes that run past the end wrap around to the start, and filters run
    // twice around the loop, so the loop seam is continuous.
    public static class AudioSynth
    {
        public const string Folder = "Assets/Audio/Generated";
        private const int SfxRate = 44100;
        private const int MusicRate = 22050;
        private const float SfxPeak = 0.708f;   // -3 dBFS
        private const float MusicPeak = 0.355f; // -9 dBFS

        public struct ClipInfo
        {
            public string name;
            public bool isMusic, loops;
            public float seconds, peak, seamJump, maxStep;
            public long bytes;
            public bool hasNaN, clipped;
        }

        public static readonly List<ClipInfo> Report = new List<ClipInfo>();

        public static void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Audio", "Generated");
            Report.Clear();
            Sfx("sfx_shoot_basic", ShootBasic());
            Sfx("sfx_shoot_siege", ShootSiege());
            Sfx("sfx_hit_grunt", HitGrunt());
            Sfx("sfx_hit_tank", HitTank());
            Sfx("sfx_hit_boss", HitBoss());
            Sfx("sfx_death", Death());
            Sfx("sfx_death_boss", DeathBoss());
            Sfx("sfx_ui_click", UiClick());
            Sfx("sfx_place_tower", PlaceTower());
            Sfx("sfx_blocked", Blocked());
            Sfx("sfx_life_lost", LifeLost());
            Sfx("sfx_wave_start", WaveStart());
            Sfx("sfx_boss_approach", BossApproach());
            Music("music_menu", MenuMusic(), true);
            Music("music_game", GameMusic(), true);
            Music("music_boss", BossMusic(), true);
            Music("jingle_win", WinJingle(), false);
            Music("jingle_lose", LoseJingle(), false);
        }

        // ================================================================ Building blocks

        private enum Wave { Sine, Square, Triangle, Saw }

        // xorshift32 with a fixed seed per clip.
        private class Rng
        {
            private uint s;
            public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s / 4294967296f; }
            public float Signed() { return Next() * 2f - 1f; }
        }

        private static float[] Buffer(float seconds, int rate) { return new float[Mathf.RoundToInt(seconds * rate)]; }

        private static float Osc(Wave wave, double phase)
        {
            float p = (float)(phase - Math.Floor(phase));
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
                case Wave.Saw: return 2f * p - 1f;
                default: return Mathf.Sin(2f * Mathf.PI * p);
            }
        }

        // Attack, decay to sustain, hold until 'gate', release.
        private static float Adsr(float t, float a, float d, float s, float r, float gate)
        {
            if (t < 0f) return 0f;
            float level;
            if (t < a) level = t / Mathf.Max(a, 1e-5f);
            else if (t < a + d) level = 1f - (1f - s) * (t - a) / Mathf.Max(d, 1e-5f);
            else level = s;
            if (t > gate)
            {
                float held = gate < a ? gate / Mathf.Max(a, 1e-5f) : gate < a + d ? 1f - (1f - s) * (gate - a) / Mathf.Max(d, 1e-5f) : s;
                level = held * Mathf.Max(0f, 1f - (t - gate) / Mathf.Max(r, 1e-5f));
            }
            return level;
        }

        private static float Exp(float t, float decay) { return Mathf.Exp(-t / Mathf.Max(decay, 1e-5f)); }

        private static float Note(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        // A tone with an exponential pitch sweep f0 -> f1 over 'sweep' seconds, ADSR shaped, with vibrato.
        // wrap: past the end of the buffer it continues at the start (loops).
        private static void Tone(float[] buf, int rate, float start, float gate, float f0, float f1, float sweep, Wave wave,
            float amp, float a, float d, float s, float r, bool wrap = false, float vibrato = 0f, float vibratoRate = 5f, float detune = 0f)
        {
            int n0 = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt((gate + r) * rate);
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float k = sweep > 0f ? Mathf.Clamp01(t / sweep) : 1f;
                float f = f0 * Mathf.Pow(f1 / f0, k) * (1f + detune);
                if (vibrato > 0f) f *= 1f + vibrato * Mathf.Sin(2f * Mathf.PI * vibratoRate * t);
                phase += f / rate;
                int idx = n0 + i;
                if (idx >= buf.Length) { if (!wrap) break; idx %= buf.Length; }
                buf[idx] += amp * Adsr(t, a, d, s, r, gate) * Osc(wave, phase);
            }
        }

        // A noise burst with an attack and an exponential decay.
        private static void Noise(float[] buf, int rate, float start, float duration, float amp, float attack, float decay, Rng rng, bool wrap = false)
        {
            int n0 = Mathf.RoundToInt(start * rate);
            int length = Mathf.RoundToInt(duration * rate);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float env = (t < attack ? t / Mathf.Max(attack, 1e-5f) : 1f) * Exp(t - attack, decay);
                int idx = n0 + i;
                if (idx >= buf.Length) { if (!wrap) break; idx %= buf.Length; }
                buf[idx] += amp * env * rng.Signed();
            }
        }

        // One-pole low-pass; on a loop it runs twice around, so the start already has the end's state.
        private static void LowPass(float[] buf, int rate, float cutoff, bool loop = false)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
            float y = 0f;
            int passes = loop ? 2 : 1;
            float[] src = loop ? (float[])buf.Clone() : buf;
            for (int pass = 0; pass < passes; pass++)
                for (int i = 0; i < buf.Length; i++)
                {
                    y += a * (src[i] - y);
                    if (pass == passes - 1) buf[i] = y;
                }
        }

        private static void HighPass(float[] buf, int rate, float cutoff, bool loop = false)
        {
            float[] low = (float[])buf.Clone();
            LowPass(low, rate, cutoff, loop);
            for (int i = 0; i < buf.Length; i++) buf[i] -= low[i];
        }

        // Feedback echo; on a loop the delay line reads around the end.
        private static void Echo(float[] buf, int rate, float delay, float feedback, float mix, bool loop = false)
        {
            int d = Mathf.Max(1, Mathf.RoundToInt(delay * rate));
            float[] wet = new float[buf.Length];
            int passes = loop ? 3 : 1;
            for (int pass = 0; pass < passes; pass++)
                for (int i = 0; i < buf.Length; i++)
                {
                    int j = i - d;
                    float back = j >= 0 ? wet[j] : loop ? wet[j + buf.Length] : 0f;
                    wet[i] = (j >= 0 || loop ? (j >= 0 ? buf[j] : buf[j + buf.Length]) : 0f) + feedback * back;
                }
            for (int i = 0; i < buf.Length; i++) buf[i] += mix * wet[i];
        }

        private static void Mix(float[] into, float[] from, float gain)
        {
            for (int i = 0; i < into.Length && i < from.Length; i++) into[i] += gain * from[i];
        }

        // Soft limiter (tanh) and normalization to the target peak: nothing can clip.
        private static void Finish(float[] buf, float peak, float drive = 1.2f)
        {
            float max = 1e-9f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(drive * buf[i] / max);
            max = 1e-9f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        // Short fades at both ends of a one-shot, so it starts and stops without a click.
        private static void Edges(float[] buf, int rate, float fadeIn = 0.002f, float fadeOut = 0.01f)
        {
            int a = Mathf.Min(buf.Length, Mathf.RoundToInt(fadeIn * rate));
            int b = Mathf.Min(buf.Length, Mathf.RoundToInt(fadeOut * rate));
            for (int i = 0; i < a; i++) buf[i] *= i / (float)a;
            for (int i = 0; i < b; i++) buf[buf.Length - 1 - i] *= i / (float)b;
        }

        // ================================================================ Sound effects (44.1 kHz)

        private static float[] ShootBasic()
        {
            var rng = new Rng(101); var b = Buffer(0.12f, SfxRate);
            Tone(b, SfxRate, 0f, 0.02f, 1250f, 520f, 0.09f, Wave.Square, 0.5f, 0.002f, 0.04f, 0.3f, 0.08f);
            Tone(b, SfxRate, 0f, 0.02f, 2500f, 1040f, 0.09f, Wave.Triangle, 0.25f, 0.001f, 0.03f, 0.2f, 0.05f);
            Noise(b, SfxRate, 0f, 0.03f, 0.25f, 0.001f, 0.008f, rng);
            LowPass(b, SfxRate, 5000f);
            return Done(b, SfxRate);
        }

        private static float[] ShootSiege()
        {
            var rng = new Rng(102); var b = Buffer(0.5f, SfxRate);
            Tone(b, SfxRate, 0f, 0.05f, 150f, 42f, 0.18f, Wave.Sine, 1.0f, 0.002f, 0.12f, 0.5f, 0.3f);
            Tone(b, SfxRate, 0f, 0.02f, 300f, 80f, 0.08f, Wave.Triangle, 0.4f, 0.001f, 0.05f, 0.2f, 0.1f);
            var tail = Buffer(0.5f, SfxRate);
            Noise(tail, SfxRate, 0f, 0.5f, 0.9f, 0.003f, 0.11f, rng);
            LowPass(tail, SfxRate, 900f);
            Mix(b, tail, 1f);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(1.6f * b[i]); // A little grit.
            return Done(b, SfxRate);
        }

        private static float[] HitGrunt()
        {
            var rng = new Rng(103); var b = Buffer(0.1f, SfxRate);
            Noise(b, SfxRate, 0f, 0.04f, 0.6f, 0.0005f, 0.01f, rng);
            Tone(b, SfxRate, 0f, 0.01f, 420f, 220f, 0.05f, Wave.Sine, 0.8f, 0.001f, 0.03f, 0.2f, 0.05f);
            LowPass(b, SfxRate, 3000f);
            return Done(b, SfxRate);
        }

        private static float[] HitTank()
        {
            var rng = new Rng(104); var b = Buffer(0.15f, SfxRate);
            foreach (float f in new[] { 310f, 697f, 1043f, 1580f })
                Tone(b, SfxRate, 0f, 0.01f, f, f * 0.97f, 0.1f, Wave.Sine, 0.35f, 0.001f, 0.05f, 0.15f, 0.08f);
            Noise(b, SfxRate, 0f, 0.05f, 0.4f, 0.0005f, 0.015f, rng);
            LowPass(b, SfxRate, 2200f);
            return Done(b, SfxRate);
        }

        private static float[] HitBoss()
        {
            var rng = new Rng(105); var b = Buffer(0.25f, SfxRate);
            Tone(b, SfxRate, 0f, 0.02f, 160f, 120f, 0.2f, Wave.Sine, 0.8f, 0.001f, 0.1f, 0.4f, 0.13f);
            foreach (float f in new[] { 441f, 1093f, 2380f, 3170f })
                Tone(b, SfxRate, 0f, 0.01f, f, f, 0f, Wave.Sine, 0.2f, 0.001f, 0.12f, 0.2f, 0.11f);
            Noise(b, SfxRate, 0f, 0.04f, 0.3f, 0.0005f, 0.01f, rng);
            return Done(b, SfxRate);
        }

        private static float[] Death()
        {
            var rng = new Rng(106); var b = Buffer(0.25f, SfxRate);
            Tone(b, SfxRate, 0f, 0.03f, 260f, 900f, 0.06f, Wave.Triangle, 0.6f, 0.001f, 0.04f, 0.3f, 0.05f);
            var puff = Buffer(0.25f, SfxRate);
            Noise(puff, SfxRate, 0.01f, 0.24f, 0.7f, 0.01f, 0.06f, rng);
            LowPass(puff, SfxRate, 1400f);
            Mix(b, puff, 1f);
            return Done(b, SfxRate);
        }

        private static float[] DeathBoss()
        {
            var rng = new Rng(107); var b = Buffer(1.5f, SfxRate);
            var rumble = Buffer(1.5f, SfxRate);
            Noise(rumble, SfxRate, 0f, 1.5f, 1f, 0.02f, 0.45f, rng);
            LowPass(rumble, SfxRate, 140f);
            LowPass(rumble, SfxRate, 140f);
            Mix(b, rumble, 3f);
            Tone(b, SfxRate, 0f, 0.2f, 70f, 38f, 1.0f, Wave.Sine, 0.6f, 0.005f, 0.3f, 0.5f, 1.0f);
            // The crystals shatter: pings that rise in pitch and come faster.
            for (int k = 0; k < 26; k++)
            {
                float t = 0.15f + 1.1f * Mathf.Pow(k / 26f, 0.8f);
                float f = 1400f * Mathf.Pow(2f, k / 12f) * (1f + 0.04f * rng.Signed());
                Tone(b, SfxRate, t, 0.005f, f, f, 0f, Wave.Sine, 0.18f, 0.001f, 0.08f, 0.1f, 0.12f);
            }
            Echo(b, SfxRate, 0.11f, 0.35f, 0.4f);
            return Done(b, SfxRate);
        }

        private static float[] UiClick()
        {
            var b = Buffer(0.05f, SfxRate);
            Tone(b, SfxRate, 0f, 0.005f, 1600f, 1200f, 0.02f, Wave.Sine, 0.8f, 0.0005f, 0.015f, 0.1f, 0.02f);
            Tone(b, SfxRate, 0f, 0.003f, 3200f, 2400f, 0.02f, Wave.Triangle, 0.2f, 0.0005f, 0.01f, 0.1f, 0.01f);
            return Done(b, SfxRate);
        }

        private static float[] PlaceTower()
        {
            var rng = new Rng(109); var b = Buffer(0.2f, SfxRate);
            Tone(b, SfxRate, 0f, 0.03f, 170f, 70f, 0.08f, Wave.Sine, 0.9f, 0.001f, 0.06f, 0.3f, 0.1f);
            var stone = Buffer(0.2f, SfxRate);
            Noise(stone, SfxRate, 0f, 0.15f, 0.8f, 0.001f, 0.03f, rng);
            LowPass(stone, SfxRate, 1100f);
            Mix(b, stone, 1f);
            return Done(b, SfxRate);
        }

        private static float[] Blocked()
        {
            var b = Buffer(0.3f, SfxRate);
            Tone(b, SfxRate, 0f, 0.09f, 150f, 150f, 0f, Wave.Square, 0.5f, 0.003f, 0.02f, 0.8f, 0.03f);
            Tone(b, SfxRate, 0.14f, 0.11f, 120f, 120f, 0f, Wave.Square, 0.5f, 0.003f, 0.02f, 0.8f, 0.04f);
            LowPass(b, SfxRate, 1200f);
            return Done(b, SfxRate);
        }

        private static float[] LifeLost()
        {
            var b = Buffer(0.6f, SfxRate);
            Tone(b, SfxRate, 0f, 0.2f, 330f, 330f, 0f, Wave.Triangle, 0.6f, 0.005f, 0.05f, 0.8f, 0.05f);
            Tone(b, SfxRate, 0.25f, 0.25f, 247f, 247f, 0f, Wave.Triangle, 0.6f, 0.005f, 0.05f, 0.8f, 0.1f);
            Tone(b, SfxRate, 0f, 0.5f, 110f, 104f, 0.5f, Wave.Sine, 0.4f, 0.01f, 0.1f, 0.7f, 0.08f);
            LowPass(b, SfxRate, 2500f);
            return Done(b, SfxRate);
        }

        private static float[] WaveStart()
        {
            var b = Buffer(0.8f, SfxRate);
            foreach (int note in new[] { 50, 57, 62 })
            {
                Tone(b, SfxRate, 0f, 0.55f, Note(note), Note(note), 0f, Wave.Saw, 0.35f, 0.08f, 0.1f, 0.8f, 0.2f, false, 0.006f, 5f);
                Tone(b, SfxRate, 0f, 0.55f, Note(note), Note(note), 0f, Wave.Saw, 0.25f, 0.08f, 0.1f, 0.8f, 0.2f, false, 0.006f, 5.5f, 0.004f);
            }
            LowPass(b, SfxRate, 1600f);
            return Done(b, SfxRate);
        }

        private static float[] BossApproach()
        {
            var b = Buffer(2.0f, SfxRate);
            foreach (int note in new[] { 33, 40, 45 })
            {
                Tone(b, SfxRate, 0f, 1.5f, Note(note), Note(note) * 0.985f, 1.5f, Wave.Saw, 0.4f, 0.5f, 0.3f, 0.8f, 0.45f, false, 0.004f, 3f);
                Tone(b, SfxRate, 0f, 1.5f, Note(note), Note(note) * 0.985f, 1.5f, Wave.Saw, 0.3f, 0.5f, 0.3f, 0.8f, 0.45f, false, 0.004f, 3.4f, 0.006f);
            }
            LowPass(b, SfxRate, 600f);
            LowPass(b, SfxRate, 900f);
            Echo(b, SfxRate, 0.25f, 0.3f, 0.35f);
            return Done(b, SfxRate);
        }

        private static float[] Done(float[] b, int rate)
        {
            Finish(b, SfxPeak);
            Edges(b, rate);
            return b;
        }

        // ================================================================ Music (22.05 kHz)

        // One instrument track at a time is rendered, filtered and mixed, so filters stay per track.
        private class Song
        {
            public readonly int rate = MusicRate;
            public readonly float beat;
            public readonly float[] mix;
            public readonly bool loop;
            public Song(float bpm, float seconds, bool loop)
            {
                beat = 60f / bpm;
                mix = new float[Mathf.RoundToInt(seconds * MusicRate)];
                this.loop = loop;
            }
            public float[] Track() { return new float[mix.Length]; }
            public void Add(float[] track, float gain) { Mix(mix, track, gain); }
        }

        private static void Kick(Song s, float[] t, float time, float amp)
        {
            Tone(t, s.rate, time, 0.02f, 130f, 42f, 0.12f, Wave.Sine, amp, 0.001f, 0.12f, 0.2f, 0.15f, s.loop);
        }

        private static void Snare(Song s, float[] t, float time, float amp, Rng rng)
        {
            Noise(t, s.rate, time, 0.2f, amp, 0.001f, 0.06f, rng, s.loop);
            Tone(t, s.rate, time, 0.01f, 210f, 180f, 0.05f, Wave.Triangle, amp * 0.5f, 0.001f, 0.04f, 0.2f, 0.05f, s.loop);
        }

        private static void Hat(Song s, float[] t, float time, float amp, Rng rng)
        {
            Noise(t, s.rate, time, 0.05f, amp, 0.0005f, 0.012f, rng, s.loop);
        }

        private static float[] GameMusic()
        {
            // D dorian, 96 bpm, 16 bars of 4/4 = 40 s: an 8-bar phrase and its variation.
            var s = new Song(96f, 16 * 4 * 60f / 96f, true);
            var rng = new Rng(201);
            float bar = 4f * s.beat;
            int[] roots = { 38, 36, 34, 36, 38, 41, 36, 38 };       // Dm C Bb C Dm F C Dm
            int[][] chords = { new[] { 62, 65, 69 }, new[] { 60, 64, 67 }, new[] { 58, 62, 65 }, new[] { 60, 64, 67 },
                               new[] { 62, 65, 69 }, new[] { 60, 65, 69 }, new[] { 60, 64, 67 }, new[] { 62, 65, 69 } };
            int[][] melodyA = { new[] { 62, 65, 69, 67 }, new[] { 64, 64, 60, 62 }, new[] { 65, 62, 58, 62 }, new[] { 64, 67, 64, 60 },
                                new[] { 62, 65, 69, 72 }, new[] { 69, 67, 65, 64 }, new[] { 64, 62, 60, 64 }, new[] { 62, -1, 62, -1 } };
            int[][] melodyB = { new[] { 69, 72, 74, 72 }, new[] { 71, 72, 67, 64 }, new[] { 70, 69, 65, 62 }, new[] { 64, 67, 72, 71 },
                                new[] { 74, 72, 69, 72 }, new[] { 72, 69, 65, 69 }, new[] { 67, 64, 60, 64 }, new[] { 62, -1, 62, -1 } };
            float[] drums = s.Track(), bass = s.Track(), lead = s.Track(), pad = s.Track();
            for (int b = 0; b < 16; b++)
            {
                float t0 = b * bar;
                int c = b % 8;
                // Drums: kick on 1 and 3 (and the "and" of 3 in odd bars), snare on 2 and 4, eighth hats.
                Kick(s, drums, t0, 0.9f);
                Kick(s, drums, t0 + 2 * s.beat, 0.8f);
                if (b % 2 == 1) Kick(s, drums, t0 + 2.5f * s.beat, 0.6f);
                Snare(s, drums, t0 + s.beat, 0.45f, rng);
                Snare(s, drums, t0 + 3 * s.beat, 0.45f, rng);
                for (int h = 0; h < 8; h++) Hat(s, drums, t0 + h * s.beat * 0.5f, h % 2 == 0 ? 0.18f : 0.1f, rng);
                // Bass: root, octave, fifth, octave in eighths.
                int[] pattern = { 0, 12, 7, 12, 0, 12, 7, 5 };
                for (int e = 0; e < 8; e++)
                    Tone(bass, s.rate, t0 + e * s.beat * 0.5f, s.beat * 0.4f, Note(roots[c] + pattern[e]), Note(roots[c] + pattern[e]), 0f,
                        Wave.Saw, 0.5f, 0.005f, 0.08f, 0.6f, 0.05f, true);
                // Pad: soft triad under everything.
                foreach (int n in chords[c])
                {
                    Tone(pad, s.rate, t0, bar * 0.95f, Note(n - 12), Note(n - 12), 0f, Wave.Saw, 0.12f, 0.3f, 0.3f, 0.7f, 0.3f, true);
                    Tone(pad, s.rate, t0, bar * 0.95f, Note(n - 12), Note(n - 12), 0f, Wave.Saw, 0.12f, 0.3f, 0.3f, 0.7f, 0.3f, true, 0f, 5f, 0.005f);
                }
                // Lead: one note per beat; the second phrase is higher.
                int[] notes = b < 8 ? melodyA[c] : melodyB[c];
                for (int q = 0; q < 4; q++)
                {
                    if (notes[q] < 0) continue;
                    float len = c == 7 && q == 2 ? 1.8f * s.beat : 0.85f * s.beat;
                    Tone(lead, s.rate, t0 + q * s.beat, len, Note(notes[q]), Note(notes[q]), 0f, Wave.Square, 0.3f, 0.01f, 0.1f, 0.6f, 0.1f, true, 0.004f, 5.5f);
                    Tone(lead, s.rate, t0 + q * s.beat, len, Note(notes[q] + 12), Note(notes[q] + 12), 0f, Wave.Triangle, 0.15f, 0.01f, 0.1f, 0.5f, 0.1f, true);
                }
            }
            HighPass(drums, s.rate, 40f, true);
            LowPass(bass, s.rate, 700f, true);
            LowPass(pad, s.rate, 1400f, true);
            LowPass(lead, s.rate, 3200f, true);
            Echo(lead, s.rate, s.beat * 0.75f, 0.3f, 0.3f, true);
            s.Add(drums, 1f); s.Add(bass, 0.8f); s.Add(pad, 0.6f); s.Add(lead, 0.7f);
            Finish(s.mix, MusicPeak);
            return s.mix;
        }

        private static float[] MenuMusic()
        {
            // A minor pentatonic, 64 bpm, 8 bars = 30 s: slow pads and a soft arpeggio with an echo.
            var s = new Song(64f, 8 * 4 * 60f / 64f, true);
            float bar = 4f * s.beat;
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 },
                               new[] { 57, 60, 64 }, new[] { 52, 55, 59 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };
            int[] scale = { 69, 72, 74, 76, 79, 81 }; // A C D E G A
            int[][] arp = { new[] { 0, 2, 3, 1, 0, 3, 2, 1 }, new[] { 1, 3, 4, 2, 1, 0, 2, 3 }, new[] { 0, 1, 3, 4, 3, 1, 0, 2 }, new[] { 4, 3, 1, 2, 4, 5, 3, 1 } };
            float[] pad = s.Track(), pluck = s.Track();
            for (int b = 0; b < 8; b++)
            {
                float t0 = b * bar;
                foreach (int n in chords[b])
                {
                    Tone(pad, s.rate, t0, bar, Note(n - 12), Note(n - 12), 0f, Wave.Saw, 0.12f, 1.2f, 0.5f, 0.8f, 1.0f, true, 0.003f, 0.3f);
                    Tone(pad, s.rate, t0, bar, Note(n), Note(n), 0f, Wave.Triangle, 0.15f, 1.0f, 0.5f, 0.8f, 1.0f, true, 0.003f, 0.25f, 0.003f);
                }
                int[] seq = arp[b % 4];
                for (int e = 0; e < 8; e++)
                {
                    int note = scale[seq[e]] - (b % 2 == 1 && e > 5 ? 12 : 0);
                    Tone(pluck, s.rate, t0 + e * s.beat * 0.5f, 0.05f, Note(note), Note(note), 0f, Wave.Triangle, 0.35f, 0.003f, 0.35f, 0f, 0.4f, true);
                }
            }
            LowPass(pad, s.rate, 900f, true);
            LowPass(pluck, s.rate, 2600f, true);
            Echo(pluck, s.rate, s.beat * 0.75f, 0.45f, 0.5f, true);
            s.Add(pad, 0.9f); s.Add(pluck, 0.7f);
            Finish(s.mix, MusicPeak);
            return s.mix;
        }

        private static float[] BossMusic()
        {
            // E phrygian, 128 bpm, 16 bars = 30 s: driving drums, a pounding bass, a dark low motif.
            var s = new Song(128f, 16 * 4 * 60f / 128f, true);
            var rng = new Rng(203);
            float bar = 4f * s.beat;
            int[] roots = { 40, 41, 40, 38, 40, 41, 43, 41 }; // E F E D E F G F
            int[][] motif = { new[] { 64, 65, 64, 59 }, new[] { 65, 64, 62, 64 }, new[] { 64, 67, 65, 64 }, new[] { 62, 64, 65, 62 } };
            float[] drums = s.Track(), bass = s.Track(), lead = s.Track(), drone = s.Track();
            for (int b = 0; b < 16; b++)
            {
                float t0 = b * bar;
                int c = b % 8;
                for (int q = 0; q < 4; q++) Kick(s, drums, t0 + q * s.beat, 0.85f);
                Snare(s, drums, t0 + s.beat, 0.5f, rng);
                Snare(s, drums, t0 + 3 * s.beat, 0.5f, rng);
                if (b % 4 == 3) Snare(s, drums, t0 + 3.5f * s.beat, 0.35f, rng);
                for (int h = 0; h < 16; h++) Hat(s, drums, t0 + h * s.beat * 0.25f, h % 4 == 0 ? 0.16f : 0.08f, rng);
                for (int e = 0; e < 8; e++)
                {
                    int n = roots[c] + (e % 4 == 3 ? 12 : 0);
                    Tone(bass, s.rate, t0 + e * s.beat * 0.5f, s.beat * 0.35f, Note(n), Note(n), 0f, Wave.Saw, 0.55f, 0.003f, 0.06f, 0.5f, 0.04f, true);
                }
                Tone(drone, s.rate, t0, bar, Note(roots[c] - 12), Note(roots[c] - 12), 0f, Wave.Saw, 0.25f, 0.2f, 0.2f, 0.8f, 0.2f, true, 0.003f, 0.5f);
                Tone(drone, s.rate, t0, bar, Note(roots[c] - 5), Note(roots[c] - 5), 0f, Wave.Saw, 0.15f, 0.2f, 0.2f, 0.8f, 0.2f, true, 0.003f, 0.6f, 0.004f);
                int[] notes = motif[(b / 2) % 4];
                for (int q = 0; q < 4; q++)
                    Tone(lead, s.rate, t0 + q * s.beat, 0.9f * s.beat, Note(notes[q] - (b < 8 ? 12 : 0)), Note(notes[q] - (b < 8 ? 12 : 0)), 0f,
                        Wave.Square, 0.25f, 0.01f, 0.1f, 0.6f, 0.08f, true, 0.008f, 6f);
            }
            HighPass(drums, s.rate, 40f, true);
            LowPass(bass, s.rate, 600f, true);
            LowPass(drone, s.rate, 500f, true);
            LowPass(lead, s.rate, 2400f, true);
            Echo(lead, s.rate, s.beat * 0.5f, 0.3f, 0.25f, true);
            s.Add(drums, 1f); s.Add(bass, 0.8f); s.Add(drone, 0.6f); s.Add(lead, 0.6f);
            Finish(s.mix, MusicPeak);
            return s.mix;
        }

        private static float[] WinJingle()
        {
            // C major fanfare, about 6 s: a rising call and a held final chord, with timpani hits.
            var s = new Song(120f, 6f, false);
            var rng = new Rng(301);
            float[] brass = s.Track(), drums = s.Track();
            (float t, int n, float len)[] notes = { (0f, 67, 0.15f), (0.15f, 72, 0.15f), (0.3f, 76, 0.3f), (0.7f, 74, 0.18f),
                (0.9f, 76, 0.18f), (1.1f, 77, 0.18f), (1.3f, 79, 0.6f) };
            foreach (var x in notes)
            {
                Tone(brass, s.rate, x.t, x.len, Note(x.n), Note(x.n), 0f, Wave.Saw, 0.4f, 0.02f, 0.08f, 0.8f, 0.1f, false, 0.004f, 5.5f);
                Tone(brass, s.rate, x.t, x.len, Note(x.n - 12), Note(x.n - 12), 0f, Wave.Square, 0.2f, 0.02f, 0.08f, 0.7f, 0.1f);
            }
            foreach (int n in new[] { 60, 64, 67, 72, 76 })
            {
                Tone(brass, s.rate, 2.0f, 3.0f, Note(n), Note(n), 0f, Wave.Saw, 0.22f, 0.08f, 0.3f, 0.7f, 0.9f, false, 0.005f, 5f);
                Tone(brass, s.rate, 2.0f, 3.0f, Note(n), Note(n), 0f, Wave.Saw, 0.15f, 0.08f, 0.3f, 0.7f, 0.9f, false, 0.005f, 5.4f, 0.004f);
            }
            foreach (float t in new[] { 0f, 0.7f, 1.3f, 2.0f })
            {
                Tone(drums, s.rate, t, 0.05f, 98f, 82f, 0.2f, Wave.Sine, 0.9f, 0.002f, 0.3f, 0.3f, 0.4f);
                Noise(drums, s.rate, t, 0.3f, 0.25f, 0.002f, 0.08f, rng);
            }
            for (int k = 0; k < 12; k++) Tone(drums, s.rate, 1.6f + k * 0.033f, 0.02f, 98f, 90f, 0.05f, Wave.Sine, 0.25f + 0.03f * k, 0.002f, 0.05f, 0.2f, 0.05f);
            LowPass(brass, s.rate, 2800f);
            LowPass(drums, s.rate, 1500f);
            s.Add(brass, 1f); s.Add(drums, 0.8f);
            Echo(s.mix, s.rate, 0.18f, 0.25f, 0.2f);
            Finish(s.mix, MusicPeak);
            Edges(s.mix, s.rate, 0.002f, 0.3f);
            return s.mix;
        }

        private static float[] LoseJingle()
        {
            // A minor, about 4.5 s: a slow descending phrase over a dark pad.
            var s = new Song(90f, 4.5f, false);
            float[] voice = s.Track(), pad = s.Track();
            int[] phrase = { 76, 74, 72, 71 };
            for (int i = 0; i < phrase.Length; i++)
                Tone(voice, s.rate, i * 0.5f, 0.42f, Note(phrase[i]), Note(phrase[i]), 0f, Wave.Triangle, 0.5f, 0.02f, 0.1f, 0.7f, 0.15f, false, 0.006f, 5f);
            Tone(voice, s.rate, 2.0f, 1.6f, Note(69), Note(69) * 0.985f, 1.6f, Wave.Triangle, 0.55f, 0.02f, 0.2f, 0.7f, 0.8f, false, 0.008f, 4.5f);
            foreach (int n in new[] { 45, 52, 57, 60 })
                Tone(pad, s.rate, 0f, 3.2f, Note(n), Note(n), 0f, Wave.Saw, 0.12f, 0.4f, 0.5f, 0.7f, 1.0f, false, 0.003f, 0.3f);
            LowPass(voice, s.rate, 2400f);
            LowPass(pad, s.rate, 700f);
            s.Add(voice, 1f); s.Add(pad, 0.8f);
            Echo(s.mix, s.rate, 0.3f, 0.3f, 0.3f);
            Finish(s.mix, MusicPeak);
            Edges(s.mix, s.rate, 0.002f, 0.4f);
            return s.mix;
        }

        // ================================================================ Files and import settings

        private static void Sfx(string name, float[] samples) { Save(name, samples, SfxRate, false, false); }
        private static void Music(string name, float[] samples, bool loops) { Save(name, samples, MusicRate, true, loops); }

        private static void Save(string name, float[] samples, int rate, bool isMusic, bool loops)
        {
            var info = new ClipInfo { name = name, isMusic = isMusic, loops = loops, seconds = samples.Length / (float)rate };
            float maxStep = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float v = samples[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) { info.hasNaN = true; v = 0f; samples[i] = 0f; }
                if (Mathf.Abs(v) >= 1f) info.clipped = true;
                info.peak = Mathf.Max(info.peak, Mathf.Abs(v));
                if (i > 0) maxStep = Mathf.Max(maxStep, Mathf.Abs(v - samples[i - 1]));
            }
            info.maxStep = maxStep;
            info.seamJump = loops && samples.Length > 1 ? Mathf.Abs(samples[0] - samples[samples.Length - 1]) : 0f;

            string path = $"{Folder}/{name}.wav";
            byte[] bytes = Wav(samples, rate);
            info.bytes = bytes.Length;
            bool changed = !File.Exists(path) || !BytesEqual(File.ReadAllBytes(path), bytes);
            if (changed) File.WriteAllBytes(path, bytes);
            Report.Add(info);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = isMusic;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = isMusic ? 0.6f : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        // 16-bit PCM mono WAV.
        private static byte[] Wav(float[] samples, int rate)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(rate);
                w.Write(rate * 2);       // byte rate
                w.Write((short)2);       // block align
                w.Write((short)16);      // bits per sample
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataBytes);
                foreach (float v in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
                w.Flush();
                return stream.ToArray();
            }
        }
    }
}
