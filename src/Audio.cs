// Audio.cs —— 大肥鱼桌宠：全部程序化合成的音效与环境音
// 目标框架：.NET Framework 4.x，C# 5 语法（Framework64\v4.0.30319\csc.exe）
// 特点：不读外部素材、不引用第三方库；全部 16bit PCM 单声道 22050Hz；
//       播放使用 System.Media.SoundPlayer（MemoryStream），音量在采样层缩放；
//       环境音为无缝循环（噪声滤波器环形预热 + LFO 整周期量化 + 随机事件回卷）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace FishPet
{
    public static class Audio
    {
        // ---------------- 常量 / 名称表 ----------------

        internal const int SampleRate = 22050;
        internal const int BitsPerSample = 16;
        internal const int Channels = 1;

        private const double Pi = 3.14159265358979323846;
        private const double TwoPi = 6.283185307179586476925286766559;

        private static readonly object Sync = new object();

        private static readonly string[] SfxList = new string[]
        {
            "click", "headpat", "feed", "eat", "happy", "sad", "surprised", "pop",
            "gacha", "coin", "pomodoro_start", "pomodoro_end", "alarm", "sleep",
            "snore", "hiccup", "bubble", "dizzy", "levelup", "error", "charge",
            "lowbattery", "notify"
        };

        private static readonly string[] AmbientList = new string[]
        {
            "none", "rain", "waves", "fire", "forest", "night", "white", "keyboard"
        };

        private static readonly string[] AmbientLabelList = new string[]
        {
            "关闭", "雨声", "海浪", "篝火", "森林", "夏夜虫鸣", "白噪音", "键盘声"
        };

        private static readonly Dictionary<string, float[]> SfxCache = new Dictionary<string, float[]>();
        private static readonly Dictionary<string, float[]> AmbientCache = new Dictionary<string, float[]>();
        private static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>();

        private static float _volume = 0.7f;
        private static bool _muted = false;
        private static string _ambientName = "none";
        private static bool _alarmOn = false;

        // ---------------- 播放器包装 ----------------

        private sealed class Clip
        {
            public SoundPlayer Player;
            public MemoryStream Stream;

            public void Dispose()
            {
                try { if (Player != null) Player.Stop(); } catch { }
                try
                {
                    IDisposable d = Player as IDisposable;
                    if (d != null) d.Dispose();
                }
                catch { }
                try { if (Stream != null) Stream.Dispose(); } catch { }
                Player = null;
                Stream = null;
            }
        }

        // ---------------- 公开 API ----------------

        public static void Init(string assetDir)
        {
            // 纯合成、无外部素材；采样全部懒加载缓存，这里无需任何工作，也绝不抛异常。
        }

        public static float Volume
        {
            get
            {
                lock (Sync) { return _volume; }
            }
            set
            {
                float v = value;
                if (float.IsNaN(v) || v < 0f) v = 0f;
                if (v > 1f) v = 1f;
                lock (Sync)
                {
                    if (Math.Abs(v - _volume) < 0.0001f) return;
                    _volume = v;
                    try
                    {
                        InvalidateClips();
                        if (_ambientName != "none") StartAmbientClip();
                        if (_alarmOn) StartAlarmClip();
                    }
                    catch { }
                }
            }
        }

        public static bool Muted
        {
            get
            {
                lock (Sync) { return _muted; }
            }
            set
            {
                lock (Sync)
                {
                    if (_muted == value) return;
                    _muted = value;
                    try
                    {
                        InvalidateClips();
                        if (!_muted)
                        {
                            if (_ambientName != "none") StartAmbientClip();
                            if (_alarmOn) StartAlarmClip();
                        }
                    }
                    catch { }
                }
            }
        }

        public static void PlaySfx(string name)
        {
            try
            {
                string key = NormalizeName(name);
                if (key == null || !IsSfxName(key)) return;
                lock (Sync)
                {
                    if (_muted || _volume <= 0f) return;
                    float[] samples = GetSfxSamples(key);
                    if (samples == null) return;
                    Clip clip = GetOrCreateClip("sfx:" + key, ToWav(samples, _volume, false));
                    if (clip == null) return;
                    clip.Player.Play();
                }
            }
            catch { }
        }

        public static void PlayAmbient(string name)
        {
            try
            {
                string key = NormalizeName(name);
                if (key == null) return;
                if (key == "none")
                {
                    StopAmbient();
                    return;
                }
                if (!IsAmbientName(key)) return;
                lock (Sync)
                {
                    _ambientName = key;
                    StopAmbientClipOnly();
                    if (_muted || _volume <= 0f) return;
                    StartAmbientClip();
                }
            }
            catch { }
        }

        public static void StopAmbient()
        {
            try
            {
                lock (Sync)
                {
                    _ambientName = "none";
                    StopAmbientClipOnly();
                }
            }
            catch { }
        }

        public static void PlayAlarm()
        {
            try
            {
                lock (Sync)
                {
                    _alarmOn = true;
                    if (_muted || _volume <= 0f) return;
                    StartAlarmClip();
                }
            }
            catch { }
        }

        public static void StopAlarm()
        {
            try
            {
                lock (Sync)
                {
                    _alarmOn = false;
                    StopClip("alarm");
                    if (_ambientName != "none" && !_muted && _volume > 0f) StartAmbientClip();
                }
            }
            catch { }
        }

        public static string[] SfxNames
        {
            get { return (string[])SfxList.Clone(); }
        }

        public static string[] AmbientNames
        {
            get { return (string[])AmbientList.Clone(); }
        }

        public static string AmbientDisplayName(string name)
        {
            try
            {
                string key = NormalizeName(name);
                if (key == null) return string.Empty;
                for (int i = 0; i < AmbientList.Length; i++)
                {
                    if (AmbientList[i] == key) return AmbientLabelList[i];
                }
                return name;
            }
            catch { return string.Empty; }
        }

        public static string AmbientNext(string current)
        {
            try
            {
                string key = NormalizeName(current);
                int idx = -1;
                for (int i = 0; i < AmbientList.Length; i++)
                {
                    if (AmbientList[i] == key) { idx = i; break; }
                }
                if (idx < 0) return "none";
                return AmbientList[(idx + 1) % AmbientList.Length];
            }
            catch { return "none"; }
        }

        public static void Shutdown()
        {
            try
            {
                lock (Sync)
                {
                    _alarmOn = false;
                    _ambientName = "none";
                    InvalidateClips();
                    SfxCache.Clear();
                    AmbientCache.Clear();
                }
            }
            catch { }
        }

        // ---------------- 播放辅助 ----------------

        private static string NormalizeName(string name)
        {
            if (name == null) return null;
            string s = name.Trim();
            if (s.Length == 0) return null;
            return s.ToLowerInvariant();
        }

        private static bool IsSfxName(string key)
        {
            for (int i = 0; i < SfxList.Length; i++)
            {
                if (SfxList[i] == key) return true;
            }
            return false;
        }

        private static bool IsAmbientName(string key)
        {
            for (int i = 0; i < AmbientList.Length; i++)
            {
                if (AmbientList[i] == key) return true;
            }
            return false;
        }

        private static void InvalidateClips()
        {
            if (Clips.Count == 0) return;
            List<Clip> old = new List<Clip>(Clips.Values);
            Clips.Clear();
            for (int i = 0; i < old.Count; i++)
            {
                try { old[i].Dispose(); } catch { }
            }
        }

        private static void StopClip(string key)
        {
            Clip c;
            if (Clips.TryGetValue(key, out c))
            {
                Clips.Remove(key);
                try { c.Dispose(); } catch { }
            }
        }

        private static void StopAmbientClipOnly()
        {
            string[] keys = new string[Clips.Keys.Count];
            Clips.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].StartsWith("amb:", StringComparison.Ordinal)) StopClip(keys[i]);
            }
        }

        private static Clip GetOrCreateClip(string key, byte[] wav)
        {
            if (wav == null) return null;
            Clip clip;
            if (Clips.TryGetValue(key, out clip)) return clip;
            try
            {
                MemoryStream ms = new MemoryStream(wav, false);
                SoundPlayer sp = new SoundPlayer(ms);
                sp.Load();
                clip = new Clip();
                clip.Player = sp;
                clip.Stream = ms;
                Clips[key] = clip;
                return clip;
            }
            catch
            {
                return null;
            }
        }

        private static void StartAmbientClip()
        {
            if (_ambientName == "none" || _muted || _volume <= 0f) return;
            float[] samples = GetAmbientSamples(_ambientName);
            if (samples == null) return;
            Clip clip = GetOrCreateClip("amb:" + _ambientName, ToWav(samples, _volume, false));
            if (clip == null) return;
            try { clip.Player.PlayLooping(); } catch { }
        }

        private static void StartAlarmClip()
        {
            if (_muted || _volume <= 0f) return;
            float[] samples = GetSfxSamples("alarm");
            if (samples == null) return;
            StopClip("alarm");
            Clip clip = GetOrCreateClip("alarm", ToWav(samples, _volume, false));
            if (clip == null) return;
            try { clip.Player.PlayLooping(); } catch { }
        }

        // ---------------- 采样缓存 / WAV 渲染 ----------------

        internal static float[] GetSfxSamples(string name)
        {
            string key = NormalizeName(name);
            if (key == null) return null;
            lock (Sync)
            {
                float[] cached;
                if (SfxCache.TryGetValue(key, out cached)) return cached;
                if (!IsSfxName(key)) return null;
                float[] built = BuildSfx(key);
                if (built == null) return null;
                SfxCache[key] = built;
                return built;
            }
        }

        internal static float[] GetAmbientSamples(string name)
        {
            string key = NormalizeName(name);
            if (key == null || key == "none") return null;
            lock (Sync)
            {
                float[] cached;
                if (AmbientCache.TryGetValue(key, out cached)) return cached;
                if (!IsAmbientName(key)) return null;
                float[] built = BuildAmbient(key);
                if (built == null) return null;
                AmbientCache[key] = built;
                return built;
            }
        }

        internal static byte[] RenderSfxWav(string name, float volume, bool muted)
        {
            float[] samples = GetSfxSamples(name);
            if (samples == null) return null;
            return ToWav(samples, volume, muted);
        }

        internal static byte[] RenderAmbientWav(string name, float volume, bool muted)
        {
            float[] samples = GetAmbientSamples(name);
            if (samples == null) return null;
            return ToWav(samples, volume, muted);
        }

        private static byte[] ToWav(float[] data, float volume, bool muted)
        {
            int n = data.Length;
            int dataBytes = n * 2;
            byte[] wav = new byte[44 + dataBytes];
            WriteAscii(wav, 0, "RIFF");
            WriteI32(wav, 4, 36 + dataBytes);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteI32(wav, 16, 16);
            WriteI16(wav, 20, 1);              // PCM
            WriteI16(wav, 22, Channels);
            WriteI32(wav, 24, SampleRate);
            WriteI32(wav, 28, SampleRate * Channels * BitsPerSample / 8);
            WriteI16(wav, 32, Channels * BitsPerSample / 8);
            WriteI16(wav, 34, BitsPerSample);
            WriteAscii(wav, 36, "data");
            WriteI32(wav, 40, dataBytes);

            double gain = muted ? 0.0 : (volume < 0f ? 0.0 : (volume > 1f ? 1.0 : volume));
            int p = 44;
            for (int i = 0; i < n; i++)
            {
                double v = data[i] * gain;
                if (v > 1.0) v = 1.0;
                else if (v < -1.0) v = -1.0;
                int q = (int)Math.Round(v * 32767.0);
                if (q > 32767) q = 32767;
                else if (q < -32767) q = -32767;
                wav[p] = (byte)(q & 0xFF);
                wav[p + 1] = (byte)((q >> 8) & 0xFF);
                p += 2;
            }
            return wav;
        }

        private static void WriteAscii(byte[] b, int off, string s)
        {
            for (int i = 0; i < s.Length; i++) b[off + i] = (byte)s[i];
        }

        private static void WriteI32(byte[] b, int off, int value)
        {
            b[off] = (byte)(value & 0xFF);
            b[off + 1] = (byte)((value >> 8) & 0xFF);
            b[off + 2] = (byte)((value >> 16) & 0xFF);
            b[off + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteI16(byte[] b, int off, int value)
        {
            b[off] = (byte)(value & 0xFF);
            b[off + 1] = (byte)((value >> 8) & 0xFF);
        }

        // ---------------- 合成基础工具 ----------------

        private sealed class Rng
        {
            private uint _state;
            public Rng(uint seed)
            {
                _state = seed == 0u ? 0x9E3779B9u : seed;
            }
            public uint NextUInt()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
            public float Unit()
            {
                return (NextUInt() / 4294967295f);
            }
            public float Bipolar()
            {
                return Unit() * 2f - 1f;
            }
            public int Range(int max)
            {
                if (max <= 1) return 0;
                return (int)(NextUInt() % (uint)max);
            }
            public double Range(double min, double max)
            {
                return min + (max - min) * (double)Unit();
            }
        }

        // 环形预热用的一阶滤波链：低通 + 高通
        private sealed class NoiseChain
        {
            private double _lpA;
            private double _lpY;
            private double _hpA;
            private double _hpX;
            private double _hpY;

            public void Setup(double lpHz, double hpHz)
            {
                if (lpHz > 0.0 && lpHz < SampleRate * 0.49)
                    _lpA = 1.0 - Math.Exp(-TwoPi * lpHz / SampleRate);
                else
                    _lpA = 1.0;
                if (hpHz > 0.0)
                    _hpA = 1.0 / (1.0 + TwoPi * hpHz / SampleRate);
                else
                    _hpA = 0.0;
                _lpY = 0.0;
                _hpX = 0.0;
                _hpY = 0.0;
            }

            public float Process(float x)
            {
                _lpY += _lpA * (x - _lpY);
                double y = _lpY;
                if (_hpA > 0.0)
                {
                    double outv = _hpA * (_hpY + y - _hpX);
                    _hpX = y;
                    _hpY = outv;
                    y = outv;
                }
                return (float)y;
            }
        }

        private static int Samples(double seconds)
        {
            int n = (int)Math.Round(seconds * SampleRate);
            if (n < 1) n = 1;
            return n;
        }

        private static float[] Buffer(double seconds)
        {
            return new float[Samples(seconds)];
        }

        private static double Env(double t, double attack, double tau)
        {
            double a = attack > 0.00005 ? 1.0 - Math.Exp(-t / attack) : 1.0;
            double d = tau > 0.00005 ? Math.Exp(-t / tau) : 1.0;
            return a * d;
        }

        // shape: 0=正弦 1=三角 2=软方波 3=锯齿感
        private static double Shape(int shape, double phase)
        {
            switch (shape)
            {
                case 1:
                    return Math.Asin(Math.Sin(phase)) * (2.0 / Pi);
                case 2:
                    return Math.Tanh(2.6 * Math.Sin(phase));
                case 3:
                    return 0.62 * Math.Sin(phase) + 0.24 * Math.Sin(2.0 * phase) + 0.14 * Math.Sin(3.0 * phase);
                default:
                    return Math.Sin(phase);
            }
        }

        private static void AddTone(float[] buf, double startSec, double durSec, double f0, double f1,
                                    float amp, double attackSec, double tauSec, int shape,
                                    double vibHz, double vibDepth)
        {
            int i0 = (int)Math.Round(startSec * SampleRate);
            if (i0 < 0) i0 = 0;
            if (i0 >= buf.Length) return;
            int n = Samples(durSec);
            if (i0 + n > buf.Length) n = buf.Length - i0;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double freq;
                if (f1 > 0.0 && f0 > 0.0 && Math.Abs(f1 - f0) > 0.0001)
                    freq = f0 * Math.Pow(f1 / f0, t / durSec);
                else
                    freq = f0;
                if (vibHz > 0.0 && vibDepth > 0.0)
                    freq *= 1.0 + vibDepth * Math.Sin(TwoPi * vibHz * t);
                phase += TwoPi * freq / SampleRate;
                double env = Env(t, attackSec, tauSec);
                buf[i0 + i] += (float)(amp * env * Shape(shape, phase));
            }
        }

        private static float[] NoiseBurst(double seconds, uint seed, float amp,
                                          double attackSec, double tauSec, double lpHz, double hpHz)
        {
            int n = Samples(seconds);
            Rng rng = new Rng(seed);
            NoiseChain chain = new NoiseChain();
            chain.Setup(lpHz, hpHz);
            float[] res = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double env = Env(t, attackSec, tauSec);
                res[i] = chain.Process(rng.Bipolar()) * (float)(amp * env);
            }
            return res;
        }

        private static void MixWrap(float[] dst, int start, float[] ev)
        {
            int n = dst.Length;
            if (n <= 0 || ev == null || ev.Length == 0) return;
            start = start % n;
            if (start < 0) start += n;
            for (int i = 0; i < ev.Length; i++)
            {
                int idx = (start + i) % n;
                dst[idx] += ev[i];
            }
        }

        private static void AddWrappedTone(float[] buf, int start, double freq, double durSec,
                                           float amp, double attackSec, double tauSec)
        {
            int n = buf.Length;
            int len = Samples(durSec);
            if (len > n) len = n;
            start = start % n;
            if (start < 0) start += n;
            double phase = 0.0;
            for (int i = 0; i < len; i++)
            {
                double t = i / (double)SampleRate;
                double env = Env(t, attackSec, tauSec);
                phase += TwoPi * freq / SampleRate;
                buf[(start + i) % n] += (float)(amp * env * Math.Sin(phase));
            }
        }

        private static void AddWrappedSweep(float[] buf, int start, double f0, double f1, double durSec,
                                            float amp, double attackSec, double tauSec)
        {
            int n = buf.Length;
            int len = Samples(durSec);
            if (len > n) len = n;
            start = start % n;
            if (start < 0) start += n;
            double phase = 0.0;
            for (int i = 0; i < len; i++)
            {
                double t = i / (double)SampleRate;
                double freq = f0 * Math.Pow(f1 / f0, t / durSec);
                double env = Env(t, attackSec, tauSec);
                phase += TwoPi * freq / SampleRate;
                buf[(start + i) % n] += (float)(amp * env * Math.Sin(phase));
            }
        }

        private static void Normalize(float[] buf, float target)
        {
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = buf[i] < 0f ? -buf[i] : buf[i];
                if (a > peak) peak = a;
            }
            if (peak < 0.0000001f) return;
            float g = target / peak;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        private static void FadeEdges(float[] buf, double fadeInSec, double fadeOutSec)
        {
            int n = buf.Length;
            int fi = Samples(fadeInSec);
            if (fi > n / 2) fi = n / 2;
            for (int i = 0; i < fi; i++)
            {
                float g = (float)(0.5 - 0.5 * Math.Cos(Pi * i / (double)fi));
                buf[i] *= g;
            }
            int fo = Samples(fadeOutSec);
            if (fo > n / 2) fo = n / 2;
            for (int i = 0; i < fo; i++)
            {
                float g = (float)(0.5 - 0.5 * Math.Cos(Pi * i / (double)fo));
                buf[n - 1 - i] *= g;
            }
        }

        private static void ReleaseTail(float[] buf, double seconds)
        {
            int n = buf.Length;
            int len = Samples(seconds);
            if (len > n) len = n;
            for (int i = 0; i < len; i++)
            {
                float g = (float)(0.5 + 0.5 * Math.Cos(Pi * i / (double)len));
                buf[n - 1 - i] *= g;
            }
        }

        // ---------------- 乐器音色 ----------------

        // 马林巴：基音 + 4 次/10 次非谐泛音，短促指数衰减
        private static void Marimba(float[] buf, double startSec, double freq, float amp, double tau)
        {
            int i0 = (int)Math.Round(startSec * SampleRate);
            if (i0 < 0) i0 = 0;
            if (i0 >= buf.Length) return;
            int n = Samples(tau * 5.0 + 0.05);
            if (i0 + n > buf.Length) n = buf.Length - i0;
            double w = TwoPi * freq;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double env = (1.0 - Math.Exp(-t / 0.0025)) * Math.Exp(-t / tau);
                double s = Math.Sin(w * t);
                s += 0.22 * Math.Exp(-t / 0.045) * Math.Sin(w * 3.93 * t);
                s += 0.06 * Math.Exp(-t / 0.020) * Math.Sin(w * 9.10 * t);
                buf[i0 + i] += (float)(amp * env * s);
            }
        }

        // FM 钟声：调制指数指数衰减，带一个柔和的 2.01 倍分音
        private static void Bell(float[] buf, double startSec, double freq, float amp, double tau)
        {
            int i0 = (int)Math.Round(startSec * SampleRate);
            if (i0 < 0) i0 = 0;
            if (i0 >= buf.Length) return;
            int n = Samples(tau * 6.0 + 0.08);
            if (i0 + n > buf.Length) n = buf.Length - i0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double index = 3.2 * Math.Exp(-t / (tau * 0.25));
                double s = Math.Sin(TwoPi * freq * t + index * Math.Sin(TwoPi * freq * 3.0 * t));
                s += 0.30 * Math.Exp(-t / (tau * 0.5)) * Math.Sin(TwoPi * freq * 2.01 * t);
                double env = (1.0 - Math.Exp(-t / 0.003)) * Math.Exp(-t / tau);
                buf[i0 + i] += (float)(amp * env * s);
            }
        }

        // ---------------- 音效合成 ----------------

        private static float[] BuildSfx(string name)
        {
            switch (name)
            {
                case "click": return SfxClick();
                case "headpat": return SfxHeadpat();
                case "feed": return SfxFeed();
                case "eat": return SfxEat();
                case "happy": return SfxHappy();
                case "sad": return SfxSad();
                case "surprised": return SfxSurprised();
                case "pop": return SfxPop();
                case "gacha": return SfxGacha();
                case "coin": return SfxCoin();
                case "pomodoro_start": return SfxPomodoroStart();
                case "pomodoro_end": return SfxPomodoroEnd();
                case "alarm": return SfxAlarm();
                case "sleep": return SfxSleep();
                case "snore": return SfxSnore();
                case "hiccup": return SfxHiccup();
                case "bubble": return SfxBubble();
                case "dizzy": return SfxDizzy();
                case "levelup": return SfxLevelup();
                case "error": return SfxError();
                case "charge": return SfxCharge();
                case "lowbattery": return SfxLowBattery();
                case "notify": return SfxNotify();
                default: return null;
            }
        }

        private static float[] SfxClick()
        {
            float[] buf = Buffer(0.07);
            float[] n = NoiseBurst(0.03, 0x1001u, 0.55f, 0.0008, 0.006, 4200, 700);
            for (int i = 0; i < n.Length && i < buf.Length; i++) buf[i] += n[i];
            AddTone(buf, 0.0, 0.045, 1250.0, 900.0, 0.42f, 0.0006, 0.012, 0, 0.0, 0.0);
            AddTone(buf, 0.0, 0.020, 2450.0, 2200.0, 0.15f, 0.0005, 0.006, 0, 0.0, 0.0);
            Normalize(buf, 0.68f);
            FadeEdges(buf, 0.001, 0.008);
            return buf;
        }

        private static float[] SfxHeadpat()
        {
            float[] buf = Buffer(0.62);
            Marimba(buf, 0.00, 523.25, 0.55f, 0.22);
            Marimba(buf, 0.13, 659.25, 0.50f, 0.22);
            Marimba(buf, 0.26, 783.99, 0.46f, 0.25);
            Marimba(buf, 0.26, 1046.50, 0.12f, 0.18);
            Normalize(buf, 0.80f);
            FadeEdges(buf, 0.002, 0.02);
            return buf;
        }

        private static float[] SfxFeed()
        {
            float[] buf = Buffer(0.34);
            float[] n = NoiseBurst(0.22, 0x2002u, 0.60f, 0.010, 0.070, 900, 70);
            for (int i = 0; i < n.Length && i < buf.Length; i++) buf[i] += n[i];
            AddTone(buf, 0.0, 0.20, 120.0, 70.0, 0.45f, 0.006, 0.075, 0, 0.0, 0.0);
            AddTone(buf, 0.03, 0.10, 320.0, 240.0, 0.10f, 0.004, 0.040, 0, 0.0, 0.0);
            Normalize(buf, 0.76f);
            FadeEdges(buf, 0.002, 0.02);
            return buf;
        }

        private static float[] SfxEat()
        {
            float[] buf = Buffer(0.58);
            uint seed = 0x3003u;
            for (int k = 0; k < 3; k++)
            {
                double t0 = k * 0.15;
                float[] n = NoiseBurst(0.12, seed + (uint)k * 7919u, 0.55f, 0.004, 0.045, 1200, 90);
                int off = (int)Math.Round(t0 * SampleRate);
                for (int i = 0; i < n.Length && off + i < buf.Length; i++) buf[off + i] += n[i];
                AddTone(buf, t0, 0.10, 150.0, 105.0, 0.35f, 0.002, 0.040, 0, 0.0, 0.0);
            }
            Normalize(buf, 0.78f);
            FadeEdges(buf, 0.002, 0.02);
            return buf;
        }

        private static float[] SfxHappy()
        {
            float[] buf = Buffer(0.82);
            Marimba(buf, 0.00, 523.25, 0.52f, 0.24);
            Marimba(buf, 0.10, 659.25, 0.50f, 0.24);
            Marimba(buf, 0.20, 783.99, 0.48f, 0.26);
            Marimba(buf, 0.30, 1046.50, 0.44f, 0.30);
            Marimba(buf, 0.30, 1318.51, 0.10f, 0.20);
            Normalize(buf, 0.82f);
            FadeEdges(buf, 0.002, 0.02);
            return buf;
        }

        private static float[] SfxSad()
        {
            float[] buf = Buffer(0.92);
            AddTone(buf, 0.00, 0.50, 440.00, 415.00, 0.34f, 0.012, 0.30, 1, 4.5, 0.010);
            AddTone(buf, 0.00, 0.50, 220.00, 207.00, 0.12f, 0.012, 0.30, 0, 0.0, 0.0);
            AddTone(buf, 0.26, 0.62, 349.23, 330.00, 0.30f, 0.015, 0.34, 1, 4.0, 0.012);
            AddTone(buf, 0.26, 0.62, 174.61, 165.00, 0.10f, 0.015, 0.34, 0, 0.0, 0.0);
            Normalize(buf, 0.62f);
            FadeEdges(buf, 0.003, 0.03);
            return buf;
        }

        private static float[] SfxSurprised()
        {
            float[] buf = Buffer(0.50);
            AddTone(buf, 0.00, 0.13, 380.0, 1000.0, 0.46f, 0.003, 0.070, 0, 0.0, 0.0);
            Bell(buf, 0.07, 1568.0, 0.30f, 0.18);
            AddTone(buf, 0.00, 0.06, 2100.0, 2600.0, 0.08f, 0.001, 0.020, 0, 0.0, 0.0);
            Normalize(buf, 0.78f);
            FadeEdges(buf, 0.001, 0.02);
            return buf;
        }

        private static float[] SfxPop()
        {
            float[] buf = Buffer(0.22);
            AddTone(buf, 0.00, 0.10, 290.0, 950.0, 0.62f, 0.001, 0.045, 0, 0.0, 0.0);
            float[] n = NoiseBurst(0.02, 0x4004u, 0.25f, 0.0005, 0.006, 5000, 800);
            for (int i = 0; i < n.Length && i < buf.Length; i++) buf[i] += n[i];
            Normalize(buf, 0.80f);
            FadeEdges(buf, 0.001, 0.015);
            return buf;
        }

        private static float[] SfxGacha()
        {
            float[] buf = Buffer(1.05);
            Marimba(buf, 0.00, 783.99, 0.42f, 0.22);
            Marimba(buf, 0.10, 987.77, 0.42f, 0.22);
            Marimba(buf, 0.20, 1174.66, 0.42f, 0.24);
            Marimba(buf, 0.30, 1567.98, 0.40f, 0.30);
            Bell(buf, 0.44, 2093.00, 0.18f, 0.32);
            AddTone(buf, 0.44, 0.40, 3136.0, 3136.0, 0.06f, 0.002, 0.18, 0, 7.0, 0.010);
            Normalize(buf, 0.85f);
            FadeEdges(buf, 0.002, 0.03);
            return buf;
        }

        private static float[] SfxCoin()
        {
            float[] buf = Buffer(0.40);
            Marimba(buf, 0.00, 987.77, 0.55f, 0.13);
            Marimba(buf, 0.09, 1318.51, 0.50f, 0.24);
            Normalize(buf, 0.80f);
            FadeEdges(buf, 0.001, 0.02);
            return buf;
        }

        private static float[] SfxPomodoroStart()
        {
            float[] buf = Buffer(0.72);
            Bell(buf, 0.00, 523.25, 0.40f, 0.22);
            Bell(buf, 0.12, 659.25, 0.40f, 0.24);
            Bell(buf, 0.24, 783.99, 0.42f, 0.30);
            Marimba(buf, 0.24, 1046.50, 0.16f, 0.18);
            Normalize(buf, 0.78f);
            FadeEdges(buf, 0.002, 0.03);
            return buf;
        }

        private static float[] SfxPomodoroEnd()
        {
            float[] buf = Buffer(1.40);
            Bell(buf, 0.00, 523.25, 0.62f, 0.55);
            Bell(buf, 0.05, 783.99, 0.26f, 0.45);
            AddTone(buf, 0.00, 1.30, 261.63, 261.63, 0.18f, 0.010, 0.55, 0, 0.0, 0.0);
            Normalize(buf, 0.72f);
            FadeEdges(buf, 0.003, 0.05);
            return buf;
        }

        private static float[] SfxAlarm()
        {
            float[] buf = Buffer(2.00);
            Bell(buf, 0.00, 1046.50, 0.55f, 0.15);
            Bell(buf, 0.18, 1318.51, 0.50f, 0.14);
            Bell(buf, 1.00, 1046.50, 0.55f, 0.15);
            Bell(buf, 1.18, 1318.51, 0.50f, 0.14);
            Normalize(buf, 0.70f);
            FadeEdges(buf, 0.008, 0.008);
            return buf;
        }

        private static float[] SfxSleep()
        {
            float[] buf = Buffer(1.30);
            AddTone(buf, 0.00, 1.25, 175.0, 168.0, 0.34f, 0.22, 0.0, 0, 4.5, 0.008);
            AddTone(buf, 0.00, 1.25, 131.0, 126.0, 0.16f, 0.25, 0.0, 0, 4.0, 0.006);
            float[] n = NoiseBurst(0.55, 0x5005u, 0.055f, 0.18, 0.0, 420, 70);
            AddTone(buf, 0.00, 0.80, 262.0, 250.0, 0.035f, 0.25, 0.0, 1, 3.5, 0.004);
            for (int i = 0; i < n.Length && i + 1000 < buf.Length; i++) buf[1000 + i] += n[i];
            ReleaseTail(buf, 0.28);
            Normalize(buf, 0.42f);
            FadeEdges(buf, 0.01, 0.02);
            return buf;
        }

        private static float[] SfxSnore()
        {
            float[] buf = Buffer(1.20);
            int n = buf.Length;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double f = 108.0 - 24.0 * (t / 1.2);
                phase += TwoPi * f / SampleRate;
                double tri = Math.Asin(Math.Sin(phase)) * (2.0 / Pi);
                double am = 0.25 + 0.75 * Math.Pow(0.5 + 0.5 * Math.Sin(TwoPi * 2.1 * t - 1.1), 1.6);
                double env = 1.0 - Math.Exp(-t / 0.09);
                buf[i] = (float)(tri * am * env * 0.42);
            }
            float[] hiss = NoiseBurst(0.45, 0x6006u, 0.16f, 0.12, 0.0, 700, 120);
            for (int i = 0; i < hiss.Length && i + (int)(0.70 * SampleRate) < n; i++)
                buf[(int)(0.70 * SampleRate) + i] += hiss[i] * (float)(0.55 + 0.45 * Math.Sin(TwoPi * 2.1 * i / SampleRate));
            ReleaseTail(buf, 0.22);
            Normalize(buf, 0.50f);
            FadeEdges(buf, 0.01, 0.02);
            return buf;
        }

        private static float[] SfxHiccup()
        {
            float[] buf = Buffer(0.28);
            AddTone(buf, 0.00, 0.14, 260.0, 620.0, 0.55f, 0.003, 0.060, 1, 0.0, 0.0);
            AddTone(buf, 0.00, 0.10, 520.0, 1240.0, 0.15f, 0.003, 0.040, 0, 0.0, 0.0);
            Normalize(buf, 0.72f);
            FadeEdges(buf, 0.001, 0.02);
            return buf;
        }

        private static float[] SfxBubble()
        {
            float[] buf = Buffer(0.25);
            AddTone(buf, 0.00, 0.10, 300.0, 900.0, 0.58f, 0.001, 0.045, 0, 0.0, 0.0);
            float[] n = NoiseBurst(0.02, 0x7007u, 0.18f, 0.0005, 0.005, 6000, 1200);
            for (int i = 0; i < n.Length && i < buf.Length; i++) buf[i] += n[i];
            AddTone(buf, 0.10, 0.10, 520.0, 1150.0, 0.18f, 0.001, 0.035, 0, 0.0, 0.0);
            Normalize(buf, 0.78f);
            FadeEdges(buf, 0.001, 0.02);
            return buf;
        }

        private static float[] SfxDizzy()
        {
            float[] buf = Buffer(1.00);
            int n = buf.Length;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double f = 520.0 - 190.0 * (t / 1.0);
                double vib = 1.0 + 0.035 * Math.Sin(TwoPi * 6.0 * t);
                phase += TwoPi * f * vib / SampleRate;
                double tri = Math.Asin(Math.Sin(phase)) * (2.0 / Pi);
                double am = 0.55 + 0.45 * Math.Sin(TwoPi * 5.0 * t + 0.8);
                double env = 1.0 - Math.Exp(-t / 0.05);
                buf[i] = (float)(tri * am * env * 0.30);
            }
            AddTone(buf, 0.0, 0.9, 260.0, 210.0, 0.08f, 0.05, 0.0, 1, 5.0, 0.02);
            ReleaseTail(buf, 0.20);
            Normalize(buf, 0.55f);
            FadeEdges(buf, 0.005, 0.02);
            return buf;
        }

        private static float[] SfxLevelup()
        {
            float[] buf = Buffer(1.25);
            Marimba(buf, 0.00, 523.25, 0.46f, 0.26);
            Marimba(buf, 0.10, 659.25, 0.46f, 0.26);
            Marimba(buf, 0.20, 783.99, 0.46f, 0.28);
            Marimba(buf, 0.30, 1046.50, 0.46f, 0.32);
            Bell(buf, 0.42, 1567.98, 0.30f, 0.42);
            Bell(buf, 0.42, 2093.00, 0.12f, 0.35);
            Normalize(buf, 0.85f);
            FadeEdges(buf, 0.002, 0.04);
            return buf;
        }

        private static float[] SfxError()
        {
            float[] buf = Buffer(0.58);
            AddTone(buf, 0.00, 0.24, 392.00, 392.00, 0.40f, 0.006, 0.16, 1, 0.0, 0.0);
            AddTone(buf, 0.00, 0.24, 196.00, 196.00, 0.12f, 0.006, 0.16, 0, 0.0, 0.0);
            AddTone(buf, 0.20, 0.34, 261.63, 261.63, 0.38f, 0.008, 0.22, 1, 0.0, 0.0);
            AddTone(buf, 0.20, 0.34, 130.81, 130.81, 0.10f, 0.008, 0.22, 0, 0.0, 0.0);
            Normalize(buf, 0.60f);
            FadeEdges(buf, 0.003, 0.03);
            return buf;
        }

        private static float[] SfxCharge()
        {
            float[] buf = Buffer(1.20);
            int n = buf.Length;
            double phase = 0.0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SampleRate;
                double x = t / 1.15;
                if (x > 1.0) x = 1.0;
                double env = Math.Pow(x, 1.7);
                double freq = 180.0 * Math.Pow(1400.0 / 180.0, x);
                phase += TwoPi * freq / SampleRate;
                double s = Math.Sin(phase) + 0.30 * Math.Sin(2.0 * phase) + 0.10 * Math.Sin(3.0 * phase);
                buf[i] = (float)(env * s * 0.45);
            }
            ReleaseTail(buf, 0.10);
            Normalize(buf, 0.70f);
            FadeEdges(buf, 0.005, 0.02);
            return buf;
        }

        private static float[] SfxLowBattery()
        {
            float[] buf = Buffer(0.78);
            AddTone(buf, 0.00, 0.28, 880.0, 860.0, 0.42f, 0.004, 0.18, 1, 0.0, 0.0);
            AddTone(buf, 0.00, 0.28, 440.0, 430.0, 0.12f, 0.004, 0.18, 0, 0.0, 0.0);
            AddTone(buf, 0.24, 0.36, 659.25, 640.0, 0.42f, 0.005, 0.24, 1, 4.0, 0.004);
            AddTone(buf, 0.24, 0.36, 329.63, 320.0, 0.10f, 0.005, 0.24, 0, 0.0, 0.0);
            Normalize(buf, 0.58f);
            FadeEdges(buf, 0.004, 0.04);
            return buf;
        }

        private static float[] SfxNotify()
        {
            float[] buf = Buffer(0.55);
            Bell(buf, 0.00, 1318.51, 0.44f, 0.20);
            Bell(buf, 0.10, 1975.53, 0.38f, 0.28);
            Normalize(buf, 0.75f);
            FadeEdges(buf, 0.001, 0.03);
            return buf;
        }

        // ---------------- 环境音合成（无缝循环） ----------------

        private static float[] BuildAmbient(string name)
        {
            switch (name)
            {
                case "rain": return AmbientRain();
                case "waves": return AmbientWaves();
                case "fire": return AmbientFire();
                case "forest": return AmbientForest();
                case "night": return AmbientNight();
                case "white": return AmbientWhite();
                case "keyboard": return AmbientKeyboard();
                default: return null;
            }
        }

        // 生成“周期化”的滤波噪声：先把信号末尾当作前置样本喂给滤波器（环形预热），
        // 再从头处理一遍；这样循环接缝处的滤波器状态与真实周期信号一致。
        private static float[] FilteredNoise(double seconds, uint seed, double lpHz, double hpHz)
        {
            int n = Samples(seconds);
            Rng rng = new Rng(seed);
            float[] raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = rng.Bipolar();
            NoiseChain chain = new NoiseChain();
            chain.Setup(lpHz, hpHz);
            int warm = n < SampleRate ? n : SampleRate;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = n - warm; i < n; i++) chain.Process(raw[i]);
            }
            float[] dst = new float[n];
            for (int i = 0; i < n; i++) dst[i] = chain.Process(raw[i]);
            return dst;
        }

        private static float[] AmbientRain()
        {
            double len = 8.0;
            int n = Samples(len);
            float[] body = FilteredNoise(len, 0x1A2B3C4Du, 3000.0, 300.0);
            float[] hiss = FilteredNoise(len, 0x5E6F7A8Bu, 7000.0, 1200.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++)
            {
                // 0.125Hz（8s 正好 1 个周期）
                double lfo = 0.82 + 0.18 * Math.Sin(TwoPi * i / n);
                mix[i] = (float)((body[i] * 0.85 + hiss[i] * 0.30) * lfo);
            }
            Rng rng = new Rng(0x11223344u);
            for (int d = 0; d < 110; d++)
            {
                int pos = rng.Range(n);
                double f = rng.Range(1200.0, 3800.0);
                float amp = (float)rng.Range(0.018, 0.070);
                float[] ev = Buffer(0.06);
                AddTone(ev, 0.0, 0.055, f, f * 0.88, amp, 0.001, 0.012, 0, 0.0, 0.0);
                float[] tick = NoiseBurst(0.008, 0x778899AAu + (uint)d * 131u, amp * 0.6f, 0.0004, 0.002, 6000.0, 1800.0);
                for (int i = 0; i < tick.Length && i < ev.Length; i++) ev[i] += tick[i];
                FadeEdges(ev, 0.0004, 0.004);
                MixWrap(mix, pos, ev);
            }
            Normalize(mix, 0.40f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientWaves()
        {
            double len = 10.0;
            int n = Samples(len);
            float[] body = FilteredNoise(len, 0x2B3C4D5Eu, 480.0, 45.0);
            float[] foam = FilteredNoise(len, 0x3C4D5E6Fu, 2600.0, 700.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++)
            {
                // 0.1Hz 主涌浪（10s 整 1 个周期），再叠一点 0.2Hz 细浪
                double ph = TwoPi * i / n;
                double swell = 0.5 - 0.5 * Math.Cos(ph);
                double e = Math.Pow(swell, 1.35);
                double ripple = 0.5 - 0.5 * Math.Cos(2.0 * ph);
                mix[i] = (float)(body[i] * (0.10 + 0.90 * e) * 0.80
                               + foam[i] * (0.05 + 0.95 * e) * 0.32
                               + foam[i] * ripple * 0.05);
            }
            Normalize(mix, 0.48f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientFire()
        {
            double len = 9.0;
            int n = Samples(len);
            float[] rumble = FilteredNoise(len, 0x4D5E6F70u, 220.0, 35.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++)
            {
                // 0.222Hz（9s 整 2 个周期）
                double lfo = 0.72 + 0.28 * Math.Sin(TwoPi * 2.0 * i / n + 0.7);
                mix[i] = (float)(rumble[i] * lfo * 0.55);
            }
            Rng rng = new Rng(0x55667788u);
            for (int k = 0; k < 52; k++)
            {
                int pos = rng.Range(n);
                if (rng.Unit() < 0.28f)
                {
                    float[] pop = Buffer(0.05);
                    AddTone(pop, 0.0, 0.035, rng.Range(190.0, 520.0), rng.Range(140.0, 380.0),
                            (float)rng.Range(0.05, 0.16), 0.001, 0.012, 0, 0.0, 0.0);
                    FadeEdges(pop, 0.0005, 0.004);
                    MixWrap(mix, pos, pop);
                }
                else
                {
                    float amp = (float)rng.Range(0.05, 0.26);
                    float[] ev = NoiseBurst(rng.Range(0.03, 0.11), 0x66778899u + (uint)k * 3571u,
                                            amp, 0.001, rng.Range(0.010, 0.040), rng.Range(2600.0, 6500.0), rng.Range(500.0, 1400.0));
                    FadeEdges(ev, 0.0005, 0.004);
                    MixWrap(mix, pos, ev);
                }
            }
            Normalize(mix, 0.45f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientForest()
        {
            double len = 11.0;
            int n = Samples(len);
            float[] wind = FilteredNoise(len, 0x6F7A8B9Cu, 700.0, 70.0);
            float[] leaves = FilteredNoise(len, 0x7A8B9CADu, 3000.0, 900.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++)
            {
                // 0.0909Hz（11s 整 1 个周期）
                double lfo = 0.62 + 0.38 * Math.Sin(TwoPi * i / n);
                double ph = TwoPi * i / n;
                mix[i] = (float)(wind[i] * lfo * 0.55 + leaves[i] * (0.10 + 0.04 * Math.Sin(2.0 * ph)) * 0.22);
            }
            Rng rng = new Rng(0x8899AABBu);
            for (int g = 0; g < 7; g++)
            {
                int pos = rng.Range(n);
                int notes = 2 + rng.Range(3);
                double offset = 0.0;
                for (int k = 0; k < notes; k++)
                {
                    double f0 = rng.Range(2100.0, 3200.0);
                    double f1 = rng.Unit() < 0.6f ? f0 * rng.Range(1.15, 1.55) : f0 * rng.Range(0.62, 0.85);
                    float amp = (float)rng.Range(0.07, 0.16);
                    int at = pos + (int)Math.Round(offset * SampleRate) + (int)Math.Round((rng.Unit() * 0.02) * SampleRate);
                    AddWrappedSweep(mix, at, f0, f1, rng.Range(0.055, 0.11), amp, 0.004, 0.030);
                    offset += rng.Range(0.085, 0.17);
                }
            }
            Normalize(mix, 0.40f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientNight()
        {
            double len = 10.0;
            int n = Samples(len);
            float[] floor = FilteredNoise(len, 0x99AABBCCu, 350.0, 55.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++) mix[i] = floor[i] * 0.07f;
            Rng rng = new Rng(0xAABBCCDDu);
            double[] voices = new double[] { 4150.0, 4580.0, 4960.0, 4380.0 };
            for (int v = 0; v < voices.Length; v++)
            {
                int groups = 2 + rng.Range(2);
                for (int g = 0; g < groups; g++)
                {
                    int pos = rng.Range(n);
                    int pulses = 3 + rng.Range(3);
                    for (int p = 0; p < pulses; p++)
                    {
                        int at = pos + (int)Math.Round((p * rng.Range(0.028, 0.040)) * SampleRate);
                        AddWrappedTone(mix, at, voices[v] + rng.Range(-40.0, 40.0), 0.022,
                                       (float)rng.Range(0.08, 0.13), 0.001, 0.008);
                    }
                }
            }
            Normalize(mix, 0.38f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientWhite()
        {
            double len = 7.0;
            int n = Samples(len);
            float[] noise = FilteredNoise(len, 0xBBCCDDEEu, 9000.0, 60.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++) mix[i] = noise[i];
            Normalize(mix, 0.25f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }

        private static float[] AmbientKeyboard()
        {
            double len = 8.0;
            int n = Samples(len);
            float[] room = FilteredNoise(len, 0xCCDDEEFFu, 420.0, 60.0);
            float[] mix = new float[n];
            for (int i = 0; i < n; i++) mix[i] = room[i] * 0.10f;
            Rng rng = new Rng(0xDDEEFF00u);
            for (int k = 0; k < 26; k++)
            {
                int pos = rng.Range(n);
                float amp = (float)rng.Range(0.16, 0.34);
                float[] ev = Buffer(0.07);
                float[] tick = NoiseBurst(0.020, 0xEEFF0011u + (uint)k * 2657u, amp, 0.0004, 0.004, 7500.0, 1400.0);
                for (int i = 0; i < tick.Length && i < ev.Length; i++) ev[i] += tick[i];
                AddTone(ev, 0.0, 0.05, rng.Range(120.0, 180.0), rng.Range(80.0, 120.0), amp * 0.45f, 0.001, 0.018, 0, 0.0, 0.0);
                FadeEdges(ev, 0.0005, 0.004);
                MixWrap(mix, pos, ev);
            }
            Normalize(mix, 0.50f);
            FadeEdges(mix, 0.008, 0.008);
            return mix;
        }
    }
}
