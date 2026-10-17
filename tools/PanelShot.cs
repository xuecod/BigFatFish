// PanelShot.cs —— 开发自检：把各个功能面板渲染成 PNG，便于在不可见桌面的环境里检查布局
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FishPet
{
    internal static class PanelShot
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dir = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "panels");
            Directory.CreateDirectory(dir);

            Settings cfg = Settings.Load();
            PetForm pet = new PetForm(cfg, true);

            Save(new WidgetsForm(cfg, pet), dir, "1_系统信息小窗");
            Save(new PomodoroForm(cfg, pet), dir, "2_番茄钟");
            Save(new GachaForm(cfg, pet), dir, "3_扭蛋换装");
            Save(new AlarmForm(cfg, pet), dir, "4_闹钟");
            Save(new ReportForm(cfg, pet), dir, "5_今日鱼报");
            Save(new MemeForm(cfg, pet), dir, "6_表情包");
            Save(new SettingsForm(cfg, pet), dir, "7_设置");

            Console.WriteLine("面板截图输出到：" + dir);
            Application.Exit();
        }

        private static void Save(Form f, string dir, string name)
        {
            try
            {
                // 子控件只有在窗口"显示过"之后才会响应 WM_PRINT，所以先挪到屏幕外显示一下
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-4000, -4000);
                f.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(120);
                Application.DoEvents();

                using (Bitmap b = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                    b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
                }
                f.Hide();
                Console.WriteLine("  " + name + "  " + f.Width + "x" + f.Height);
            }
            catch (Exception ex)
            {
                Console.WriteLine("  " + name + " 失败：" + ex.Message);
            }
        }
    }
}
