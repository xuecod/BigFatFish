// PetForm.cs —— 大肥鱼桌宠主窗体：逐像素透明 + 常驻顶层 + 漫游 + 鼠标交互 + 右键菜单 + 托盘
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FishPet
{
    public sealed partial class PetForm : Form, IPetHost
    {
        // ---------- 常量 ----------
        private const int FishWBase = 190;
        private const int FishHBase = 172;
        private const int PadBase = 30;
        private const int BubbleAreaBase = 124;
        private const int BottomPadBase = 12;

        // ---------- 运行部件 ----------
        internal Settings cfg;
        private LayeredCanvas canvas;
        private System.Windows.Forms.Timer frameTimer;
        private System.Windows.Forms.Timer slowTimer;
        private NotifyIcon tray;
        private ContextMenuStrip menu;
        private Icon trayIcon;
        private readonly Stopwatch clock = Stopwatch.StartNew();

        // ---------- 几何 ----------
        private int fishW, fishH, padX, bubbleArea, bottomPad, canvasW, canvasH;
        internal float X, Y;
        internal float VX, VY;
        internal bool facingLeft;

        // ---------- 动画 / 状态 ----------
        internal PetState state = PetState.Idle;
        internal float stateTime;
        internal float stateDuration = -1f;
        internal bool microAction;
        private float blinkCountdown = 3f;
        private float blinkAnim;
        private int blinkStage;
        private float nextMicro = 22f;
        private float breathT;

        // ---------- 气泡 ----------
        internal string bubbleText = "";
        internal float bubbleLeft;
        private Font bubbleFont;
        private string measuredText = "";
        private int measuredMaxW = -1;
        private SizeF measuredSize = SizeF.Empty;
        private readonly Dictionary<string, float> sayCooldown = new Dictionary<string, float>();

        // ---------- 交互 ----------
        private bool dragging;
        private Point dragOffset;
        private Point dragStartScreen;
        private bool dragMoved;
        private bool manualHidden;
        private bool fullscreenHidden;

        // ---------- 统计 / 通知节流 ----------
        private float coinAcc, saveAcc;
        private float batteryNotifyCd, hotNotifyCd, hungryCd;
        private bool lastCharging;
        private float hudHintLeft;
        private string hudHint = "";
        private int topmostTick;

        // ---------- 面板 ----------
        private WidgetsForm widgets;
        private PomodoroForm pomodoro;
        private GachaForm gacha;
        private SettingsForm settingsForm;
        private AlarmForm alarmForm;
        private ReportForm reportForm;
        private MemeForm memeForm;

        // ---------- 字体 ----------
        private Font hudFont;

        // ---------- 自检导出 ----------
        /// <summary>非空则启动后把当前帧导出为 PNG 并退出（走真实绘制管线）</summary>
        public string ShotPath;
        public string ShotPose = "idle";
        /// <summary>非空则启动后写一份窗口/渲染诊断报告并退出</summary>
        public string DiagPath;
        internal bool riceFreeze;
        internal bool shotMode;

        public PetForm(Settings settings, bool silentStart)
        {
            cfg = settings;
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = cfg.TopMost;
            Text = "大肥鱼桌宠";
            SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = false;
            Relayout();
            ResumeLayout(false);
            // 画布必须在 OnHandleCreated 里绑定句柄，避免句柄重建后 UpdateLayeredWindow 画到已销毁窗口

            Screen sc = Screen.PrimaryScreen;
            float startX;
            float startY;
            if (cfg.PosX >= 0 && cfg.PosY >= 0 && IsOnAnyScreen(cfg.PosX, cfg.PosY))
            {
                startX = cfg.PosX;
                startY = cfg.PosY;
            }
            else
            {
                startX = sc.WorkingArea.Left + (sc.WorkingArea.Width - canvasW) / 2f;
                startY = GroundY(sc) - FishBottomOffset();
            }
            X = startX; Y = startY;
            Location = new Point((int)Math.Round(X), (int)Math.Round(Y));

            frameTimer = new System.Windows.Forms.Timer();
            frameTimer.Interval = 33;
            frameTimer.Tick += FrameTick;

            slowTimer = new System.Windows.Forms.Timer();
            slowTimer.Interval = 1000;
            slowTimer.Tick += SlowTick;

            InitTray();
            ApplyMouseThrough();

            if (!cfg.SimpleMode) InitWidgets();

            if (!silentStart) Say(Speech.Greeting(), 6f);
            OnStartupChecks();
        }

        internal int FishBottomOffset() { return bubbleArea + fishH; }

        // ==================== 布局 / 外观 ====================

        internal void Relayout()
        {
            float sc = cfg.Scale;
            fishW = (int)Math.Round(FishWBase * sc);
            fishH = (int)Math.Round(FishHBase * sc);
            padX = (int)Math.Round(PadBase * sc);
            bubbleArea = (int)Math.Round(BubbleAreaBase * sc);
            bottomPad = (int)Math.Round(BottomPadBase * sc);
            canvasW = fishW + padX * 2;
            canvasH = bubbleArea + fishH + bottomPad;
            bubbleFont = null;
            hudFont = null;
        }

        internal RectangleF FishRect
        {
            get { return new RectangleF(padX, bubbleArea, fishW, fishH); }
        }

        public void RefreshAppearance()
        {
            float bottomCenterX = X + canvasW / 2f;
            float bottomCenterY = Y + FishBottomOffset();
            Relayout();
            if (canvas != null)
            {
                canvas.Resize(canvasW, canvasH);
                canvas.GlobalAlpha = (int)Math.Round(255 * cfg.Opacity);
            }
            Size = new Size(canvasW, canvasH);
            TopMost = cfg.TopMost;
            ApplyMouseThrough();
            X = bottomCenterX - canvasW / 2f;
            Y = bottomCenterY - FishBottomOffset();
            ClampToScreen();
            if (widgets != null) widgets.SyncFromConfig();
            Render(true);
        }

        private void ApplyMouseThrough()
        {
            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            if (cfg.MouseThrough) ex |= Native.WS_EX_TRANSPARENT;
            else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (canvas != null)
            {
                canvas.Dispose();
                canvas = null;
            }
            canvas = new LayeredCanvas(Handle, canvasW, canvasH);
            canvas.GlobalAlpha = (int)Math.Round(255 * cfg.Opacity);
            Size = new Size(canvasW, canvasH);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (canvas != null)
            {
                canvas.Dispose();
                canvas = null;
            }
            base.OnHandleDestroyed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 启动主循环：渲染/动画（33ms）+ 系统联动与统计（1s）
            if (frameTimer != null && !frameTimer.Enabled) frameTimer.Start();
            if (slowTimer != null && !slowTimer.Enabled) slowTimer.Start();

            if (!string.IsNullOrEmpty(DiagPath))
            {
                System.Windows.Forms.Timer diag = new System.Windows.Forms.Timer();
                diag.Interval = 1500;
                diag.Tick += delegate
                {
                    diag.Stop();
                    try
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        Native.RECT r;
                        Native.GetWindowRect(Handle, out r);
                        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                        Rectangle b = Screen.PrimaryScreen.Bounds;
                        sb.AppendLine("canvasW x canvasH = " + canvasW + " x " + canvasH);
                        sb.AppendLine("form.Size = " + Size.Width + " x " + Size.Height);
                        sb.AppendLine("window rect = (" + r.Left + "," + r.Top + ")-(" + r.Right + "," + r.Bottom + ") = " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top));
                        sb.AppendLine("X,Y = " + X.ToString("0") + "," + Y.ToString("0"));
                        sb.AppendLine("screen bounds = " + b.Width + "x" + b.Height + "  workarea bottom=" + wa.Bottom);
                        sb.AppendLine("visible = " + Visible + "  topmost = " + TopMost + "  scale = " + cfg.Scale);
                        sb.AppendLine("commit ok = " + canvas.LastCommitSucceeded + "  err = " + canvas.LastCommitError + "  count = " + canvas.CommitCount);
                        sb.AppendLine("state = " + state + "  bubble = '" + bubbleText + "'");
                        sb.AppendLine("dataDir = " + Settings.DataDir);
                        File.WriteAllText(DiagPath, sb.ToString(), System.Text.Encoding.UTF8);
                        if (canvas != null)
                        {
                            using (Bitmap snap = canvas.SnapshotFromDib())
                                snap.Save(Path.ChangeExtension(DiagPath, ".png"), System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    catch { }
                    Application.Exit();
                };
                diag.Start();
                return;
            }

            if (string.IsNullOrEmpty(ShotPath)) return;
            ApplyShotPose();
            System.Windows.Forms.Timer shotTimer = new System.Windows.Forms.Timer();
            shotTimer.Interval = 1600;
            shotTimer.Tick += delegate
            {
                shotTimer.Stop();
                try
                {
                    using (Bitmap b = RenderSceneBitmap(canvasW * 2, canvasH * 2))
                        b.Save(ShotPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                catch { }
                Application.Exit();
            };
            shotTimer.Start();
        }

        private void ApplyShotPose()
        {
            shotMode = true;
            string p = ShotPose == null ? "idle" : ShotPose.ToLowerInvariant();
            if (p == "rice")
            {
                riceFreeze = true;
                EnterState(PetState.EatRice, 30f, false);
                riceLevel = 1f;
                Say(Speech.OnRice(), 30f);
            }
            else if (p == "walk") { EnterState(PetState.Walk, 30f, false); Say("游动中～", 30f); }
            else if (p == "sleep") { EnterState(PetState.Sleep, -1f, false); Say("呼……呼……", 30f); }
            else if (p == "focus") { EnterState(PetState.Focus, -1f, false); Say("专注中，别打扰我", 30f); }
            else if (p == "drag") { EnterState(PetState.Drag, -1f, false); Say("放我下来！", 30f); }
            else if (p == "sick") { ForceMood(PetMood.Sick, 30f); Say(Speech.Sick(), 30f); }
            else { Say(Speech.Greeting(), 30f); }
            bubbleLeft = 30f;
        }

        internal Screen CurrentScreen()
        {
            Point center = new Point((int)Math.Round(X + canvasW / 2f), (int)Math.Round(Y + FishBottomOffset() - fishH / 2f));
            return Screen.FromPoint(center);
        }

        internal float GroundY(Screen sc)
        {
            return sc.WorkingArea.Bottom - 4f * cfg.Scale;
        }

        private static bool IsOnAnyScreen(int x, int y)
        {
            Screen[] all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
            {
                Rectangle r = all[i].Bounds;
                if (x >= r.Left - 400 && x <= r.Right + 400 && y >= r.Top - 200 && y <= r.Bottom + 200) return true;
            }
            return false;
        }

        internal void ClampToScreen()
        {
            Screen sc = CurrentScreen();
            Rectangle wa = sc.WorkingArea;
            float minX = wa.Left - canvasW * 0.35f;
            float maxX = wa.Right - canvasW * 0.65f;
            if (X < minX) X = minX;
            if (X > maxX) X = maxX;
            float top = wa.Top - bubbleArea * 0.5f;
            float bottom = wa.Bottom - 8f * cfg.Scale;
            if (Y < top) Y = top;
            if (Y > bottom) Y = bottom;
        }

        // ==================== 主循环 ====================

        private void FrameTick(object sender, EventArgs e)
        {
            float dt = 1f / 33f;
            UpdateBrain(dt);
            Render(false);
        }

        private void SlowTick(object sender, EventArgs e)
        {
            ReactToSystem();
            AccumulateStats(1f);
            TickAlarm();
            UpdateHiddenState();

            // 每 2 秒重申一次置顶：别的置顶窗口不该把桌宠压住
            topmostTick++;
            if (cfg.TopMost && topmostTick % 2 == 0 && Visible)
            {
                if (cfg.MouseThrough) topmostTick++;
                BringToFrontSafe();
            }

            saveAcc += 1f;
            if (saveAcc >= 60f)
            {
                saveAcc = 0f;
                SaveNow();
            }
        }

        private void SaveNow()
        {
            cfg.PosX = (int)Math.Round(X);
            cfg.PosY = (int)Math.Round(Y);
            cfg.Save();
        }

        // ==================== 绘制 ====================

        private Font BubbleFont
        {
            get
            {
                if (bubbleFont == null)
                {
                    float size = (float)Math.Round(10.0 * Math.Max(0.75, Math.Min(2.0, cfg.Scale)), 1);
                    bubbleFont = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point);
                }
                return bubbleFont;
            }
        }

        private Font HudFont
        {
            get
            {
                if (hudFont == null)
                {
                    float size = (float)Math.Round(8.5 * Math.Max(0.75, Math.Min(2.0, cfg.Scale)), 1);
                    hudFont = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Point);
                }
                return hudFont;
            }
        }

        private void Render(bool force)
        {
            if (canvas == null) return;
            if (!Visible && !force) return;
            try { DrawScene(canvas.Graphics); }
            catch { }
            // 无论绘制过程中出什么岔子，都要把这一帧提交上去，否则屏幕上会什么都没有
            try { canvas.Commit((int)Math.Round(X), (int)Math.Round(Y)); }
            catch { }
        }

        /// <summary>把当前这一帧画到给定的 Graphics 上（画布坐标系 = canvasW × canvasH）</summary>
        internal void DrawScene(Graphics g)
        {
            g.Clear(Color.Transparent);

            RectangleF fishRect = FishRect;
            PetPose pose = BuildPose();

            float groundY = GroundY(CurrentScreen());
            float feetY = Y + FishBottomOffset();
            float closeness = 1f - (groundY - feetY) / (240f * Math.Max(0.5f, cfg.Scale));
            if (closeness > 0.03f && CharacterArt.WantShadow)
            {
                if (closeness > 1f) closeness = 1f;
                if (!dragging || state != PetState.Drag) Art.DrawGroundShadow(g, fishRect, closeness);
            }

            CharacterArt.Draw(g, fishRect, pose);

            if (!cfg.SimpleMode && cfg.SpeechEnabled && bubbleText.Length > 0 && bubbleLeft > 0f)
            {
                try
                {
                    int maxTextW = Math.Max(90, canvasW - 28);
                    if (measuredText != bubbleText || measuredMaxW != maxTextW)
                    {
                        measuredText = bubbleText;
                        measuredMaxW = maxTextW;
                        measuredSize = BubbleArt.Measure(g, bubbleText, BubbleFont, maxTextW);
                    }
                    SizeF need = measuredSize;
                    float bw = Math.Min(Math.Max(need.Width, 60f), canvasW - 4f);
                    float bh = Math.Min(Math.Max(need.Height, 30f), bubbleArea - 4f);
                    float bx = (canvasW - bw) / 2f;
                    float by = bubbleArea - bh - 2f;
                    if (by < 1f) by = 1f;
                    PointF tail = new PointF(canvasW / 2f + (facingLeft ? -fishW * 0.10f : fishW * 0.10f), by + bh);
                    BubbleArt.Draw(g, bubbleText, BubbleFont, new RectangleF(bx, by, bw, bh), tail, facingLeft);
                }
                catch { }
            }

            if (hudHintLeft > 0f && hudHint.Length > 0)
            {
                Font f = HudFont;
                SizeF sz = g.MeasureString(hudHint, f);
                float w = sz.Width + 16f;
                float h = sz.Height + 8f;
                float cx = canvasW / 2f - w / 2f;
                float cy = bubbleArea + fishH * 0.30f;
                using (System.Drawing.Drawing2D.GraphicsPath gp = Round(new RectangleF(cx, cy, w, h), h / 2f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 20, 30, 52)))
                using (Pen p = new Pen(Color.FromArgb(160, 120, 170, 255), 1f))
                {
                    g.FillPath(b, gp);
                    g.DrawPath(p, gp);
                }
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 235, 245, 255)))
                    g.DrawString(hudHint, f, tb, cx + 8f, cy + 4f);
            }
        }

        /// <summary>把当前帧渲染成放大后的位图（自检用，走与屏幕完全相同的绘制路径）</summary>
        internal Bitmap RenderSceneBitmap(int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.ScaleTransform((float)width / canvasW, (float)height / canvasH);
                DrawScene(g);
            }
            return bmp;
        }

        internal static System.Drawing.Drawing2D.GraphicsPath Round(RectangleF r, float radius)
        {
            System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            gp.AddArc(r.X, r.Y, d, d, 180f, 90f);
            gp.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            gp.CloseFigure();
            return gp;
        }

        // ==================== 鼠标交互 ====================

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks >= 2)
                {
                    HandleDoubleClick();
                    dragging = false;
                    return;
                }
                dragging = true;
                dragMoved = false;
                dragStartScreen = Cursor.Position;
                dragOffset = new Point(Cursor.Position.X - (int)Math.Round(X), Cursor.Position.Y - (int)Math.Round(Y));
                VX = 0f; VY = 0f;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                int dx = Math.Abs(Cursor.Position.X - dragStartScreen.X);
                int dy = Math.Abs(Cursor.Position.Y - dragStartScreen.Y);
                if (!dragMoved && dx + dy > 4)
                {
                    dragMoved = true;
                    EnterState(PetState.Drag, -1f, false);
                    Say(Speech.OnDragStart(), 2.5f);
                    PlaySfx("hiccup");
                }
                if (dragMoved)
                {
                    X = Cursor.Position.X - dragOffset.X;
                    Y = Cursor.Position.Y - dragOffset.Y;
                    if (Cursor.Position.X < dragStartScreen.X - 6) facingLeft = true;
                    else if (Cursor.Position.X > dragStartScreen.X + 6) facingLeft = false;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            bool wasDrag = dragMoved;
            dragging = false;
            dragMoved = false;
            if (wasDrag)
            {
                VY = 30f;
                EnterState(PetState.Sink, -1f, false);
                Say(Speech.OnDrop(), 2.5f);
                PlaySfx("pop");
                SaveNow();
            }
            else
            {
                OnPetClicked();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float step = e.Delta > 0 ? 0.1f : -0.1f;
            cfg.Scale = Settings.Clamp(cfg.Scale + step, 0.35f, 2.5f);
            RefreshAppearance();
            ShowHud("大小 " + ((int)Math.Round(cfg.Scale * 100)) + "%", 1.4f);
        }

        private void ShowHud(string text, float seconds)
        {
            hudHint = text;
            hudHintLeft = seconds;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                BuildMenu();
                menu.Show(Cursor.Position);
            }
            base.OnMouseClick(e);
        }

        // ==================== 托盘与菜单 ====================

        private void InitTray()
        {
            tray = new NotifyIcon();
            try
            {
                using (Bitmap bmp = CharacterArt.RenderTrayIcon(32))
                {
                    IntPtr h = bmp.GetHicon();
                    trayIcon = Icon.FromHandle(h);
                    tray.Icon = trayIcon;
                }
            }
            catch { }
            tray.Text = "大肥鱼桌宠";
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleVisible(); };
            menu = new ContextMenuStrip();
            menu.Opening += delegate { BuildMenu(); };
            tray.ContextMenuStrip = menu;
        }

        private void ToggleVisible()
        {
            if (Visible)
            {
                manualHidden = true;
                Hide();
                tray.Visible = true;
            }
            else
            {
                manualHidden = false;
                Show();
                BringToFrontSafe();
                EnterState(PetState.Surprised, 0.9f, false);
                Say("我回来啦～", 3f);
            }
        }

        private void BringToFrontSafe()
        {
            Native.SetWindowPos(Handle, cfg.TopMost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        private void InitWidgets()
        {
            if (widgets == null)
            {
                widgets = new WidgetsForm(cfg, this);
                widgets.SyncFromConfig();
            }
            if (!cfg.SimpleMode) widgets.Show();
        }

        public void OpenWidgets()
        {
            if (widgets == null) widgets = new WidgetsForm(cfg, this);
            widgets.SyncFromConfig();
            widgets.Show();
            widgets.BringToFront();
        }

        private void BuildMenu()
        {
            menu.Items.Clear();
            SysSnapshot s = Monitor.Latest;

            ToolStripMenuItem head = new ToolStripMenuItem("大肥鱼 · 亲密度 " + ((int)cfg.Intimacy) + "% · 饱食度 " + ((int)cfg.Satiety) + "% · 鱼丸 " + cfg.Coins);
            head.Enabled = false;
            menu.Items.Add(head);

            string status = "CPU " + FmtPct(s.CpuLoad) + "  内存 " + FmtPct(s.MemPercent);
            if (s.GpuUsage >= 0) status += "  GPU " + FmtPct(s.GpuUsage) + " " + FmtTemp(s.GpuTempC);
            if (s.HasBattery) status += "  电量 " + s.BatteryPercent + "%";
            ToolStripMenuItem st = new ToolStripMenuItem(status);
            st.Enabled = false;
            menu.Items.Add(st);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Item("投喂白米饭（铁盆）", delegate { FeedRice(); }));
            menu.Items.Add(Item("喂食（小鱼干）", delegate { Feed(); }));
            menu.Items.Add(Item("扭蛋机 / 背包", delegate { OpenGacha(); }));
            menu.Items.Add(Item("番茄钟", delegate { OpenPomodoro(); }));
            menu.Items.Add(Item("闹钟与提醒", delegate { OpenAlarm(); }));
            menu.Items.Add(Item("今日鱼报", delegate { OpenReport(); }));
            menu.Items.Add(Item("生成表情包", delegate { OpenMeme(); }));
            menu.Items.Add(Item("让它说句话", delegate
            {
                Wake();
                Say(Speech.OnIdleChatter(), 5f);
                EnterState(PetState.Cheer, 1.5f, false);
            }));
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem info = new ToolStripMenuItem("系统信息小窗");
            info.DropDownItems.Add(Check("显示（打开面板）", widgets != null && widgets.Visible, delegate { OpenWidgets(); }));
            info.DropDownItems.Add(new ToolStripSeparator());
            info.DropDownItems.Add(Check("CPU 占用", cfg.ShowCpu, delegate { cfg.ShowCpu = !cfg.ShowCpu; SyncWidgets(); }));
            info.DropDownItems.Add(Check("GPU 占用/温度/风扇", cfg.ShowGpu, delegate { cfg.ShowGpu = !cfg.ShowGpu; SyncWidgets(); }));
            info.DropDownItems.Add(Check("内存", cfg.ShowMem, delegate { cfg.ShowMem = !cfg.ShowMem; SyncWidgets(); }));
            info.DropDownItems.Add(Check("网络", cfg.ShowNet, delegate { cfg.ShowNet = !cfg.ShowNet; SyncWidgets(); }));
            menu.Items.Add(info);

            menu.Items.Add(Check("桌面漫游", cfg.WalkAround, delegate
            {
                cfg.WalkAround = !cfg.WalkAround;
                if (!cfg.WalkAround && state == PetState.Walk) EnterState(PetState.Idle, -1f, false);
                ShowHud(cfg.WalkAround ? "开始漫游" : "原地待着", 1.4f);
            }));
            menu.Items.Add(Check("跟随鼠标", cfg.FollowMouse, delegate
            {
                cfg.FollowMouse = !cfg.FollowMouse;
                ShowHud(cfg.FollowMouse ? "跟随鼠标" : "不跟了", 1.4f);
            }));
            menu.Items.Add(Check("鼠标穿透（点不到我）", cfg.MouseThrough, delegate
            {
                cfg.MouseThrough = !cfg.MouseThrough;
                ApplyMouseThrough();
                ShowHud(cfg.MouseThrough ? "鼠标穿透已开（用托盘图标关掉）" : "鼠标穿透已关", 2.5f);
            }));
            menu.Items.Add(Check("全屏应用时自动隐身", cfg.HideFullscreen, delegate { cfg.HideFullscreen = !cfg.HideFullscreen; }));
            menu.Items.Add(Check("简约模式（无气泡/无面板）", cfg.SimpleMode, delegate
            {
                cfg.SimpleMode = !cfg.SimpleMode;
                if (cfg.SimpleMode && widgets != null) widgets.Hide();
                else OpenWidgets();
            }));

            ToolStripMenuItem size = new ToolStripMenuItem("调整大小");
            float[] presets = new float[] { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f };
            for (int i = 0; i < presets.Length; i++)
            {
                float v = presets[i];
                size.DropDownItems.Add(Check(((int)(v * 100)) + "%", Math.Abs(cfg.Scale - v) < 0.01f, delegate
                {
                    cfg.Scale = v;
                    RefreshAppearance();
                    ShowHud("大小 " + ((int)(v * 100)) + "%", 1.4f);
                }));
            }
            size.DropDownItems.Add(new ToolStripSeparator());
            size.DropDownItems.Add(Item("放大（+10%）", delegate { cfg.Scale = Settings.Clamp(cfg.Scale + 0.1f, 0.35f, 2.5f); RefreshAppearance(); }));
            size.DropDownItems.Add(Item("缩小（-10%）", delegate { cfg.Scale = Settings.Clamp(cfg.Scale - 0.1f, 0.35f, 2.5f); RefreshAppearance(); }));
            size.DropDownItems.Add(new ToolStripSeparator());
            size.DropDownItems.Add(Item("更透明", delegate { cfg.Opacity = Settings.Clamp(cfg.Opacity - 0.1f, 0.3f, 1f); RefreshAppearance(); }));
            size.DropDownItems.Add(Item("更不透明", delegate { cfg.Opacity = Settings.Clamp(cfg.Opacity + 0.1f, 0.3f, 1f); RefreshAppearance(); }));
            menu.Items.Add(size);

            ToolStripMenuItem amb = new ToolStripMenuItem("环境音");
            string[] ambs = Audio.AmbientNames;
            for (int i = 0; i < ambs.Length; i++)
            {
                string nm = ambs[i];
                amb.DropDownItems.Add(Check(Audio.AmbientDisplayName(nm), cfg.Ambient == nm, delegate
                {
                    cfg.Ambient = nm;
                    Audio.PlayAmbient(nm);
                    ShowHud("环境音：" + Audio.AmbientDisplayName(nm), 1.6f);
                }));
            }
            menu.Items.Add(amb);

            ToolStripMenuItem vol = new ToolStripMenuItem("音量");
            int[] vols = new int[] { 0, 30, 50, 70, 90, 100 };
            for (int i = 0; i < vols.Length; i++)
            {
                int v = vols[i];
                vol.DropDownItems.Add(Check(v == 0 ? "静音" : (v + "%"), v == 0 ? cfg.Muted : (!cfg.Muted && cfg.Volume == v), delegate
                {
                    if (v == 0) { cfg.Muted = true; }
                    else { cfg.Muted = false; cfg.Volume = v; }
                    Audio.Muted = cfg.Muted;
                    Audio.Volume = cfg.Volume / 100f;
                    ShowHud("音量 " + (cfg.Muted ? "静音" : v + "%"), 1.4f);
                }));
            }
            menu.Items.Add(vol);

            ToolStripMenuItem dbl = new ToolStripMenuItem("双击行为");
            string[] dblNames = new string[] { "打开主菜单", "喂食小鱼干", "和我说话", "开始专注", "投喂白米饭" };
            for (int i = 0; i < dblNames.Length; i++)
            {
                int v = i;
                dbl.DropDownItems.Add(Check(dblNames[i], cfg.DoubleClickAction == v, delegate { cfg.DoubleClickAction = v; }));
            }
            menu.Items.Add(dbl);

            ToolStripMenuItem look = new ToolStripMenuItem("角色外观");
            look.DropDownItems.Add(Check("自动（有立绘就用立绘）", cfg.CharacterStyle == "auto", delegate
            {
                cfg.CharacterStyle = "auto";
                CharacterArt.ApplyStyleSetting(cfg);
                ShowHud(CharacterArt.UsingSprite ? "使用立绘" : "assets\\sprites 里没有 PNG", 1.8f);
            }));
            look.DropDownItems.Add(Check("只用立绘（assets\\sprites）", cfg.CharacterStyle == "sprite", delegate
            {
                cfg.CharacterStyle = "sprite";
                CharacterArt.ApplyStyleSetting(cfg);
                SpriteArt.Rescan();
                ShowHud(SpriteArt.HasAny ? "使用立绘" : "没找到立绘，放几张 PNG 进 assets\\sprites", 2.5f);
            }));
            look.DropDownItems.Add(new ToolStripSeparator());
            look.DropDownItems.Add(Item("重新扫描立绘", delegate
            {
                SpriteArt.Rescan();
                ShowHud(SpriteArt.HasAny ? "立绘已重新扫描" : "assets\\sprites 里没有 PNG", 2f);
            }));
            look.DropDownItems.Add(Item("打开立绘目录", delegate { OpenSpriteFolder(); }));
            look.DropDownItems.Add(Item("立绘与动画用法", delegate
            {
                MessageBox.Show(
                    "角色完全来自你的立绘图片，程序只负责播放，不手绘。\n\n" +
                    "立绘目录：\n" + SpriteArt.PrimaryDir + "\n\n" +
                    "【静态立绘】按动作命名（可选）：\n" +
                    "idle.png 待机 / walk.png 走动 / happy.png 开心 / surprise.png 惊吓\n" +
                    "sleep.png 睡觉 / sad.png 委屈 / drag.png 被拎起 / focus.png 专注\n" +
                    "relax.png 休息 / eat.png 吃饭 / love.png / dizzy.png / sick.png / cheer.png\n\n" +
                    "【动画帧】推荐：把 tools\\make_anim.py 生成的帧放进 anim\\ 子目录，\n" +
                    "文件名形如 idle_00.png、idle_01.png……，并在 manifest.txt 里记录帧率，\n" +
                    "程序会按帧播放呼吸、摆动、鲸鱼尾摆动等动作。\n\n" +
                    "缺哪个动作就用 idle 顶上。",
                    "立绘与动画", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
            menu.Items.Add(look);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Check("总在最前", cfg.TopMost, delegate
            {
                cfg.TopMost = !cfg.TopMost;
                TopMost = cfg.TopMost;
                BringToFrontSafe();
            }));
            menu.Items.Add(Check("开机自动启动", Settings.IsStartupEnabled(), delegate
            {
                bool now = Settings.IsStartupEnabled();
                Settings.SetStartup(!now);
                ShowHud(!now ? "已加入开机启动" : "已取消开机启动", 1.8f);
            }));
            menu.Items.Add(Item("设置…", delegate { OpenSettings(); }));

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item(SleepMenuItemText(), delegate { if (IsAsleep()) Wake(); else PutToSleep(); }));
            menu.Items.Add(Item("召唤天选姬", delegate { LaunchTxMascot(); }));
            menu.Items.Add(Item("打开奥创中心", delegate { LaunchArmouryCrate(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("关于大肥鱼", delegate { OpenAbout(); }));
            menu.Items.Add(Item("隐藏（托盘双击恢复）", delegate { ToggleVisible(); }));
            menu.Items.Add(Item("退出", delegate { QuitApp(); }));
        }

        private void SyncWidgets()
        {
            if (widgets == null) widgets = new WidgetsForm(cfg, this);
            widgets.SyncFromConfig();
            if (!cfg.SimpleMode) widgets.Show();
        }

        private static string FmtPct(float v)
        {
            if (v < 0) return "--";
            return ((int)Math.Round(v)) + "%";
        }

        private static string FmtTemp(float v)
        {
            if (v < 0) return "--";
            return ((int)Math.Round(v)) + "°C";
        }

        private static ToolStripMenuItem Item(string text, EventHandler h)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Click += h;
            return it;
        }

        private static ToolStripMenuItem Check(string text, bool isChecked, EventHandler h)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            it.Checked = isChecked;
            it.CheckOnClick = false;
            it.Click += h;
            return it;
        }

        // ==================== 外部动作 ====================

        private void HandleDoubleClick()
        {
            switch (cfg.DoubleClickAction)
            {
                case 1: Feed(); break;
                case 2:
                    Wake();
                    Say(Speech.OnIdleChatter(), 5f);
                    EnterState(PetState.Cheer, 1.5f, false);
                    break;
                case 3: OpenPomodoro(); StartPomodoro(); break;
                case 4: FeedRice(); break;
                default:
                    BuildMenu();
                    menu.Show(Cursor.Position);
                    break;
            }
        }

        public void OpenPomodoro()
        {
            if (pomodoro == null || pomodoro.IsDisposed) pomodoro = new PomodoroForm(cfg, this);
            pomodoro.Show();
            pomodoro.BringToFront();
        }

        public void OpenGacha()
        {
            if (gacha == null || gacha.IsDisposed) gacha = new GachaForm(cfg, this);
            gacha.Show();
            gacha.BringToFront();
        }

        public void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed) settingsForm = new SettingsForm(cfg, this);
            settingsForm.Show();
            settingsForm.BringToFront();
        }

        public void OpenAlarm()
        {
            if (alarmForm == null || alarmForm.IsDisposed) alarmForm = new AlarmForm(cfg, this);
            alarmForm.SyncFromConfig();
            alarmForm.Show();
            alarmForm.BringToFront();
        }

        public void OpenReport()
        {
            if (reportForm == null || reportForm.IsDisposed) reportForm = new ReportForm(cfg, this);
            reportForm.RefreshReport();
            reportForm.Show();
            reportForm.BringToFront();
        }

        public void OpenMeme()
        {
            if (memeForm == null || memeForm.IsDisposed) memeForm = new MemeForm(cfg, this);
            memeForm.Show();
            memeForm.BringToFront();
        }

        private void OpenAbout()
        {
            MessageBox.Show(
                "大肥鱼桌宠 v1.0\n\n" +
                "一只 Q 版蓝发鲸鱼娘，抱着铁盆只吃白饭、不干活。\n" +
                "角色与动画全部来自你提供的立绘图片。\n" +
                "功能参照本机「天选姬 4.7.6」：桌面漫游、点击互动、铁盆扒白饭、喂食养成、\n" +
                "扭蛋换装、番茄钟、闹钟、系统信息小窗、环境音、全屏自动隐身、开机自启。\n\n" +
                "右键点我打开菜单；左键拖动把我拎走；滚轮可以调整大小。\n\n" +
                "配置与存档目录：" + Settings.DataDir + "\n" +
                "立绘目录：" + SpriteArt.PrimaryDir,
                "关于大肥鱼", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenSpriteFolder()
        {
            try
            {
                string dir = SpriteArt.PrimaryDir;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
                Say("把透明背景的立绘放进这个目录就能换成你的角色～", 6f);
            }
            catch { }
        }

        private void LaunchTxMascot()
        {
            try
            {
                string p = @"C:\Program Files\ASUS\TX Mascot\TX Mascot.exe";
                if (File.Exists(p))
                {
                    Process.Start(p);
                    Say("把天选姬也叫来一起玩～", 4f);
                }
                else
                {
                    Say("这台机器上没找到天选姬呢", 4f);
                }
            }
            catch
            {
                Say("召唤失败，可能被系统拦住了", 4f);
            }
        }

        private void LaunchArmouryCrate()
        {
            try
            {
                Process.Start(new ProcessStartInfo("armourycrate://") { UseShellExecute = true });
                Say("奥创中心这就来～", 3.5f);
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder") { UseShellExecute = true });
                    Say("没找到奥创中心，帮你打开了应用列表", 4f);
                }
                catch { Say("打不开奥创中心，手动启动一下吧", 4f); }
            }
        }

        public void QuitApp()
        {
            SaveNow();
            cfg.Save();
            try { Audio.Shutdown(); } catch { }
            try
            {
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
            }
            catch { }
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveNow();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }
                if (canvas != null) { canvas.Dispose(); canvas = null; }
                if (bubbleFont != null) { bubbleFont.Dispose(); bubbleFont = null; }
                if (hudFont != null) { hudFont.Dispose(); hudFont = null; }
            }
            base.Dispose(disposing);
        }

        // ==================== IPetHost ====================

        public Settings Cfg { get { return cfg; } }
        public SysSnapshot Snapshot { get { return Monitor.Latest; } }

        public void Say(string text)
        {
            Say(text, cfg.BubbleSeconds);
        }

        public void Say(string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (cfg.SimpleMode || !cfg.SpeechEnabled) return;
            if (text.Length > 90) text = text.Substring(0, 88) + "…";
            bubbleText = text;
            bubbleLeft = seconds;
        }

        public void SayWithMood(string text, PetMood mood)
        {
            Say(text, cfg.BubbleSeconds);
            EnterState(StateFor(mood), 2.2f, false);
        }

        public void ForceMood(PetMood mood, float seconds)
        {
            moodLockValue = mood;
            moodLockLeft = seconds;
        }

        public void Notify(string title, string text)
        {
            try
            {
                if (tray != null && tray.Visible) tray.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
            }
            catch { }
        }

        public void RewardCoins(int n, string reason)
        {
            cfg.Coins += n;
            if (n > 0 && reason != null && reason.Length > 0)
                Say("获得 " + n + " 颗鱼丸（" + reason + "），现在有 " + cfg.Coins + " 颗", 4f);
        }

        public void AddIntimacy(float v)
        {
            float before = cfg.Intimacy;
            cfg.Intimacy = Settings.Clamp(cfg.Intimacy + v, 0f, 100f);
            if ((int)(before / 20f) < (int)(cfg.Intimacy / 20f))
            {
                Say(Speech.OnLevelUp(), 4f);
                PlaySfx("levelup");
                Notify("大肥鱼", "亲密度提升到 " + ((int)cfg.Intimacy) + "% 啦！");
            }
        }

        public void PlaySfx(string name)
        {
            try { Audio.PlaySfx(name); } catch { }
        }

        public void SetAmbient(string name)
        {
            cfg.Ambient = name;
            try { Audio.PlayAmbient(name); } catch { }
        }
    }

    internal enum PetState
    {
        Idle, Walk, Drag, Sink, Eat, EatRice, Sleepy, Sleep, Focus, Relax, Dizzy, Cheer, Surprised
    }
}
