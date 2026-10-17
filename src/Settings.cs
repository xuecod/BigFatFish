// Settings.cs —— 配置与存档（key=value 文本，存 %APPDATA%\BigFatFish\）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace FishPet
{
    public sealed class Settings
    {
        /// <summary>
        /// 存档目录。优先 %APPDATA%\BigFatFish；若该位置不可写（权限/杀软/便携场景），
        /// 依次退到 %LOCALAPPDATA%\BigFatFish 和 程序目录\data，保证总能存下来。
        /// </summary>
        public static readonly string DataDir = ResolveDataDir();

        private static string ResolveDataDir()
        {
            string[] candidates = new string[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BigFatFish"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BigFatFish"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    Directory.CreateDirectory(candidates[i]);
                    string probe = Path.Combine(candidates[i], ".writetest");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return candidates[i];
                }
                catch { }
            }
            return candidates[0];
        }

        private static string ConfigPath { get { return Path.Combine(DataDir, "config.ini"); } }
        private static string SavePath { get { return Path.Combine(DataDir, "save.ini"); } }

        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ---- 外观 ----
        public float Scale = 1.0f;            // 0.35 ~ 2.5
        public float Opacity = 1.0f;          // 0.3 ~ 1.0
        public bool TopMost = true;
        public bool SimpleMode = false;       // 简约模式：无气泡、无监控
        public string CharacterStyle = "auto"; // auto=有立绘用立绘 / sprite=只用立绘 / draw=只用手绘
        public bool FollowMouse = false;      // 跟随鼠标模式
        public bool WalkAround = true;        // 自动漫游
        public bool MouseThrough = false;     // 鼠标穿透
        public bool HideFullscreen = true;    // 全屏应用时自动隐身
        // ---- 声音 ----
        public bool Muted = false;
        public int Volume = 70;               // 0~100
        public int AmbientVolume = 45;
        public string Ambient = "none";       // 环境音
        // ---- 交互 ----
        public int DoubleClickAction = 0;     // 0=主菜单 1=小鱼干 2=说话 3=专注 4=白米饭
        public float BubbleSeconds = 5f;
        public bool SpeechEnabled = true;
        // ---- 行为 ----
        public int PosX = -1, PosY = -1;
        public Accessory Worn = Accessory.None;
        public List<Accessory> Owned = new List<Accessory>();
        // ---- 养成数值 ----
        public float Satiety = 60f;           // 饱食度 0~100
        public float Intimacy = 0f;           // 亲密度 0~100
        public int FedCount = 0;
        public int RiceCount = 0;             // 铁盆白饭的盆数
        public int PomodoroCount = 0;
        public int Coins = 3;                 // 鱼丸（扭蛋币）
        public string LastFeed = "";
        public string LastSeenDate = "";
        public double TodaySlackMinutes = 0;
        public double TotalSlackMinutes = 0;
        // ---- 番茄钟 ----
        public int FocusMinutes = 25;
        public int BreakMinutes = 5;
        public bool AutoNextPomodoro = true;
        // ---- 闹钟 ----
        public string AlarmTime = "";         // HH:mm
        public bool AlarmEnabled = false;
        public bool AlarmDaily = false;
        // ---- 系统监控小窗 ----
        public bool ShowCpu = true, ShowGpu = true, ShowMem = true, ShowNet = true;
        public bool WidgetsCollapsed = false;
        public int WidgetX = -1, WidgetY = -1;
        public bool NotifyBattery = true;
        public bool NotifyHotCpu = true;

        public string Get(string key, string def)
        {
            string v;
            return _map.TryGetValue(key, out v) ? v : def;
        }

        public void Set(string key, string value)
        {
            _map[key] = value == null ? "" : value;
        }

        private static float F(string s, float def)
        {
            float f;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return f;
            return def;
        }

        private static int I(string s, int def)
        {
            int i;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return i;
            return def;
        }

        private static bool B(string s, bool def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try { Directory.CreateDirectory(DataDir); }
            catch { }
            s.ReadFile(ConfigPath);
            s.ReadFile(SavePath);
            return s;
        }

        private void ReadFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    _map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { }
            Bind();
        }

        private void Bind()
        {
            Scale = Clamp(F(Get("Scale", "1"), 1f), 0.35f, 2.5f);
            Opacity = Clamp(F(Get("Opacity", "1"), 1f), 0.3f, 1f);
            TopMost = B(Get("TopMost", "1"), true);
            SimpleMode = B(Get("SimpleMode", "0"), false);
            CharacterStyle = Get("CharacterStyle", "auto");
            FollowMouse = B(Get("FollowMouse", "0"), false);
            WalkAround = B(Get("WalkAround", "1"), true);
            MouseThrough = B(Get("MouseThrough", "0"), false);
            HideFullscreen = B(Get("HideFullscreen", "1"), true);
            Muted = B(Get("Muted", "0"), false);
            Volume = I(Get("Volume", "70"), 70);
            AmbientVolume = I(Get("AmbientVolume", "45"), 45);
            Ambient = Get("Ambient", "none");
            DoubleClickAction = I(Get("DoubleClickAction", "0"), 0);
            BubbleSeconds = Clamp(F(Get("BubbleSeconds", "5"), 5f), 2f, 20f);
            SpeechEnabled = B(Get("SpeechEnabled", "1"), true);
            PosX = I(Get("PosX", "-1"), -1);
            PosY = I(Get("PosY", "-1"), -1);
            Worn = (Accessory)I(Get("Worn", "0"), 0);
            string owned = Get("Owned", "");
            Owned.Clear();
            if (owned.Length > 0)
            {
                foreach (string part in owned.Split(','))
                {
                    int v;
                    if (int.TryParse(part.Trim(), out v) && v > 0) Owned.Add((Accessory)v);
                }
            }
            Satiety = Clamp(F(Get("Satiety", "60"), 60f), 0f, 100f);
            Intimacy = Clamp(F(Get("Intimacy", "0"), 0f), 0f, 100f);
            FedCount = I(Get("FedCount", "0"), 0);
            RiceCount = I(Get("RiceCount", "0"), 0);
            PomodoroCount = I(Get("PomodoroCount", "0"), 0);
            Coins = I(Get("Coins", "3"), 3);
            LastFeed = Get("LastFeed", "");
            LastSeenDate = Get("LastSeenDate", "");
            TodaySlackMinutes = F(Get("TodaySlackMinutes", "0"), 0f);
            TotalSlackMinutes = F(Get("TotalSlackMinutes", "0"), 0f);
            FocusMinutes = I(Get("FocusMinutes", "25"), 25);
            BreakMinutes = I(Get("BreakMinutes", "5"), 5);
            AutoNextPomodoro = B(Get("AutoNextPomodoro", "1"), true);
            AlarmTime = Get("AlarmTime", "");
            AlarmEnabled = B(Get("AlarmEnabled", "0"), false);
            AlarmDaily = B(Get("AlarmDaily", "0"), false);
            ShowCpu = B(Get("ShowCpu", "1"), true);
            ShowGpu = B(Get("ShowGpu", "1"), true);
            ShowMem = B(Get("ShowMem", "1"), true);
            ShowNet = B(Get("ShowNet", "1"), true);
            WidgetsCollapsed = B(Get("WidgetsCollapsed", "0"), false);
            WidgetX = I(Get("WidgetX", "-1"), -1);
            WidgetY = I(Get("WidgetY", "-1"), -1);
            NotifyBattery = B(Get("NotifyBattery", "1"), true);
            NotifyHotCpu = B(Get("NotifyHotCpu", "1"), true);
        }

        public static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# 大肥鱼桌宠配置");
                sb.AppendLine("Scale=" + Scale.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("Opacity=" + Opacity.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("TopMost=" + (TopMost ? "1" : "0"));
                sb.AppendLine("SimpleMode=" + (SimpleMode ? "1" : "0"));
                sb.AppendLine("CharacterStyle=" + CharacterStyle);
                sb.AppendLine("FollowMouse=" + (FollowMouse ? "1" : "0"));
                sb.AppendLine("WalkAround=" + (WalkAround ? "1" : "0"));
                sb.AppendLine("MouseThrough=" + (MouseThrough ? "1" : "0"));
                sb.AppendLine("HideFullscreen=" + (HideFullscreen ? "1" : "0"));
                sb.AppendLine("Muted=" + (Muted ? "1" : "0"));
                sb.AppendLine("Volume=" + Volume);
                sb.AppendLine("AmbientVolume=" + AmbientVolume);
                sb.AppendLine("Ambient=" + Ambient);
                sb.AppendLine("DoubleClickAction=" + DoubleClickAction);
                sb.AppendLine("BubbleSeconds=" + BubbleSeconds.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("SpeechEnabled=" + (SpeechEnabled ? "1" : "0"));
                sb.AppendLine("PosX=" + PosX);
                sb.AppendLine("PosY=" + PosY);
                sb.AppendLine("Worn=" + (int)Worn);
                StringBuilder owned = new StringBuilder();
                for (int i = 0; i < Owned.Count; i++)
                {
                    if (i > 0) owned.Append(',');
                    owned.Append((int)Owned[i]);
                }
                sb.AppendLine("Owned=" + owned.ToString());
                sb.AppendLine("Satiety=" + Satiety.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("Intimacy=" + Intimacy.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("FedCount=" + FedCount);
                sb.AppendLine("RiceCount=" + RiceCount);
                sb.AppendLine("PomodoroCount=" + PomodoroCount);
                sb.AppendLine("Coins=" + Coins);
                sb.AppendLine("LastFeed=" + LastFeed);
                sb.AppendLine("LastSeenDate=" + LastSeenDate);
                sb.AppendLine("TodaySlackMinutes=" + TodaySlackMinutes.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("TotalSlackMinutes=" + TotalSlackMinutes.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("FocusMinutes=" + FocusMinutes);
                sb.AppendLine("BreakMinutes=" + BreakMinutes);
                sb.AppendLine("AutoNextPomodoro=" + (AutoNextPomodoro ? "1" : "0"));
                sb.AppendLine("AlarmTime=" + AlarmTime);
                sb.AppendLine("AlarmEnabled=" + (AlarmEnabled ? "1" : "0"));
                sb.AppendLine("AlarmDaily=" + (AlarmDaily ? "1" : "0"));
                sb.AppendLine("ShowCpu=" + (ShowCpu ? "1" : "0"));
                sb.AppendLine("ShowGpu=" + (ShowGpu ? "1" : "0"));
                sb.AppendLine("ShowMem=" + (ShowMem ? "1" : "0"));
                sb.AppendLine("ShowNet=" + (ShowNet ? "1" : "0"));
                sb.AppendLine("WidgetsCollapsed=" + (WidgetsCollapsed ? "1" : "0"));
                sb.AppendLine("WidgetX=" + WidgetX);
                sb.AppendLine("WidgetY=" + WidgetY);
                sb.AppendLine("NotifyBattery=" + (NotifyBattery ? "1" : "0"));
                sb.AppendLine("NotifyHotCpu=" + (NotifyHotCpu ? "1" : "0"));
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);

                StringBuilder sv = new StringBuilder();
                sv.AppendLine("Satiety=" + Satiety.ToString("0.##", CultureInfo.InvariantCulture));
                sv.AppendLine("Intimacy=" + Intimacy.ToString("0.##", CultureInfo.InvariantCulture));
                sv.AppendLine("FedCount=" + FedCount);
                sv.AppendLine("RiceCount=" + RiceCount);
                sv.AppendLine("PomodoroCount=" + PomodoroCount);
                sv.AppendLine("Coins=" + Coins);
                sv.AppendLine("Worn=" + (int)Worn);
                sv.AppendLine("Owned=" + owned.ToString());
                sv.AppendLine("LastFeed=" + LastFeed);
                sv.AppendLine("LastSeenDate=" + LastSeenDate);
                sv.AppendLine("TodaySlackMinutes=" + TodaySlackMinutes.ToString("0.##", CultureInfo.InvariantCulture));
                sv.AppendLine("TotalSlackMinutes=" + TotalSlackMinutes.ToString("0.##", CultureInfo.InvariantCulture));
                File.WriteAllText(SavePath, sv.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 排障：把失败原因落到程序目录，方便定位"为什么存档没写出来"
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save_error.log"),
                        DateTime.Now.ToString("HH:mm:ss") + "  " + ex.GetType().Name + ": " + ex.Message +
                        "  target=" + ConfigPath + "  cwd=" + Environment.CurrentDirectory + Environment.NewLine);
                }
                catch { }
            }
        }

        // ---------------- 开机自启 ----------------
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "BigFatFishPet";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(RunName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enabled) k.SetValue(RunName, "\"" + System.Windows.Forms.Application.ExecutablePath + "\" -silent");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        public bool IsOwned(Accessory a)
        {
            return Owned.Contains(a);
        }
    }
}
