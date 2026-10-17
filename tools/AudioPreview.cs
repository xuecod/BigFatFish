// AudioPreview.cs —— 大肥鱼桌宠音频自检
// 编译（与 Audio.cs / Contracts.cs 一起，/target:exe）后运行：
//   1) 导出全部音效与环境音到 preview\audio\*.wav
//   2) 校验 RIFF 头、采样率、时长、峰值、RMS、削波
//   3) 校验环境音循环接缝（首末样本差、首末 50ms 能量差）
//   4) 校验音量线性 / 静音 / 全零
// 退出码 0 = 全部通过。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using FishPet;

internal static class AudioPreview
{
    private const string Root = @"D:\其他\deepseek\大肥鱼";
    private const string OutDir = Root + @"\preview\audio";
    private const float ExportVolume = 0.8f;

    private static int _failures = 0;
    private static readonly StringBuilder Log = new StringBuilder();

    private sealed class WavInfo
    {
        public int Rate;
        public int Channels;
        public int Bits;
        public int Format;
        public int DataBytes;
        public int SampleCount;
        public short[] Samples;
        public double Peak;
        public double Rms;
        public int Clipped;
        public double DurationMs;
    }

    private static void Main()
    {
        try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
        Console.WriteLine("== 大肥鱼桌宠 Audio 自检 ==");
        try
        {
            Directory.CreateDirectory(OutDir);
            Audio.Init(Root + @"\assets");
            Audio.Volume = 0.8f;
        }
        catch (Exception ex)
        {
            Fail("Init/Volume 抛出异常: " + ex.Message);
        }

        string[] sfx = Audio.SfxNames;
        string[] ambient = Audio.AmbientNames;

        CheckNames(sfx, ambient);

        Console.WriteLine();
        Console.WriteLine("-- 音效导出 --");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,-16} {1,8} {2,8} {3,8} {4,6}  {5}", "name", "ms", "peak", "rms", "clip", "bytes"));
        for (int i = 0; i < sfx.Length; i++)
        {
            string name = sfx[i];
            string path = Path.Combine(OutDir, name + ".wav");
            try
            {
                byte[] wav = Audio.RenderSfxWav(name, ExportVolume, false);
                if (wav == null)
                {
                    Fail(name + ": RenderSfxWav 返回 null");
                    continue;
                }
                File.WriteAllBytes(path, wav);
                WavInfo info = ParseWav(path);
                PrintStat(name, info, wav.Length);
                CheckWavHeader(name, info, wav.Length);
                double maxMs = name == "alarm" ? 2000.0 : 1500.0;
                if (info.DurationMs < 30.0 || info.DurationMs > maxMs + 0.5)
                    Fail(name + ": 时长超限 " + F(info.DurationMs) + "ms");
                CheckSignal(name, info);
            }
            catch (Exception ex)
            {
                Fail(name + ": " + ex.Message);
            }
        }

        Console.WriteLine();
        Console.WriteLine("-- 环境音导出 --");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,-16} {1,8} {2,8} {3,8} {4,6}  {5}", "name", "ms", "peak", "rms", "clip", "bytes"));
        int ambientCount = 0;
        for (int i = 1; i < ambient.Length; i++)
        {
            string name = ambient[i];
            string path = Path.Combine(OutDir, name + ".wav");
            try
            {
                byte[] wav = Audio.RenderAmbientWav(name, ExportVolume, false);
                if (wav == null)
                {
                    Fail(name + ": RenderAmbientWav 返回 null");
                    continue;
                }
                File.WriteAllBytes(path, wav);
                ambientCount++;
                WavInfo info = ParseWav(path);
                PrintStat(name, info, wav.Length);
                CheckWavHeader(name, info, wav.Length);
                if (info.DurationMs < 6000.0 || info.DurationMs > 15000.0)
                    Fail(name + ": 环境音时长应在 6~15s，实际 " + F(info.DurationMs) + "ms");
                CheckSignal(name, info);
            }
            catch (Exception ex)
            {
                Fail(name + ": " + ex.Message);
            }
        }

        Console.WriteLine();
        Console.WriteLine("-- 环境音循环接缝 --");
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,-16} {1,10} {2,12} {3,14} {4,10}", "name", "first-last", "50ms E1", "50ms E2", "E rel diff"));
        for (int i = 1; i < ambient.Length; i++)
        {
            string name = ambient[i];
            string path = Path.Combine(OutDir, name + ".wav");
            try
            {
                WavInfo info = ParseWav(path);
                int win = (int)Math.Round(0.05 * Audio.SampleRate);
                double e1 = Energy(info, 0, win);
                double e2 = Energy(info, info.SampleCount - win, win);
                int diff = Math.Abs((int)info.Samples[0] - (int)info.Samples[info.SampleCount - 1]);
                double rel = Math.Abs(e1 - e2) / Math.Max(Math.Max(e1, e2), 1e-12);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-16} {1,10} {2,12} {3,14} {4,10}",
                    name, diff, F6(e1), F6(e2), F4(rel)));
                if (diff > 8)
                    Fail(name + ": 循环首末样本差 " + diff + " 过大");
                if (rel > 0.15)
                    Fail(name + ": 首末 50ms 能量相对差 " + F4(rel) + " 过大");
            }
            catch (Exception ex)
            {
                Fail(name + " 接缝检查: " + ex.Message);
            }
        }

        CheckVolumeLinearity();
        CheckApiBehavior(sfx, ambient);

        int exported = 0;
        string[] files = Directory.GetFiles(OutDir, "*.wav");
        exported = files.Length;
        Console.WriteLine();
        Console.WriteLine("导出 WAV 文件数: " + exported.ToString(CultureInfo.InvariantCulture)
            + "（音效 " + sfx.Length.ToString(CultureInfo.InvariantCulture)
            + " + 环境音 " + ambientCount.ToString(CultureInfo.InvariantCulture) + "）");
        if (exported != sfx.Length + ambientCount)
            Fail("WAV 数量不符: " + exported + " != " + (sfx.Length + ambientCount));

        try
        {
            Audio.Shutdown();
            Stopwatch sw = Stopwatch.StartNew();
            Audio.Init(Root + @"\assets");
            Audio.Volume = 0.8f;
            double initMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            Audio.RenderAmbientWav("waves", 0.8f, false);
            double wavesMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            Audio.RenderSfxWav("gacha", 0.8f, false);
            double sfxMs = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "-- 冷启动性能 -- Init={0:0.00}ms  waves首次合成={1:0.0}ms  gacha首次合成={2:0.0}ms",
                initMs, wavesMs, sfxMs));
            if (initMs > 5.0) Fail("Init 过慢: " + F4(initMs) + "ms");
            if (wavesMs > 200.0) Fail("waves 首次合成超过 200ms: " + F4(wavesMs) + "ms");
            if (sfxMs > 50.0) Fail("单音效首次合成超过 50ms: " + F4(sfxMs) + "ms");
            Audio.Shutdown();
            Audio.Init(Root + @"\assets");
            Audio.Volume = 0.5f;
            Audio.Muted = false;
            Audio.Shutdown();
        }
        catch (Exception ex)
        {
            Fail("Shutdown/二次 Init 抛出异常: " + ex.Message);
        }

        Console.WriteLine();
        if (_failures == 0)
        {
            Console.WriteLine("RESULT: PASS（所有检查通过）");
        }
        else
        {
            Console.WriteLine("RESULT: FAIL（失败 " + _failures.ToString(CultureInfo.InvariantCulture) + " 项）");
        }
        Environment.ExitCode = _failures == 0 ? 0 : 1;
    }

    // ---------------- 检查项 ----------------

    private static void CheckNames(string[] sfx, string[] ambient)
    {
        string[] required =
        {
            "click", "headpat", "feed", "eat", "happy", "sad", "surprised", "pop",
            "gacha", "coin", "pomodoro_start", "pomodoro_end", "alarm", "sleep",
            "snore", "hiccup", "bubble", "dizzy", "levelup", "error", "charge",
            "lowbattery", "notify"
        };
        for (int i = 0; i < required.Length; i++)
        {
            bool found = false;
            for (int k = 0; k < sfx.Length; k++) if (sfx[k] == required[i]) found = true;
            if (!found) Fail("SfxNames 缺少 " + required[i]);
        }
        if (sfx.Length != required.Length) Fail("SfxNames 数量应为 23，实际 " + sfx.Length);
        if (ambient.Length < 7) Fail("AmbientNames 至少 7 项（含 none）");
        if (ambient.Length == 0 || ambient[0] != "none") Fail("AmbientNames[0] 必须为 none");
        string[] want = { "rain", "waves", "fire", "forest", "night", "white", "keyboard" };
        for (int i = 0; i < want.Length; i++)
        {
            bool found = false;
            for (int k = 0; k < ambient.Length; k++) if (ambient[k] == want[i]) found = true;
            if (!found) Fail("AmbientNames 缺少 " + want[i]);
        }
        for (int i = 0; i < ambient.Length; i++)
        {
            string label = Audio.AmbientDisplayName(ambient[i]);
            if (string.IsNullOrEmpty(label)) Fail("AmbientDisplayName 为空: " + ambient[i]);
        }
        string next = "none";
        int guard = 0;
        do
        {
            next = Audio.AmbientNext(next);
            guard++;
        }
        while (next != "none" && guard < 32);
        if (next != "none" || guard != ambient.Length)
            Fail("AmbientNext 未在 " + ambient.Length + " 步内回到 none（实际 " + guard + "）");
        if (Audio.AmbientNext("不存在的环境音") != "none")
            Fail("AmbientNext(未知) 应返回 none");
    }

    private static void PrintStat(string name, WavInfo info, int bytes)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,-16} {1,8} {2,8} {3,8} {4,6}  {5}",
            name, F(info.DurationMs), F4(info.Peak), F4(info.Rms), info.Clipped, bytes));
    }

    private static void CheckWavHeader(string name, WavInfo info, int bytes)
    {
        if (info.Format != 1) Fail(name + ": 非 PCM（format=" + info.Format + "）");
        if (info.Rate != 22050) Fail(name + ": 采样率 " + info.Rate + " != 22050");
        if (info.Channels != 1) Fail(name + ": 声道 " + info.Channels + " != 1");
        if (info.Bits != 16) Fail(name + ": 位深 " + info.Bits + " != 16");
        if (info.DataBytes != info.SampleCount * 2) Fail(name + ": data 长度与样本数不符");
        if (bytes != 44 + info.DataBytes) Fail(name + ": RIFF 总长与 data 不符");
    }

    private static void CheckSignal(string name, WavInfo info)
    {
        if (info.Peak > 1.0) Fail(name + ": 峰值 > 1.0 (" + F4(info.Peak) + ")");
        if (info.Peak < 0.02) Fail(name + ": 峰值过低 (" + F4(info.Peak) + ")");
        if (info.Clipped != 0) Fail(name + ": 削波样本 " + info.Clipped);
        if (info.Rms < 0.001) Fail(name + ": RMS 近零 (" + F4(info.Rms) + ")");
    }

    private static void CheckVolumeLinearity()
    {
        Console.WriteLine();
        Console.WriteLine("-- 音量线性（happy） --");
        double[] vols = { 0.0, 0.25, 0.5, 1.0 };
        double[] peaks = new double[vols.Length];
        for (int i = 0; i < vols.Length; i++)
        {
            try
            {
                byte[] wav = Audio.RenderSfxWav("happy", (float)vols[i], false);
                WavInfo info = ParseWavBytes(wav);
                peaks[i] = info.Peak;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Volume={0,-5} peak={1}", vols[i], F4(info.Peak)));
            }
            catch (Exception ex)
            {
                Fail("音量 " + vols[i] + " 渲染异常: " + ex.Message);
            }
        }
        if (peaks[3] <= 0.02) Fail("Volume=1.0 峰值异常低");
        for (int i = 0; i < vols.Length - 1; i++)
        {
            double expected = peaks[3] * vols[i];
            double err = Math.Abs(peaks[i] - expected);
            if (peaks[i] > 0.02 && err > 0.005)
                Fail("Volume=" + vols[i] + " 峰值未按比例: " + F4(peaks[i]) + " vs " + F4(expected));
            if (vols[i] == 0.0 && peaks[i] != 0.0) Fail("Volume=0 时 WAV 未全零");
        }
        try
        {
            WavInfo zero = ParseWavBytes(Audio.RenderSfxWav("happy", 0.8f, true));
            if (zero.Peak != 0.0) Fail("Muted=true 时 WAV 未全零");
            WavInfo zeroAmb = ParseWavBytes(Audio.RenderAmbientWav("rain", 0.8f, true));
            if (zeroAmb.Peak != 0.0) Fail("Muted=true 时环境音 WAV 未全零");
            Console.WriteLine("Volume=0 / Muted=true: 全零 OK");
        }
        catch (Exception ex)
        {
            Fail("静音渲染异常: " + ex.Message);
        }
    }

    private static void CheckApiBehavior(string[] sfx, string[] ambient)
    {
        Console.WriteLine();
        Console.WriteLine("-- API 健壮性 --");
        try
        {
            Audio.Muted = true;
            Audio.Volume = 0f;
            Audio.PlaySfx("no_such_sound");
            Audio.PlaySfx(null);
            Audio.PlaySfx("");
            Audio.PlaySfx("CLICK");
            Audio.PlayAmbient("no_such_ambient");
            Audio.PlayAmbient("none");
            Audio.PlayAmbient("rain");
            Audio.PlayAmbient(null);
            Audio.PlayAmbient("RAIN");
            Audio.StopAmbient();
            Audio.PlayAlarm();
            Audio.StopAlarm();
            Audio.StopAmbient();
            Audio.Muted = false;

            float saved = Audio.Volume;
            Audio.Volume = 2.0f;
            if (Audio.Volume != 1.0f) Fail("Volume 未钳制到 1.0");
            Audio.Volume = -1.0f;
            if (Audio.Volume != 0.0f) Fail("Volume 未钳制到 0.0");
            Audio.Volume = float.NaN;
            if (Audio.Volume != 0.0f) Fail("Volume NaN 未归零");
            Audio.Volume = saved;
            Console.WriteLine("非法名字 / 音量钳制 / 静音切换: 无异常 OK");

            for (int i = 0; i < sfx.Length; i++)
            {
                WavInfo info = ParseWavBytes(Audio.RenderSfxWav(sfx[i], 1.0f, false));
                if (info.Peak > 1.0 || info.Clipped != 0)
                    Fail(sfx[i] + " 在 Volume=1.0 时削波/超峰: " + F4(info.Peak));
            }
            Console.WriteLine("全部音效在 Volume=1.0 下无削波 OK");
        }
        catch (Exception ex)
        {
            Fail("API 健壮性测试抛出异常: " + ex.Message);
        }
    }

    // ---------------- WAV 解析 / 统计 ----------------

    private static WavInfo ParseWav(string path)
    {
        return ParseWavBytes(File.ReadAllBytes(path));
    }

    private static WavInfo ParseWavBytes(byte[] b)
    {
        if (b == null) throw new Exception("WAV 为 null");
        if (b.Length < 44) throw new Exception("WAV 长度不足 44 字节");
        if (Encoding.ASCII.GetString(b, 0, 4) != "RIFF") throw new Exception("RIFF 标识错误");
        if (Encoding.ASCII.GetString(b, 8, 4) != "WAVE") throw new Exception("WAVE 标识错误");
        int riffSize = BitConverter.ToInt32(b, 4);
        if (riffSize != b.Length - 8) throw new Exception("RIFF size 错误: " + riffSize);

        WavInfo info = new WavInfo();
        info.Format = -1;
        int pos = 12;
        int dataOffset = -1;
        while (pos + 8 <= b.Length)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4);
            int size = BitConverter.ToInt32(b, pos + 4);
            if (size < 0 || pos + 8 + size > b.Length) throw new Exception("chunk " + id + " 越界");
            if (id == "fmt ")
            {
                if (size < 16) throw new Exception("fmt chunk 过短");
                info.Format = BitConverter.ToInt16(b, pos + 8);
                info.Channels = BitConverter.ToInt16(b, pos + 10);
                info.Rate = BitConverter.ToInt32(b, pos + 12);
                info.Bits = BitConverter.ToInt16(b, pos + 22);
            }
            else if (id == "data")
            {
                dataOffset = pos + 8;
                info.DataBytes = size;
            }
            pos += 8 + size + (size & 1);
        }
        if (info.Format < 0 || dataOffset < 0) throw new Exception("缺少 fmt/data chunk");
        info.SampleCount = info.DataBytes / 2;
        info.Samples = new short[info.SampleCount];
        long sumsq = 0;
        int peak = 0;
        int clipped = 0;
        for (int i = 0; i < info.SampleCount; i++)
        {
            short s = BitConverter.ToInt16(b, dataOffset + i * 2);
            info.Samples[i] = s;
            int a = s < 0 ? -s : s;
            if (a > peak) peak = a;
            if (a >= 32767) clipped++;
            sumsq += (long)s * (long)s;
        }
        info.Clipped = clipped;
        info.Peak = peak / 32768.0;
        info.Rms = info.SampleCount > 0 ? Math.Sqrt(sumsq / (double)info.SampleCount) / 32768.0 : 0.0;
        info.DurationMs = info.SampleCount * 1000.0 / info.Rate;
        return info;
    }

    private static double Energy(WavInfo info, int start, int count)
    {
        double sum = 0.0;
        int end = start + count;
        if (end > info.SampleCount) end = info.SampleCount;
        for (int i = start; i < end; i++)
        {
            double v = info.Samples[i] / 32768.0;
            sum += v * v;
        }
        int n = end - start;
        return n > 0 ? sum / n : 0.0;
    }

    // ---------------- 输出辅助 ----------------

    private static string F(double v)
    {
        return v.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string F4(double v)
    {
        return v.ToString("0.0000", CultureInfo.InvariantCulture);
    }

    private static string F6(double v)
    {
        return v.ToString("0.000000", CultureInfo.InvariantCulture);
    }

    private static void Fail(string message)
    {
        _failures++;
        Console.WriteLine("FAIL: " + message);
        Log.AppendLine(message);
    }
}
