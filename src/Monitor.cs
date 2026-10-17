// Monitor.cs —— 系统监控采样（CPU / 内存 / 网络 / 磁盘 / 电池 / GPU / 温度 / 运行时长）
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

namespace FishPet
{
    public sealed class SysSnapshot
    {
        public float CpuLoad = -1f;               // %
        public float MemPercent = -1f;
        public float MemUsedGB = -1f;
        public float MemTotalGB = -1f;
        public float NetDownKBps = -1f;
        public float NetUpKBps = -1f;
        public float DiskUsedPercent = -1f;
        public float DiskFreeGB = -1f;
        public bool HasBattery = false;
        public int BatteryPercent = -1;
        public bool AcOnline = false;
        public bool Charging = false;
        public float CpuTempC = -1f;
        public float GpuUsage = -1f;
        public float GpuTempC = -1f;
        public float GpuFanPercent = -1f;
        public float GpuMemUsedMB = -1f;
        public float GpuMemTotalMB = -1f;
        public string GpuName = "";
        public double UptimeHours = -1;
        public int ProcessCount = -1;
        public uint IdleMs = 0;
        public DateTime SampledAt = DateTime.MinValue;

        public SysSnapshot Clone()
        {
            SysSnapshot s = (SysSnapshot)MemberwiseClone();
            return s;
        }
    }

    public static class Monitor
    {
        private static readonly object _lock = new object();
        private static SysSnapshot _latest = new SysSnapshot();
        private static Thread _thread;
        private static volatile bool _running;
        private static bool _tempUnsupported;
        private static bool _gpuUnsupported;

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }
        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

        public static SysSnapshot Latest
        {
            get { lock (_lock) { return _latest.Clone(); } }
        }

        public static bool GpuAvailable { get { return !_gpuUnsupported; } }
        public static bool TempAvailable { get { return !_tempUnsupported; } }

        public static void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "FishPetMonitor";
            _thread.Start();
        }

        public static void Stop() { _running = false; }

        public static void RefreshNow()
        {
            try { Sample(0); } catch { }
        }

        private static void Loop()
        {
            int tick = 0;
            while (_running)
            {
                try { Sample(tick); }
                catch { }
                tick++;
                Thread.Sleep(1000);
            }
        }

        private static ulong _prevIdle, _prevKernel, _prevUser;
        private static bool _cpuPrimed;
        private static long _prevBytesIn = -1, _prevBytesOut = -1;
        private static DateTime _prevNetTime = DateTime.MinValue;

        private static void Sample(int tick)
        {
            SysSnapshot s;
            lock (_lock) { s = _latest; }
            s = s.Clone();

            // ---- CPU ----
            FILETIME idleFt, kernelFt, userFt;
            if (GetSystemTimes(out idleFt, out kernelFt, out userFt))
            {
                ulong idle = ToU64(idleFt), kernel = ToU64(kernelFt), user = ToU64(userFt);
                if (_cpuPrimed)
                {
                    ulong dIdle = idle - _prevIdle;
                    ulong dKernel = kernel - _prevKernel;
                    ulong dUser = user - _prevUser;
                    ulong total = dKernel + dUser;   // kernel 已含 idle
                    if (total > 0)
                    {
                        float busy = (float)((double)(total - dIdle) * 100.0 / (double)total);
                        if (busy < 0f) busy = 0f;
                        if (busy > 100f) busy = 100f;
                        s.CpuLoad = busy;
                    }
                }
                _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
                _cpuPrimed = true;
            }

            // ---- 内存 ----
            try
            {
                MEMORYSTATUSEX m = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(m))
                {
                    s.MemPercent = m.dwMemoryLoad;
                    s.MemTotalGB = (float)(m.ullTotalPhys / 1073741824.0);
                    s.MemUsedGB = (float)((m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0);
                }
            }
            catch { }

            // ---- 网络 ----
            try
            {
                long bin = 0, bout = 0;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface ni = nics[i];
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    try
                    {
                        IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                        bin += st.BytesReceived;
                        bout += st.BytesSent;
                    }
                    catch { }
                }
                DateTime now = DateTime.UtcNow;
                if (_prevBytesIn >= 0 && _prevNetTime != DateTime.MinValue)
                {
                    double sec = (now - _prevNetTime).TotalSeconds;
                    if (sec > 0.2)
                    {
                        s.NetDownKBps = (float)Math.Max(0, (bin - _prevBytesIn) / 1024.0 / sec);
                        s.NetUpKBps = (float)Math.Max(0, (bout - _prevBytesOut) / 1024.0 / sec);
                    }
                }
                _prevBytesIn = bin; _prevBytesOut = bout; _prevNetTime = now;
            }
            catch { }

            // ---- 电池 / 运行时长（每 5 个 tick）----
            if (tick % 5 == 0)
            {
                try
                {
                    SYSTEM_POWER_STATUS sps;
                    if (GetSystemPowerStatus(out sps))
                    {
                        s.HasBattery = sps.BatteryFlag != 128 && sps.BatteryFlag != 255;
                        if (sps.BatteryLifePercent != 255) s.BatteryPercent = sps.BatteryLifePercent;
                        s.AcOnline = sps.ACLineStatus == 1;
                        s.Charging = s.HasBattery && s.AcOnline && s.BatteryPercent >= 0 && s.BatteryPercent < 100;
                    }
                }
                catch { }
                try { s.UptimeHours = GetTickCount64() / 3600000.0; } catch { }
                try { s.IdleMs = Native.GetIdleMilliseconds(); } catch { }
            }

            // ---- 磁盘（每 15 个 tick）----
            if (tick % 15 == 0)
            {
                try
                {
                    string root = Path.GetPathRoot(Environment.SystemDirectory);
                    DriveInfo di = new DriveInfo(root);
                    if (di.IsReady)
                    {
                        double total = di.TotalSize;
                        double free = di.TotalFreeSpace;
                        s.DiskFreeGB = (float)(free / 1073741824.0);
                        s.DiskUsedPercent = (float)((total - free) * 100.0 / total);
                    }
                }
                catch { }
            }

            // ---- 进程数（每 10 个 tick）----
            if (tick % 10 == 0)
            {
                try { s.ProcessCount = Process.GetProcesses().Length; } catch { }
            }

            // ---- 温度（每 15 个 tick，失败一次就永久停用）----
            if (tick % 15 == 0 && !_tempUnsupported)
            {
                try
                {
                    float t = ReadThermalZoneCelsius();
                    if (t > 0f) s.CpuTempC = t;
                    else if (t < 0f) _tempUnsupported = true;
                }
                catch { _tempUnsupported = true; }
            }

            // ---- GPU（每 3 个 tick）----
            if (tick % 3 == 0 && !_gpuUnsupported)
            {
                try { ReadNvidiaSmi(s); }
                catch { _gpuUnsupported = true; }
            }

            s.SampledAt = DateTime.Now;
            lock (_lock) { _latest = s; }
        }

        private static ulong ToU64(FILETIME ft)
        {
            return ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        }

        private static float ReadThermalZoneCelsius()
        {
            float best = -1f;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    using (ManagementObjectCollection col = searcher.Get())
                    {
                        foreach (ManagementBaseObject mo in col)
                        {
                            try
                            {
                                object v = mo["CurrentTemperature"];
                                if (v == null) continue;
                                double kelvin10 = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                                double c = kelvin10 / 10.0 - 273.15;
                                if (c > 0 && c < 150) { if (c > best) best = (float)c; }
                            }
                            catch { }
                            finally { mo.Dispose(); }
                        }
                    }
                }
            }
            catch { return -1f; }
            return best;
        }

        private static void ReadNvidiaSmi(SysSnapshot s)
        {
            ProcessStartInfo psi = new ProcessStartInfo("nvidia-smi");
            psi.Arguments = "--query-gpu=name,utilization.gpu,temperature.gpu,fan.speed,memory.used,memory.total --format=csv,noheader,nounits";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = System.Text.Encoding.ASCII;

            using (Process p = Process.Start(psi))
            {
                if (p == null) { _gpuUnsupported = true; return; }
                string outp = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(2500))
                {
                    try { p.Kill(); } catch { }
                    _gpuUnsupported = true;
                    return;
                }
                if (p.ExitCode != 0 || string.IsNullOrEmpty(outp)) { _gpuUnsupported = true; return; }
                string line = outp.Replace("\r", "").Split('\n')[0].Trim();
                string[] parts = line.Split(',');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
                if (parts.Length >= 6)
                {
                    s.GpuName = parts[0];
                    s.GpuUsage = ParseF(parts[1]);
                    s.GpuTempC = ParseF(parts[2]);
                    s.GpuFanPercent = ParseF(parts[3]);
                    s.GpuMemUsedMB = ParseF(parts[4]);
                    s.GpuMemTotalMB = ParseF(parts[5]);
                }
            }
        }

        private static float ParseF(string t)
        {
            if (string.IsNullOrEmpty(t)) return -1f;
            t = t.Trim();
            if (t.Length == 0 || t.IndexOf('N') >= 0 || t.IndexOf('n') >= 0) return -1f;
            float f;
            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            return -1f;
        }

        public static string FormatSpeed(float kbps)
        {
            if (kbps < 0) return "--";
            if (kbps < 1024f) return kbps.ToString("0") + " KB/s";
            return (kbps / 1024f).ToString("0.0") + " MB/s";
        }

        public static Color LoadColor(float percent)
        {
            if (percent < 0) return Palette.PanelSub;
            if (percent < 60f) return Palette.BarGreen;
            if (percent < 85f) return Palette.BarYellow;
            return Palette.BarRed;
        }

        /// <summary>是否为华硕天选整机（读注册表机型串）</summary>
        public static bool IsAsusTufMachine()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (k == null) return false;
                    object model = k.GetValue("SystemProductName");
                    object vendor = k.GetValue("SystemManufacturer");
                    string m = model == null ? "" : model.ToString();
                    string v = vendor == null ? "" : vendor.ToString();
                    if (v.IndexOf("ASUS", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (m.IndexOf("TUF", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        if (m.IndexOf("天选", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        if (m.IndexOf("FX", StringComparison.OrdinalIgnoreCase) == 0) return true;
                        if (m.IndexOf("FA", StringComparison.OrdinalIgnoreCase) == 0) return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
