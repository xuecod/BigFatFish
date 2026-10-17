// Program.cs —— 入口：单实例、DPI 感知、全局异常兜底、自检参数
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FishPet
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            bool silent = false;
            string shotPath = null;
            string shotPose = "idle";
            string diagPath = null;
            string style = null;
            string posArg = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-silent", StringComparison.OrdinalIgnoreCase)) silent = true;
                else if (string.Equals(args[i], "-shot", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) shotPath = args[++i];
                else if (string.Equals(args[i], "-pose", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) shotPose = args[++i];
                else if (string.Equals(args[i], "-diag", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) diagPath = args[++i];
                else if (string.Equals(args[i], "-style", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) style = args[++i];
                else if (string.Equals(args[i], "-pos", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) posArg = args[++i];
            }

            bool createdNew = false;
            Mutex mutex = new Mutex(true, "BigFatFishPet_SingleInstance_v1", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("大肥鱼已经在桌面上游着了～（看屏幕右下角托盘图标）", "大肥鱼桌宠",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { SetProcessDPIAware(); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { LogError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                LogError(e.ExceptionObject as Exception);
            };

            Settings cfg = Settings.Load();
            // -style / -pos 只是本次启动的临时覆盖，不写回配置
            string savedStyle = cfg.CharacterStyle;
            int savedPosX = cfg.PosX, savedPosY = cfg.PosY;
            bool tempOverrides = false;
            if (!string.IsNullOrEmpty(style))
            {
                cfg.CharacterStyle = style;
                tempOverrides = true;
            }
            if (!string.IsNullOrEmpty(posArg))
            {
                string[] pp = posArg.Split(',');
                int px, py;
                if (pp.Length == 2 && int.TryParse(pp[0], out px) && int.TryParse(pp[1], out py))
                {
                    cfg.PosX = px;
                    cfg.PosY = py;
                    tempOverrides = true;
                }
            }
            CharacterArt.ApplyStyleSetting(cfg);
            try { Monitor.Start(); } catch { }

            PetForm pet = new PetForm(cfg, silent);
            pet.ShotPath = shotPath;
            pet.ShotPose = shotPose;
            pet.DiagPath = diagPath;
            try
            {
                Application.Run(pet);
            }
            finally
            {
                if (tempOverrides)
                {
                    cfg.CharacterStyle = savedStyle;
                    cfg.PosX = savedPosX;
                    cfg.PosY = savedPosY;
                }
                try { cfg.Save(); } catch { }
                try { mutex.ReleaseMutex(); } catch { }
                try { mutex.Close(); } catch { }
            }
        }

        private static void LogError(Exception ex)
        {
            if (ex == null) return;
            string text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex.ToString() + Environment.NewLine;
            string[] candidates = new string[] {
                Path.Combine(Settings.DataDir, "error.log"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(candidates[i]);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(candidates[i], text, System.Text.Encoding.UTF8);
                    return;
                }
                catch { }
            }
            try { Console.Error.WriteLine(text); } catch { }
        }
    }
}
