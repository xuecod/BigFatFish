// Panels.cs —— 功能面板：系统信息小窗 / 番茄钟 / 扭蛋换装 / 闹钟 / 今日鱼报 / 表情包 / 设置
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FishPet
{
    /// <summary>桌宠向面板暴露的能力（由 PetForm 实现）</summary>
    public interface IPetHost
    {
        Settings Cfg { get; }
        SysSnapshot Snapshot { get; }
        void Say(string text);
        void Say(string text, float seconds);
        void SayWithMood(string text, PetMood mood);
        void ForceMood(PetMood mood, float seconds);
        void Notify(string title, string text);
        void RewardCoins(int n, string reason);
        void AddIntimacy(float v);
        void PlaySfx(string name);
        void SetAmbient(string name);
        void RefreshAppearance();
        void OpenWidgets();
    }

    // =====================================================================
    //  通用 UI 工具
    // =====================================================================
    internal static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(26, 36, 60);
        public static readonly Color Bg2 = Color.FromArgb(38, 52, 82);
        public static readonly Color Bg3 = Color.FromArgb(48, 64, 98);
        public static readonly Color Text = Color.FromArgb(234, 242, 255);
        public static readonly Color Sub = Color.FromArgb(148, 174, 214);
        public static readonly Color Accent = Color.FromArgb(96, 140, 246);
        public static readonly Color Good = Color.FromArgb(92, 214, 168);
        public static readonly Color Warn = Color.FromArgb(250, 205, 96);
        public static readonly Color Bad = Color.FromArgb(250, 110, 120);

        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        public static Font F(float size, bool bold)
        {
            string key = size.ToString("0.#") + (bold ? "b" : "n");
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            f = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            FontCache[key] = f;
            return f;
        }

        public static Region Rounded(Size s, int r)
        {
            GraphicsPath gp = new GraphicsPath();
            int d = r * 2;
            gp.AddArc(0, 0, d, d, 180, 90);
            gp.AddArc(s.Width - d - 1, 0, d, d, 270, 90);
            gp.AddArc(s.Width - d - 1, s.Height - d - 1, d, d, 0, 90);
            gp.AddArc(0, s.Height - d - 1, d, d, 90, 90);
            gp.CloseFigure();
            Region reg = new Region(gp);
            gp.Dispose();
            return reg;
        }

        public static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(120, 160, 240);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 96, 150);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(56, 76, 120);
            b.BackColor = Bg2;
            b.ForeColor = Text;
            b.Font = F(9f, false);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
        }

        public static void StyleCheck(CheckBox c)
        {
            c.ForeColor = Text;
            c.BackColor = Color.Transparent;
            c.Font = F(9f, false);
            c.FlatStyle = FlatStyle.Flat;
            c.Cursor = Cursors.Hand;
        }

        public static void StyleLabel(Label l, bool sub)
        {
            l.ForeColor = sub ? Sub : Text;
            l.BackColor = Color.Transparent;
            l.Font = F(sub ? 8.5f : 9.5f, false);
        }

        public static void StyleCombo(ComboBox c)
        {
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Bg2;
            c.ForeColor = Text;
            c.Font = F(9f, false);
            c.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        public static void StyleNum(NumericUpDown n)
        {
            n.BackColor = Bg2;
            n.ForeColor = Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = F(9f, false);
        }

        public static void StyleTrack(TrackBar t)
        {
            t.BackColor = Bg;
            t.TickStyle = TickStyle.None;
            t.Height = 26;
        }

        public static void StyleText(TextBox t, bool multiline)
        {
            t.BackColor = Bg2;
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = F(9f, false);
            t.Multiline = multiline;
            if (multiline) t.ScrollBars = ScrollBars.Vertical;
        }

        public static void DrawBar(Graphics g, RectangleF r, float percent, Color color, string label)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(50, 70, 108)))
                g.FillRectangle(bg, r);
            float w = r.Width * Math.Max(0f, Math.Min(100f, percent)) / 100f;
            if (w > 0f)
            {
                using (SolidBrush fg = new SolidBrush(color))
                    g.FillRectangle(fg, new RectangleF(r.X, r.Y, w, r.Height));
            }
            using (Pen p = new Pen(Color.FromArgb(70, 96, 140), 1f))
                g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            if (!string.IsNullOrEmpty(label))
            {
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 248, 255)))
                using (StringFormat sf = new StringFormat())
                {
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Alignment = StringAlignment.Far;
                    g.DrawString(label, F(8.5f, false), tb, new RectangleF(r.X, r.Y, r.Width - 4f, r.Height), sf);
                }
            }
        }

        public static string Pct(float v)
        {
            if (v < 0) return "--";
            return ((int)Math.Round(v)) + "%";
        }

        public static string Temp(float v)
        {
            if (v < 0) return "--";
            return ((int)Math.Round(v)) + "°C";
        }
    }

    // =====================================================================
    //  卡片窗体基类：无边框深色圆角 + 可拖动 + 关闭按钮
    // =====================================================================
    public class CardForm : Form
    {
        protected Settings cfg;
        protected PetForm pet;
        private readonly string caption;
        private bool dragging;
        private Point dragOffset;
        private readonly Button closeBtn;

        public CardForm(Settings c, PetForm p, string title, Size size)
        {
            cfg = c;
            pet = p;
            caption = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Ui.Bg;
            Size = size;
            MinimumSize = size;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Region = Ui.Rounded(size, 12);

            closeBtn = new Button();
            closeBtn.Text = "✕";
            closeBtn.Size = new Size(26, 24);
            closeBtn.Location = new Point(size.Width - 32, 5);
            Ui.StyleButton(closeBtn);
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += delegate { Hide(); };
            Controls.Add(closeBtn);

            MouseDown += CardMouseDown;
            MouseMove += CardMouseMove;
            MouseUp += CardMouseUp;
        }

        private void CardMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragOffset = new Point(e.X, e.Y);
            }
        }

        private void CardMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
                Location = new Point(Cursor.Position.X - dragOffset.X, Cursor.Position.Y - dragOffset.Y);
        }

        private void CardMouseUp(object sender, MouseEventArgs e) { dragging = false; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Ui.Bg2, Ui.Bg, 90f))
                g.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(Color.FromArgb(90, 130, 220), 1f))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (SolidBrush tb = new SolidBrush(Ui.Text))
                g.DrawString(caption, Ui.F(10f, true), tb, 12f, 8f);
            base.OnPaint(e);
        }

        public virtual void SyncFromConfig() { }
    }

    // =====================================================================
    //  系统信息小窗（对应天选姬的 4 个 systemInfoType 小窗）
    // =====================================================================
    public sealed class WidgetsForm : CardForm
    {
        private readonly Timer timer;
        private readonly List<string> sections = new List<string>();
        private readonly Dictionary<string, bool> collapsed = new Dictionary<string, bool>();

        public WidgetsForm(Settings c, PetForm p) : base(c, p, "系统信息 · 大肥鱼", new Size(320, 210))
        {
            collapsed["CPU"] = false;
            collapsed["GPU"] = false;
            collapsed["内存"] = false;
            collapsed["网络"] = false;
            if (c.WidgetX >= 0 && c.WidgetY >= 0)
                Location = new Point(c.WidgetX, c.WidgetY);
            else
                Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 24, Screen.PrimaryScreen.WorkingArea.Top + 24);

            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += delegate { Invalidate(); };
            timer.Start();
            SyncFromConfig();
        }

        public override void SyncFromConfig()
        {
            sections.Clear();
            if (cfg.ShowCpu) sections.Add("CPU");
            if (cfg.ShowGpu) sections.Add("GPU");
            if (cfg.ShowMem) sections.Add("内存");
            if (cfg.ShowNet) sections.Add("网络");
            int h = 34;
            for (int i = 0; i < sections.Count; i++)
                h += collapsed[sections[i]] ? 26 : 62;
            h += 26;
            if (sections.Count == 0) h = 60;
            if (h < 90) h = 90;
            Size = new Size(320, h);
            MinimumSize = Size;
            Region = Ui.Rounded(Size, 12);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int y = 34;
            for (int i = 0; i < sections.Count; i++)
            {
                string s = sections[i];
                int hh = collapsed[s] ? 26 : 62;
                if (e.Y >= y && e.Y < y + 22 && e.Button == MouseButtons.Left)
                {
                    collapsed[s] = !collapsed[s];
                    SyncFromConfig();
                    return;
                }
                y += hh;
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            SysSnapshot s = Monitor.Latest;

            if (sections.Count == 0)
            {
                using (SolidBrush b = new SolidBrush(Ui.Sub))
                    g.DrawString("没有勾选任何项目（右键桌宠 → 系统信息小窗）", Ui.F(8.5f, false), b, 12f, 44f);
                return;
            }

            int y = 34;
            for (int i = 0; i < sections.Count; i++)
            {
                string name = sections[i];
                bool col = collapsed[name];
                int hh = col ? 26 : 62;

                using (SolidBrush hb = new SolidBrush(Ui.Bg3))
                    g.FillRectangle(hb, 8, y, Width - 16, 20);
                using (SolidBrush tb = new SolidBrush(Ui.Text))
                    g.DrawString((col ? "▸ " : "▾ ") + name, Ui.F(8.5f, true), tb, 12f, y + 3f);
                using (SolidBrush sb = new SolidBrush(Ui.Sub))
                    g.DrawString(Summary(name, s), Ui.F(8.5f, false), sb, 74f, y + 3f);

                if (!col) DrawSection(g, name, s, new RectangleF(12, y + 24, Width - 24, 34));
                y += hh;
            }

            using (SolidBrush fb = new SolidBrush(Ui.Sub))
            {
                string foot = "电量 " + (s.HasBattery ? s.BatteryPercent + "%" + (s.AcOnline ? "(充电)" : "") : "无电池")
                    + "   开机 " + (s.UptimeHours >= 0 ? s.UptimeHours.ToString("0.0") + "h" : "--")
                    + "   进程 " + (s.ProcessCount >= 0 ? s.ProcessCount.ToString() : "--");
                g.DrawString(foot, Ui.F(8f, false), fb, 12f, y + 4f);
            }
        }

        private static string Summary(string name, SysSnapshot s)
        {
            if (name == "CPU") return Ui.Pct(s.CpuLoad) + (s.CpuTempC > 0 ? "   " + Ui.Temp(s.CpuTempC) : "");
            if (name == "GPU") return s.GpuUsage < 0 ? "无独显数据" : Ui.Pct(s.GpuUsage) + "   " + Ui.Temp(s.GpuTempC) + "   风扇 " + Ui.Pct(s.GpuFanPercent);
            if (name == "内存") return Ui.Pct(s.MemPercent);
            if (name == "网络") return "↓" + Monitor.FormatSpeed(s.NetDownKBps) + "  ↑" + Monitor.FormatSpeed(s.NetUpKBps);
            return "";
        }

        private static void DrawSection(Graphics g, string name, SysSnapshot s, RectangleF r)
        {
            if (name == "CPU")
            {
                Ui.DrawBar(g, new RectangleF(r.X, r.Y + 6, r.Width, 12), s.CpuLoad, Monitor.LoadColor(s.CpuLoad), Ui.Pct(s.CpuLoad));
            }
            else if (name == "内存")
            {
                Ui.DrawBar(g, new RectangleF(r.X, r.Y + 6, r.Width, 12), s.MemPercent, Monitor.LoadColor(s.MemPercent), Ui.Pct(s.MemPercent));
                using (SolidBrush b = new SolidBrush(Ui.Sub))
                {
                    string memText = (s.MemTotalGB > 0f)
                        ? (s.MemUsedGB.ToString("0.0") + " GB / " + s.MemTotalGB.ToString("0.0") + " GB")
                        : "采样中…";
                    g.DrawString(memText, Ui.F(8f, false), b, r.X, r.Y + 20);
                }
            }
            else if (name == "GPU")
            {
                Ui.DrawBar(g, new RectangleF(r.X, r.Y + 6, r.Width, 12), s.GpuUsage, Monitor.LoadColor(s.GpuUsage), Ui.Pct(s.GpuUsage));
                using (SolidBrush b = new SolidBrush(Ui.Sub))
                {
                    g.DrawString("温度 " + Ui.Temp(s.GpuTempC) + "   风扇 " + Ui.Pct(s.GpuFanPercent), Ui.F(8f, false), b, r.X, r.Y + 20);
                    if (s.GpuMemTotalMB > 0)
                        g.DrawString("显存 " + (s.GpuMemUsedMB / 1024f).ToString("0.0") + "/" + (s.GpuMemTotalMB / 1024f).ToString("0.0") + " GB", Ui.F(8f, false), b, r.X + 150f, r.Y + 20);
                }
            }
            else if (name == "网络")
            {
                using (SolidBrush b = new SolidBrush(Ui.Text))
                    g.DrawString("↓ " + Monitor.FormatSpeed(s.NetDownKBps), Ui.F(9.5f, true), b, r.X, r.Y + 2);
                using (SolidBrush b = new SolidBrush(Ui.Text))
                    g.DrawString("↑ " + Monitor.FormatSpeed(s.NetUpKBps), Ui.F(9.5f, true), b, r.X + 150f, r.Y + 2);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            cfg.WidgetX = Location.X;
            cfg.WidgetY = Location.Y;
        }
    }

    // =====================================================================
    //  番茄钟
    // =====================================================================
    public sealed class PomodoroForm : CardForm
    {
        private enum Phase { Idle, Focus, Break, PausedFocus, PausedBreak }

        private Phase phase = Phase.Idle;
        private Phase phaseBeforePause = Phase.Idle;
        private int remaining;
        private int round;
        private readonly Timer timer;
        private readonly Label timeLabel;
        private readonly Label stateLabel;
        private readonly Label roundLabel;
        private readonly Button startBtn;
        private readonly Button pauseBtn;
        private readonly Button giveUpBtn;
        private readonly Button ambientBtn;
        private readonly NumericUpDown focusNum;
        private readonly NumericUpDown breakNum;

        public PomodoroForm(Settings c, PetForm p) : base(c, p, "天选番茄钟 · 大肥鱼", new Size(340, 264))
        {
            timeLabel = new Label();
            timeLabel.Font = Ui.F(30f, true);
            timeLabel.ForeColor = Ui.Text;
            timeLabel.BackColor = Color.Transparent;
            timeLabel.TextAlign = ContentAlignment.MiddleCenter;
            timeLabel.SetBounds(12, 34, Width - 24, 52);
            timeLabel.Text = "25:00";
            Controls.Add(timeLabel);

            stateLabel = new Label();
            Ui.StyleLabel(stateLabel, true);
            stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            stateLabel.SetBounds(12, 84, Width - 24, 20);
            stateLabel.Text = "准备开始";
            Controls.Add(stateLabel);

            roundLabel = new Label();
            Ui.StyleLabel(roundLabel, true);
            roundLabel.SetBounds(12, 104, Width - 24, 20);
            roundLabel.Text = "已完成番茄：" + cfg.PomodoroCount + " 个";
            Controls.Add(roundLabel);

            Label l1 = new Label();
            Ui.StyleLabel(l1, true);
            l1.Text = "专注(分)";
            l1.SetBounds(12, 130, 60, 20);
            Controls.Add(l1);

            focusNum = new NumericUpDown();
            Ui.StyleNum(focusNum);
            focusNum.Minimum = 1;
            focusNum.Maximum = 180;
            focusNum.Value = Math.Max(1, Math.Min(180, cfg.FocusMinutes));
            focusNum.SetBounds(72, 128, 60, 24);
            focusNum.ValueChanged += delegate { cfg.FocusMinutes = (int)focusNum.Value; ResetDisplay(); };
            Controls.Add(focusNum);

            Label l2 = new Label();
            Ui.StyleLabel(l2, true);
            l2.Text = "休息(分)";
            l2.SetBounds(146, 130, 60, 20);
            Controls.Add(l2);

            breakNum = new NumericUpDown();
            Ui.StyleNum(breakNum);
            breakNum.Minimum = 1;
            breakNum.Maximum = 60;
            breakNum.Value = Math.Max(1, Math.Min(60, cfg.BreakMinutes));
            breakNum.SetBounds(206, 128, 60, 24);
            breakNum.ValueChanged += delegate { cfg.BreakMinutes = (int)breakNum.Value; };
            Controls.Add(breakNum);

            ambientBtn = new Button();
            Ui.StyleButton(ambientBtn);
            ambientBtn.SetBounds(12, 160, 150, 26);
            ambientBtn.Text = "环境音：" + Audio.AmbientDisplayName(cfg.Ambient);
            ambientBtn.Click += delegate
            {
                cfg.Ambient = Audio.AmbientNext(cfg.Ambient);
                ambientBtn.Text = "环境音：" + Audio.AmbientDisplayName(cfg.Ambient);
                if (phase == Phase.Focus || phase == Phase.PausedFocus) pet.SetAmbient(cfg.Ambient);
            };
            Controls.Add(ambientBtn);

            startBtn = new Button();
            Ui.StyleButton(startBtn);
            startBtn.SetBounds(12, 194, 96, 30);
            startBtn.Text = "开始专注";
            startBtn.Click += delegate { StartFocus(); };
            Controls.Add(startBtn);

            pauseBtn = new Button();
            Ui.StyleButton(pauseBtn);
            pauseBtn.SetBounds(114, 194, 96, 30);
            pauseBtn.Text = "暂停";
            pauseBtn.Click += delegate { TogglePause(); };
            Controls.Add(pauseBtn);

            giveUpBtn = new Button();
            Ui.StyleButton(giveUpBtn);
            giveUpBtn.SetBounds(216, 194, 110, 30);
            giveUpBtn.Text = "放弃本轮";
            giveUpBtn.Click += delegate { GiveUp(); };
            Controls.Add(giveUpBtn);

            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            ResetDisplay();
        }

        private void ResetDisplay()
        {
            if (phase == Phase.Idle)
            {
                remaining = cfg.FocusMinutes * 60;
                UpdateTime();
            }
        }

        private void UpdateTime()
        {
            int mm = remaining / 60;
            int ss = remaining % 60;
            timeLabel.Text = mm.ToString("00") + ":" + ss.ToString("00");
            roundLabel.Text = "已完成番茄：" + cfg.PomodoroCount + " 个   第 " + (round + 1) + " 轮";
            Invalidate();
        }

        public void StartFocus()
        {
            phase = Phase.Focus;
            remaining = cfg.FocusMinutes * 60;
            stateLabel.Text = "专注中… 本鱼在旁边看着你";
            pet.OnFocusStarted();
            UpdateTime();
        }

        private void StartBreak()
        {
            phase = Phase.Break;
            remaining = cfg.BreakMinutes * 60;
            stateLabel.Text = "休息中… 起来动一动";
            pet.OnBreakStarted();
            UpdateTime();
        }

        private void TogglePause()
        {
            if (phase == Phase.Focus) { phaseBeforePause = phase; phase = Phase.PausedFocus; stateLabel.Text = "已暂停（本鱼在等你）"; }
            else if (phase == Phase.Break) { phaseBeforePause = phase; phase = Phase.PausedBreak; stateLabel.Text = "休息已暂停"; }
            else if (phase == Phase.PausedFocus) { phase = Phase.Focus; stateLabel.Text = "继续专注！"; }
            else if (phase == Phase.PausedBreak) { phase = Phase.Break; stateLabel.Text = "继续休息～"; }
        }

        private void GiveUp()
        {
            if (phase == Phase.Focus || phase == Phase.PausedFocus)
            {
                phase = Phase.Idle;
                stateLabel.Text = "本轮已放弃，随时可以再来";
                remaining = cfg.FocusMinutes * 60;
                pet.OnFocusInterrupted();
            }
            else if (phase == Phase.Break || phase == Phase.PausedBreak)
            {
                phase = Phase.Idle;
                stateLabel.Text = "休息结束";
                pet.OnBreakEnded();
                remaining = cfg.FocusMinutes * 60;
            }
            UpdateTime();
        }

        private void Tick()
        {
            if (phase != Phase.Focus && phase != Phase.Break) return;
            remaining--;
            if (remaining <= 0)
            {
                if (phase == Phase.Focus)
                {
                    round++;
                    pet.OnFocusEnded();
                    if (cfg.AutoNextPomodoro) StartBreak();
                    else { phase = Phase.Idle; stateLabel.Text = "专注完成！"; remaining = cfg.FocusMinutes * 60; }
                }
                else
                {
                    pet.OnBreakEnded();
                    if (cfg.AutoNextPomodoro) StartFocus();
                    else { phase = Phase.Idle; stateLabel.Text = "休息结束"; remaining = cfg.FocusMinutes * 60; }
                }
            }
            else if (phase == Phase.Focus && remaining == 60)
            {
                pet.Say("最后一分钟，冲鸭！", 4f);
            }
            UpdateTime();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float total = 0f, done = 0f;
            if (phase == Phase.Focus || phase == Phase.PausedFocus) { total = cfg.FocusMinutes * 60f; done = total - remaining; }
            else if (phase == Phase.Break || phase == Phase.PausedBreak) { total = cfg.BreakMinutes * 60f; done = total - remaining; }
            float pct = total > 0f ? done * 100f / total : 0f;
            Color col = (phase == Phase.Break || phase == Phase.PausedBreak) ? Ui.Good : Ui.Accent;
            Ui.DrawBar(g, new RectangleF(12, 232, Width - 24, 14), pct, col, ((int)pct) + "%");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ResumeLayout(false);
        }
    }

    // =====================================================================
    //  扭蛋机 + 背包换装
    // =====================================================================
    public sealed class GachaForm : CardForm
    {
        private readonly Label coinLabel;
        private readonly Label resultLabel;
        private readonly ListBox list;
        private readonly PictureBox preview;
        private readonly Timer anim;
        private readonly Random rnd = new Random();
        private float animT;
        private Accessory previewAcc = Accessory.None;

        private static readonly Dictionary<Accessory, bool> Rare = BuildRare();

        private static Dictionary<Accessory, bool> BuildRare()
        {
            Dictionary<Accessory, bool> d = new Dictionary<Accessory, bool>();
            Accessory[] rare = new Accessory[] { Accessory.Crown, Accessory.SantaHat, Accessory.WizardHat, Accessory.BambooCopter };
            for (int i = 0; i < rare.Length; i++) d[rare[i]] = true;
            return d;
        }

        public GachaForm(Settings c, PetForm p) : base(c, p, "大肥鱼扭蛋机 · 换装", new Size(420, 300))
        {
            coinLabel = new Label();
            Ui.StyleLabel(coinLabel, false);
            coinLabel.SetBounds(12, 32, 260, 22);
            Controls.Add(coinLabel);

            Button draw = new Button();
            Ui.StyleButton(draw);
            draw.SetBounds(12, 58, 130, 30);
            draw.Text = "扭一颗（1 鱼丸）";
            draw.Click += delegate { Draw(); };
            Controls.Add(draw);

            Button free = new Button();
            Ui.StyleButton(free);
            free.SetBounds(150, 58, 110, 30);
            free.Text = "免费摸一下";
            free.Click += delegate
            {
                resultLabel.Text = "摸到了：" + Speech.Category("idle");
                pet.PlaySfx("headpat");
                pet.AddIntimacy(0.3f);
            };
            Controls.Add(free);

            resultLabel = new Label();
            Ui.StyleLabel(resultLabel, true);
            resultLabel.SetBounds(12, 92, 250, 44);
            resultLabel.Text = "攒鱼丸：专注一个番茄 +1，陪伴 20 分钟 +1";
            Controls.Add(resultLabel);

            list = new ListBox();
            list.BackColor = Ui.Bg2;
            list.ForeColor = Ui.Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = Ui.F(9f, false);
            list.SetBounds(12, 140, 250, 140);
            list.SelectedIndexChanged += delegate { OnSelect(); };
            Controls.Add(list);

            preview = new PictureBox();
            preview.BackColor = Ui.Bg;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.SetBounds(272, 32, 136, 176);
            preview.Paint += PreviewPaint;
            Controls.Add(preview);

            Button wear = new Button();
            Ui.StyleButton(wear);
            wear.SetBounds(272, 214, 136, 28);
            wear.Text = "戴上选中的";
            wear.Click += delegate { Wear(); };
            Controls.Add(wear);

            Button takeOff = new Button();
            Ui.StyleButton(takeOff);
            takeOff.SetBounds(272, 246, 136, 28);
            takeOff.Text = "全部取下";
            takeOff.Click += delegate
            {
                cfg.Worn = Accessory.None;
                previewAcc = Accessory.None;
                pet.RefreshAppearance();
                pet.Say("清清爽爽，本鱼本色出演", 4f);
                RefreshList();
            };
            Controls.Add(takeOff);

            anim = new Timer();
            anim.Interval = 120;
            anim.Tick += delegate { animT += 0.12f; preview.Invalidate(); };
            anim.Start();

            RefreshList();
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            PetPose pose = PetPose.Default;
            pose.Time = animT;
            pose.Accessory = previewAcc;
            pose.Squash = 1f + 0.03f * (float)Math.Sin(animT * 2.4);
            pose.Bob = 2f * (float)Math.Sin(animT * 1.7);
            pose.Mood = PetMood.Happy;
            pose.Happy = 0.8f;
            using (Bitmap bmp = CharacterArt.RenderPose(pose, preview.Width, preview.Height))
                e.Graphics.DrawImageUnscaled(bmp, 0, 0);
        }

        private void RefreshList()
        {
            coinLabel.Text = "鱼丸：" + cfg.Coins + " 颗    已收集：" + cfg.Owned.Count + "/" + Art.AccessoryNames.Length + " 种装饰";
            list.Items.Clear();
            for (int i = 0; i < cfg.Owned.Count; i++)
            {
                Accessory a = cfg.Owned[i];
                string nm = Art.AccessoryNames[(int)a];
                list.Items.Add((Rare.ContainsKey(a) ? "★ " : "· ") + nm + (cfg.Worn == a ? "（已戴上）" : ""));
            }
            if (list.Items.Count == 0) list.Items.Add("（背包空空，去扭一颗吧）");
        }

        private void OnSelect()
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= cfg.Owned.Count) return;
            previewAcc = cfg.Owned[i];
            preview.Invalidate();
        }

        private void Wear()
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= cfg.Owned.Count) return;
            Accessory a = cfg.Owned[i];
            cfg.Worn = a;
            previewAcc = a;
            pet.RefreshAppearance();
            pet.Say("戴上「" + Art.AccessoryNames[(int)a] + "」，好看吗？", 4f);
            pet.PlaySfx("coin");
            RefreshList();
        }

        private void Draw()
        {
            if (cfg.Coins <= 0)
            {
                resultLabel.Text = Speech.OnNoCoin();
                pet.Say(Speech.OnNoCoin(), 4.5f);
                pet.PlaySfx("error");
                return;
            }
            cfg.Coins--;
            pet.PlaySfx("gacha");

            int total = Art.AccessoryNames.Length;
            Accessory got = (Accessory)rnd.Next(1, total);
            bool rare = Rare.ContainsKey(got);
            bool dup = cfg.IsOwned(got);

            if (dup)
            {
                cfg.Coins++;
                resultLabel.Text = "重复：" + Art.AccessoryNames[(int)got] + "\n已退还 1 颗鱼丸";
                pet.Say(Speech.OnGacha(false, true), 4.5f);
                pet.PlaySfx("coin");
            }
            else
            {
                cfg.Owned.Add(got);
                previewAcc = got;
                resultLabel.Text = (rare ? "★ 稀有！" : "叮～") + Art.AccessoryNames[(int)got] + "\n已放进背包，可以戴上";
                pet.Say(Speech.OnGacha(rare, false), 4.5f);
                pet.PlaySfx(rare ? "levelup" : "coin");
                pet.AddIntimacy(0.5f);
                if (rare) pet.Notify("大肥鱼扭蛋机", "扭到稀有装饰：" + Art.AccessoryNames[(int)got] + "！");
            }
            cfg.Save();
            RefreshList();
            preview.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    // =====================================================================
    //  闹钟 / 提醒
    // =====================================================================
    public sealed class AlarmForm : CardForm
    {
        private readonly DateTimePicker picker;
        private readonly CheckBox enable;
        private readonly CheckBox daily;
        private readonly Label tip;

        public AlarmForm(Settings c, PetForm p) : base(c, p, "大肥鱼闹钟", new Size(320, 210))
        {
            picker = new DateTimePicker();
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;
            picker.Font = Ui.F(16f, true);
            picker.SetBounds(12, 40, 120, 34);
            Controls.Add(picker);

            enable = new CheckBox();
            Ui.StyleCheck(enable);
            enable.Text = "启用闹钟";
            enable.SetBounds(150, 44, 120, 24);
            enable.CheckedChanged += delegate
            {
                cfg.AlarmEnabled = enable.Checked;
                cfg.AlarmTime = picker.Value.ToString("HH:mm");
                tip.Text = cfg.AlarmEnabled ? ("已设定 " + cfg.AlarmTime + (cfg.AlarmDaily ? "（每天）" : "（一次）")) : "闹钟未启用";
                cfg.Save();
            };
            Controls.Add(enable);

            daily = new CheckBox();
            Ui.StyleCheck(daily);
            daily.Text = "每天重复";
            daily.SetBounds(150, 70, 120, 24);
            daily.CheckedChanged += delegate
            {
                cfg.AlarmDaily = daily.Checked;
                cfg.AlarmTime = picker.Value.ToString("HH:mm");
                cfg.AlarmEnabled = enable.Checked;
                cfg.Save();
            };
            Controls.Add(daily);

            tip = new Label();
            Ui.StyleLabel(tip, true);
            tip.SetBounds(12, 106, Width - 24, 40);
            Controls.Add(tip);

            Button test = new Button();
            Ui.StyleButton(test);
            test.SetBounds(12, 150, 100, 30);
            test.Text = "试听铃声";
            test.Click += delegate { p.PlaySfx("alarm"); p.Say(Speech.OnAlarm(), 4f); };
            Controls.Add(test);

            Button clear = new Button();
            Ui.StyleButton(clear);
            clear.SetBounds(122, 150, 100, 30);
            clear.Text = "清除闹钟";
            clear.Click += delegate
            {
                cfg.AlarmEnabled = false;
                cfg.AlarmTime = "";
                enable.Checked = false;
                tip.Text = "闹钟已清除";
                cfg.Save();
            };
            Controls.Add(clear);

            Button close = new Button();
            Ui.StyleButton(close);
            close.SetBounds(222, 150, 86, 30);
            close.Text = "关闭";
            close.Click += delegate { Hide(); };
            Controls.Add(close);

            SyncFromConfig();
        }

        public override void SyncFromConfig()
        {
            if (!string.IsNullOrEmpty(cfg.AlarmTime))
            {
                DateTime t;
                if (DateTime.TryParse(DateTime.Now.ToString("yyyy-MM-dd") + " " + cfg.AlarmTime, out t))
                    picker.Value = t;
            }
            enable.Checked = cfg.AlarmEnabled;
            daily.Checked = cfg.AlarmDaily;
            tip.Text = cfg.AlarmEnabled ? ("已设定 " + cfg.AlarmTime + (cfg.AlarmDaily ? "（每天）" : "（一次）")) : "闹钟未启用";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    // =====================================================================
    //  今日鱼报
    // =====================================================================
    public sealed class ReportForm : CardForm
    {
        private readonly TextBox text;

        public ReportForm(Settings c, PetForm p) : base(c, p, "大肥鱼今日鱼报", new Size(400, 340))
        {
            text = new TextBox();
            Ui.StyleText(text, true);
            text.ReadOnly = true;
            text.SetBounds(12, 34, Width - 24, 236);
            Controls.Add(text);

            Button copy = new Button();
            Ui.StyleButton(copy);
            copy.SetBounds(12, 280, 110, 30);
            copy.Text = "复制";
            copy.Click += delegate { try { Clipboard.SetText(text.Text); } catch { } };
            Controls.Add(copy);

            Button save = new Button();
            Ui.StyleButton(save);
            save.SetBounds(132, 280, 130, 30);
            save.Text = "保存为 txt";
            save.Click += delegate { SaveReport(); };
            Controls.Add(save);

            Button reset = new Button();
            Ui.StyleButton(reset);
            reset.SetBounds(272, 280, 116, 30);
            reset.Text = "重置今日摸鱼";
            reset.Click += delegate
            {
                cfg.TodaySlackMinutes = 0;
                cfg.Save();
                RefreshReport();
            };
            Controls.Add(reset);

            RefreshReport();
        }

        public void RefreshReport()
        {
            text.Text = Speech.DailyReport(Monitor.Latest, cfg);
        }

        private void SaveReport()
        {
            try
            {
                string dir = Path.Combine(Settings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "鱼报_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt");
                File.WriteAllText(file, text.Text, Encoding.UTF8);
                pet.Say("鱼报已保存到 " + file, 5f);
                try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
            }
            catch (Exception ex)
            {
                pet.Say("保存失败：" + ex.Message, 5f);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    // =====================================================================
    //  表情包生成器
    // =====================================================================
    public sealed class MemeForm : CardForm
    {
        private ComboBox moodBox;
        private ComboBox accBox;
        private ComboBox tplBox;
        private TextBox topText;
        private TextBox bottomText;
        private PictureBox preview;
        private readonly Timer anim;
        private float animT;

        private static readonly string[] Templates = new string[] {
            "摸鱼中，勿扰|有事明天说",
            "本鱼只吃白饭|不干活",
            "工作嘛|差不多得了",
            "CPU 好烫|谁在烤鱼",
            "电量不足|请投喂白饭",
            "专注？|先摸五分钟",
            "铁盆扒饭中|谁也别抢",
            "我这一盆|谁也端不走",
            "自定义|自定义"
        };

        private static readonly PetMood[] Moods = new PetMood[] {
            PetMood.Idle, PetMood.Happy, PetMood.Surprised, PetMood.Sleepy, PetMood.Sleep,
            PetMood.Eat, PetMood.EatRice, PetMood.Dizzy, PetMood.Sad, PetMood.Love, PetMood.Sick, PetMood.Cheer, PetMood.Focus
        };

        private static readonly string[] MoodNames = new string[] {
            "发呆", "开心", "震惊", "犯困", "睡着", "吃小鱼干", "铁盆扒白饭", "转晕", "委屈", "喜欢", "发烧", "欢呼", "专注"
        };

        public MemeForm(Settings c, PetForm p) : base(c, p, "大肥鱼表情包", new Size(420, 320))
        {
            Label l1 = new Label();
            Ui.StyleLabel(l1, true);
            l1.Text = "模板";
            l1.SetBounds(12, 34, 40, 20);
            Controls.Add(l1);

            tplBox = new ComboBox();
            Ui.StyleCombo(tplBox);
            for (int i = 0; i < Templates.Length; i++) tplBox.Items.Add(Templates[i].Replace("|", " / "));
            tplBox.SetBounds(56, 32, 220, 24);
            tplBox.SelectedIndexChanged += delegate
            {
                if (topText == null || preview == null || tplBox.SelectedIndex < 0) return;
                string[] parts = Templates[tplBox.SelectedIndex].Split('|');
                topText.Text = parts[0];
                bottomText.Text = parts[1];
                preview.Invalidate();
            };
            Controls.Add(tplBox);

            Label l2 = new Label();
            Ui.StyleLabel(l2, true);
            l2.Text = "情绪";
            l2.SetBounds(12, 64, 40, 20);
            Controls.Add(l2);

            moodBox = new ComboBox();
            Ui.StyleCombo(moodBox);
            for (int i = 0; i < MoodNames.Length; i++) moodBox.Items.Add(MoodNames[i]);
            moodBox.SetBounds(56, 62, 100, 24);
            moodBox.SelectedIndexChanged += delegate { if (preview != null) preview.Invalidate(); };
            Controls.Add(moodBox);

            Label l3 = new Label();
            Ui.StyleLabel(l3, true);
            l3.Text = "装扮";
            l3.SetBounds(166, 64, 40, 20);
            Controls.Add(l3);

            accBox = new ComboBox();
            Ui.StyleCombo(accBox);
            for (int i = 0; i < Art.AccessoryNames.Length; i++) accBox.Items.Add(Art.AccessoryNames[i]);
            accBox.SetBounds(210, 62, 140, 24);
            accBox.SelectedIndexChanged += delegate { if (preview != null) preview.Invalidate(); };
            Controls.Add(accBox);

            topText = new TextBox();
            Ui.StyleText(topText, false);
            topText.SetBounds(12, 92, 200, 24);
            topText.Text = "摸鱼中，勿扰";
            topText.TextChanged += delegate { if (preview != null) preview.Invalidate(); };
            Controls.Add(topText);

            bottomText = new TextBox();
            Ui.StyleText(bottomText, false);
            bottomText.SetBounds(220, 92, 186, 24);
            bottomText.Text = "有事明天说";
            bottomText.TextChanged += delegate { if (preview != null) preview.Invalidate(); };
            Controls.Add(bottomText);

            preview = new PictureBox();
            preview.BackColor = Ui.Bg;
            preview.BorderStyle = BorderStyle.FixedSingle;
            preview.SetBounds(12, 124, 240, 180);
            preview.Paint += PreviewPaint;
            Controls.Add(preview);

            Button save = new Button();
            Ui.StyleButton(save);
            save.SetBounds(262, 124, 144, 32);
            save.Text = "保存 PNG";
            save.Click += delegate { SaveMeme(); };
            Controls.Add(save);

            Button folder = new Button();
            Ui.StyleButton(folder);
            folder.SetBounds(262, 162, 144, 32);
            folder.Text = "打开文件夹";
            folder.Click += delegate { OpenFolder(); };
            Controls.Add(folder);

            Button talk = new Button();
            Ui.StyleButton(talk);
            talk.SetBounds(262, 200, 144, 32);
            talk.Text = "让它说这句";
            talk.Click += delegate { pet.Say(topText.Text + "\n" + bottomText.Text, 5f); };
            Controls.Add(talk);

            Label hint = new Label();
            Ui.StyleLabel(hint, true);
            hint.SetBounds(262, 238, 150, 60);
            hint.Text = "保存位置：\n我的图片\\大肥鱼表情包";
            Controls.Add(hint);

            // 全部控件建好后再设置初始选项，避免触发还没有控件可用的回调
            tplBox.SelectedIndex = 0;
            moodBox.SelectedIndex = 1;
            accBox.SelectedIndex = 0;

            anim = new Timer();
            anim.Interval = 120;
            anim.Tick += delegate { animT += 0.12f; preview.Invalidate(); };
            anim.Start();
        }

        private PetPose CurrentPose()
        {
            PetPose pose = PetPose.Default;
            pose.Time = animT;
            pose.Mood = Moods[Math.Max(0, Math.Min(Moods.Length - 1, moodBox.SelectedIndex))];
            pose.Accessory = (Accessory)Math.Max(0, Math.Min(Art.AccessoryNames.Length - 1, accBox.SelectedIndex));
            pose.Happy = pose.Mood == PetMood.Happy ? 1f : 0.4f;
            pose.Squash = 1f + 0.03f * (float)Math.Sin(animT * 2.4);
            pose.Bob = 2f * (float)Math.Sin(animT * 1.7);
            if (pose.Mood == PetMood.Sleep) pose.Blink = 1f;
            if (pose.Mood == PetMood.Eat) pose.MouthOpen = 0.8f;
            if (pose.Mood == PetMood.EatRice)
            {
                pose.MouthOpen = 0.85f;
                pose.Rice = 1f;
                pose.Happy = 0.95f;
            }
            if (pose.Mood == PetMood.Surprised) pose.MouthOpen = 0.7f;
            if (pose.Mood == PetMood.Cheer) pose.MouthOpen = 0.8f;
            return pose;
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            RenderMeme(e.Graphics, preview.Width, preview.Height, CurrentPose(), topText.Text, bottomText.Text, false);
        }

        private static void RenderMeme(Graphics g, int w, int h, PetPose pose, string top, string bottom, bool withBackground)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (withBackground)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                    Color.FromArgb(255, 246, 250, 255), Color.FromArgb(255, 214, 232, 252), 90f))
                    g.FillRectangle(b, 0, 0, w, h);
            }

            int fishH = (int)(h * 0.66f);
            int fishW = fishH;
            RectangleF box = new RectangleF((w - fishW) / 2f, h * 0.18f, fishW, fishH);
            CharacterArt.Draw(g, box, pose);

            DrawMemeLine(g, top, new RectangleF(4, 4, w - 8, h * 0.16f), h * 0.085f);
            DrawMemeLine(g, bottom, new RectangleF(4, h - h * 0.18f - 4, w - 8, h * 0.18f), h * 0.085f);
        }

        private static void DrawMemeLine(Graphics g, string text, RectangleF area, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (Font f = new Font("Microsoft YaHei UI", Math.Max(9f, fontSize), FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush outline = new SolidBrush(Color.FromArgb(255, 20, 20, 20)))
                {
                    float[] dx = new float[] { -2, 0, 2, 0, -1.5f, 1.5f, -1.5f, 1.5f };
                    float[] dy = new float[] { 0, -2, 0, 2, -1.5f, -1.5f, 1.5f, 1.5f };
                    for (int i = 0; i < dx.Length; i++)
                        g.DrawString(text, f, outline, new RectangleF(area.X + dx[i], area.Y + dy[i], area.Width, area.Height), sf);
                }
                using (SolidBrush fill = new SolidBrush(Color.White))
                    g.DrawString(text, f, fill, area, sf);
            }
        }

        private static string TargetDir()
        {
            string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return Path.Combine(pics, "大肥鱼表情包");
        }

        private void SaveMeme()
        {
            try
            {
                string dir = TargetDir();
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "大肥鱼_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                using (Bitmap bmp = new Bitmap(600, 600, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        RenderMeme(g, 600, 600, CurrentPose(), topText.Text, bottomText.Text, true);
                    bmp.Save(file, ImageFormat.Png);
                }
                pet.Say("表情包已保存：" + file, 5f);
                pet.PlaySfx("levelup");
                try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
            }
            catch (Exception ex)
            {
                pet.Say("保存失败：" + ex.Message, 5f);
            }
        }

        private void OpenFolder()
        {
            try
            {
                string dir = TargetDir();
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    // =====================================================================
    //  设置
    // =====================================================================
    public sealed class SettingsForm : CardForm
    {
        private readonly TrackBar scaleTrack;
        private readonly Label scaleLabel;
        private readonly TrackBar opaTrack;
        private readonly Label opaLabel;
        private readonly TrackBar volTrack;
        private readonly Label volLabel;
        private readonly CheckBox topMost;
        private readonly CheckBox walk;
        private readonly CheckBox follow;
        private readonly CheckBox through;
        private readonly CheckBox hideFs;
        private readonly CheckBox simple;
        private readonly CheckBox mute;
        private readonly CheckBox startup;
        private bool loading;

        public SettingsForm(Settings c, PetForm p) : base(c, p, "大肥鱼设置", new Size(400, 400))
        {
            loading = true;

            scaleLabel = new Label();
            Ui.StyleLabel(scaleLabel, false);
            scaleLabel.SetBounds(12, 34, 200, 20);
            Controls.Add(scaleLabel);

            scaleTrack = new TrackBar();
            Ui.StyleTrack(scaleTrack);
            scaleTrack.Minimum = 35;
            scaleTrack.Maximum = 250;
            scaleTrack.Value = (int)Math.Round(cfg.Scale * 100);
            scaleTrack.SetBounds(8, 54, Width - 24, 26);
            scaleTrack.ValueChanged += delegate
            {
                if (loading) return;
                cfg.Scale = scaleTrack.Value / 100f;
                scaleLabel.Text = "大小：" + scaleTrack.Value + "%";
                pet.RefreshAppearance();
            };
            Controls.Add(scaleTrack);

            opaLabel = new Label();
            Ui.StyleLabel(opaLabel, false);
            opaLabel.SetBounds(12, 84, 200, 20);
            Controls.Add(opaLabel);

            opaTrack = new TrackBar();
            Ui.StyleTrack(opaTrack);
            opaTrack.Minimum = 30;
            opaTrack.Maximum = 100;
            opaTrack.Value = (int)Math.Round(cfg.Opacity * 100);
            opaTrack.SetBounds(8, 104, Width - 24, 26);
            opaTrack.ValueChanged += delegate
            {
                if (loading) return;
                cfg.Opacity = opaTrack.Value / 100f;
                opaLabel.Text = "不透明度：" + opaTrack.Value + "%";
                pet.RefreshAppearance();
            };
            Controls.Add(opaTrack);

            volLabel = new Label();
            Ui.StyleLabel(volLabel, false);
            volLabel.SetBounds(12, 134, 200, 20);
            Controls.Add(volLabel);

            volTrack = new TrackBar();
            Ui.StyleTrack(volTrack);
            volTrack.Minimum = 0;
            volTrack.Maximum = 100;
            volTrack.Value = cfg.Volume;
            volTrack.SetBounds(8, 154, Width - 24, 26);
            volTrack.ValueChanged += delegate
            {
                if (loading) return;
                cfg.Volume = volTrack.Value;
                cfg.Muted = volTrack.Value == 0;
                Audio.Volume = cfg.Volume / 100f;
                Audio.Muted = cfg.Muted;
                volLabel.Text = "音量：" + cfg.Volume + "%";
                mute.Checked = cfg.Muted;
            };
            Controls.Add(volTrack);

            topMost = MakeCheck("总在最前", cfg.TopMost, 12, 190, delegate
            {
                cfg.TopMost = topMost.Checked;
                pet.RefreshAppearance();
            });
            walk = MakeCheck("桌面漫游", cfg.WalkAround, 130, 190, delegate { cfg.WalkAround = walk.Checked; });
            follow = MakeCheck("跟随鼠标", cfg.FollowMouse, 248, 190, delegate { cfg.FollowMouse = follow.Checked; });
            through = MakeCheck("鼠标穿透", cfg.MouseThrough, 12, 216, delegate
            {
                cfg.MouseThrough = through.Checked;
                pet.RefreshAppearance();
            });
            hideFs = MakeCheck("全屏时隐身", cfg.HideFullscreen, 130, 216, delegate { cfg.HideFullscreen = hideFs.Checked; });
            simple = MakeCheck("简约模式", cfg.SimpleMode, 248, 216, delegate
            {
                cfg.SimpleMode = simple.Checked;
                if (cfg.SimpleMode) { pet.RefreshAppearance(); }
                else { pet.OpenWidgets(); }
            });
            mute = MakeCheck("静音", cfg.Muted, 12, 242, delegate
            {
                cfg.Muted = mute.Checked;
                Audio.Muted = cfg.Muted;
            });
            startup = MakeCheck("开机自动启动", Settings.IsStartupEnabled(), 130, 242, delegate
            {
                Settings.SetStartup(startup.Checked);
            });

            Label hint = new Label();
            Ui.StyleLabel(hint, true);
            hint.SetBounds(12, 272, Width - 24, 60);
            hint.Text = "提示：左键拖动桌宠，滚轮缩放，右键打开全部菜单。\n配置与存档：" + Settings.DataDir;
            Controls.Add(hint);

            Button reset = new Button();
            Ui.StyleButton(reset);
            reset.SetBounds(12, 336, 110, 30);
            reset.Text = "恢复默认";
            reset.Click += delegate { ResetDefaults(); };
            Controls.Add(reset);

            Button openDir = new Button();
            Ui.StyleButton(openDir);
            openDir.SetBounds(132, 336, 130, 30);
            openDir.Text = "打开配置目录";
            openDir.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Settings.DataDir + "\""); } catch { }
            };
            Controls.Add(openDir);

            Button ok = new Button();
            Ui.StyleButton(ok);
            ok.SetBounds(272, 336, 116, 30);
            ok.Text = "保存并关闭";
            ok.Click += delegate
            {
                cfg.Save();
                Hide();
                pet.Say("设置已保存～", 3.5f);
            };
            Controls.Add(ok);

            loading = false;
            RefreshLabels();
        }

        private CheckBox MakeCheck(string text, bool value, int x, int y, EventHandler h)
        {
            CheckBox cb = new CheckBox();
            Ui.StyleCheck(cb);
            cb.Text = text;
            cb.Checked = value;
            cb.SetBounds(x, y, 116, 24);
            cb.CheckedChanged += h;
            Controls.Add(cb);
            return cb;
        }

        private void RefreshLabels()
        {
            scaleLabel.Text = "大小：" + (int)Math.Round(cfg.Scale * 100) + "%";
            opaLabel.Text = "不透明度：" + (int)Math.Round(cfg.Opacity * 100) + "%";
            volLabel.Text = "音量：" + cfg.Volume + "%";
        }

        private void ResetDefaults()
        {
            loading = true;
            cfg.Scale = 1f;
            cfg.Opacity = 1f;
            cfg.Volume = 70;
            cfg.Muted = false;
            cfg.TopMost = true;
            cfg.WalkAround = true;
            cfg.FollowMouse = false;
            cfg.MouseThrough = false;
            cfg.HideFullscreen = true;
            cfg.SimpleMode = false;
            scaleTrack.Value = 100;
            opaTrack.Value = 100;
            volTrack.Value = 70;
            topMost.Checked = true;
            walk.Checked = true;
            follow.Checked = false;
            through.Checked = false;
            hideFs.Checked = true;
            simple.Checked = false;
            mute.Checked = false;
            loading = false;
            RefreshLabels();
            cfg.Save();
            pet.RefreshAppearance();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                cfg.Save();
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
