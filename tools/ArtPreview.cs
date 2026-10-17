// ArtPreview.cs —— 大肥鱼桌宠美术自检程序
// 编译：csc /target:exe /out:tools\ArtPreview.exe src\Contracts.cs src\Art.cs tools\ArtPreview.cs
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using FishPet;

internal static class Program
{
    static string s_dir;
    static string s_family;
    static readonly Color Ink = Color.FromArgb(255, 34, 52, 88);
    static readonly Color InkSoft = Color.FromArgb(255, 110, 126, 156);

    static void Main()
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        s_dir = Path.Combine(root, "preview");
        Directory.CreateDirectory(s_dir);
        s_family = PickFamily();

        try
        {
            RenderMoods();
            RenderRice();
            RenderAccessories();
            RenderSizes();
            RenderBubbles();
            RenderTray();
            SelfCheck();
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED: " + ex.ToString());
            Environment.Exit(1);
        }

        Console.WriteLine("OK  preview -> " + s_dir);
    }

    static string PickFamily()
    {
        string[] wanted = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Segoe UI" };
        for (int i = 0; i < wanted.Length; i++)
        {
            try
            {
                using (FontFamily ff = new FontFamily(wanted[i])) { return wanted[i]; }
            }
            catch (Exception) { }
        }
        return FontFamily.GenericSansSerif.Name;
    }

    static Font F(float px, bool bold)
    {
        return new Font(new FontFamily(s_family), px, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }

    static void SetQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    static void Checker(Graphics g, Rectangle r, int cell)
    {
        using (SolidBrush a = new SolidBrush(Color.FromArgb(255, 251, 252, 255)))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 234, 238, 247)))
        {
            g.FillRectangle(a, r);
            int nx = (r.Width + cell - 1) / cell;
            int ny = (r.Height + cell - 1) / cell;
            for (int y = 0; y < ny; y++)
            {
                for (int x = 0; x < nx; x++)
                {
                    if (((x + y) & 1) == 0) continue;
                    int rx = r.X + x * cell;
                    int ry = r.Y + y * cell;
                    Rectangle q = new Rectangle(rx, ry, Math.Min(cell, r.Right - rx), Math.Min(cell, r.Bottom - ry));
                    if (q.Width > 0 && q.Height > 0) g.FillRectangle(b, q);
                }
            }
        }
    }

    static void TextC(Graphics g, string s, float cx, float y, float px, Color col, bool bold)
    {
        TextC(g, s, cx, y, px, col, bold, 400f);
    }

    static void TextC(Graphics g, string s, float cx, float y, float px, Color col, bool bold, float width)
    {
        using (Font f = F(px, bold))
        using (SolidBrush b = new SolidBrush(col))
        using (StringFormat sf = new StringFormat())
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Near;
            g.DrawString(s, f, b, new RectangleF(cx - width / 2f, y, width, px * 2.4f + 6f), sf);
        }
    }

    static void TextL(Graphics g, string s, float x, float y, float px, Color col, bool bold)
    {
        using (Font f = F(px, bold))
        using (SolidBrush b = new SolidBrush(col))
        {
            g.DrawString(s, f, b, x, y);
        }
    }

    static string MoodName(PetMood m)
    {
        switch (m)
        {
            case PetMood.Idle: return "发呆";
            case PetMood.Blink: return "眨眼";
            case PetMood.Happy: return "开心";
            case PetMood.Surprised: return "惊讶";
            case PetMood.Sleepy: return "困倦";
            case PetMood.Sleep: return "睡着";
            case PetMood.Eat: return "吃东西";
            case PetMood.Drag: return "被拎起";
            case PetMood.Walk: return "游动";
            case PetMood.Focus: return "专注";
            case PetMood.Relax: return "休息";
            case PetMood.Sad: return "委屈";
            case PetMood.Love: return "喜欢";
            case PetMood.Dizzy: return "晕眩";
            case PetMood.Sick: return "生病";
            case PetMood.Cheer: return "加油";
            case PetMood.EatRice: return "干饭";
            default: return m.ToString();
        }
    }

    static PetPose PoseFor(PetMood m, float time)
    {
        PetPose p = PetPose.Default;
        p.Mood = m;
        p.Time = time;
        p.Happy = 0.4f;
        p.Rice = 1f;
        switch (m)
        {
            case PetMood.Blink:
                p.Blink = 1f;
                break;
            case PetMood.Happy:
                p.MouthOpen = 0.55f;
                p.Happy = 1f;
                break;
            case PetMood.Surprised:
                p.MouthOpen = 0.7f;
                break;
            case PetMood.Sleep:
                p.Blink = 1f;
                break;
            case PetMood.Eat:
                p.MouthOpen = 0.45f;
                break;
            case PetMood.Drag:
                p.Squash = 1.10f;
                p.MouthOpen = 0.5f;
                break;
            case PetMood.Walk:
                p.Tilt = 0f;
                break;
            case PetMood.Sad:
                p.MouthOpen = 0.25f;
                break;
            case PetMood.Love:
                p.Happy = 1f;
                break;
            case PetMood.Cheer:
                p.MouthOpen = 0.65f;
                p.Happy = 0.9f;
                break;
            case PetMood.EatRice:
                p.Rice = 1f;
                p.Time = 0.125f / 0.30f;
                break;
            default:
                break;
        }
        return p;
    }

    // ---------------------------------------------------------------- moods
    static void RenderMoods()
    {
        PetMood[] moods = new PetMood[]
        {
            PetMood.Idle, PetMood.Blink, PetMood.Happy, PetMood.Surprised, PetMood.Sleepy,
            PetMood.Sleep, PetMood.Eat, PetMood.Drag, PetMood.Walk, PetMood.Focus,
            PetMood.Relax, PetMood.Sad, PetMood.Love, PetMood.Dizzy, PetMood.Sick,
            PetMood.Cheer, PetMood.EatRice
        };

        int cols = 5, rows = 4, cw = 244, ch = 288, gap = 10, pad = 22, top = 70;
        int W = pad * 2 + cols * cw + (cols - 1) * gap;
        int H = top + rows * ch + (rows - 1) * gap + 44;

        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 246, 248, 253));
            TextC(g, "大肥鱼桌宠 · 17 种情绪", W / 2f, 16f, 26f, Ink, true);
            TextC(g, "Art.moods  |  手绘矢量 GDI+  |  每种情绪都应一眼可辨", W / 2f, 48f, 13f, InkSoft, false, W - 40f);

            for (int i = 0; i < moods.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Rectangle cell = new Rectangle(pad + col * (cw + gap), top + row * (ch + gap), cw, ch);
                Checker(g, cell, 20);
                using (Pen bd = new Pen(Color.FromArgb(160, 196, 208, 232), 1f))
                    g.DrawRectangle(bd, cell);

                RectangleF box = new RectangleF(cell.X + 6f, cell.Y + 6f, cell.Width - 12f, cell.Height - 50f);
                PetPose pose = PoseFor(moods[i], 1.15f + i * 0.37f);
                Art.DrawGroundShadow(g, box, 0.78f);
                Art.DrawFish(g, box, pose);

                TextC(g, MoodName(moods[i]), cell.X + cw / 2f, cell.Bottom - 42f, 17f, Ink, true);
                TextC(g, moods[i].ToString() + "  #" + (int)moods[i], cell.X + cw / 2f, cell.Bottom - 21f, 11f, InkSoft, false);
            }

            TextC(g, "idle 呼吸晃脚 / blink 闭眼 / happy 弯月眼 / surprised 蹦起 / sleepy 半垂眼 / sleep 鼻涕泡 / "
                + "eat 咀嚼碎屑 / drag 乱蹬 / walk 摆动", W / 2f, H - 36f, 11f, InkSoft, false, W - 40f);

            bmp.Save(Path.Combine(s_dir, "moods.png"), ImageFormat.Png);
        }
    }

    // ---------------------------------------------------------------- rice
    static void RenderRice()
    {
        float[] rices = new float[] { 1.00f, 0.60f, 0.25f, 0.05f, 0.00f };
        float[] samples = new float[] { 0.180f, 0.375f, 0.650f, 0.875f };
        string[] phases = new string[] { "① 咀嚼", "② 扒拉", "③ 端盆倒饭", "④ 放下满足" };

        int cw = 240, ch = 272, gap = 10, pad = 24, top = 104, left = 128;
        int W = left + pad + 4 * cw + 3 * gap;
        int H = top + 5 * ch + 4 * gap + 50;

        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 246, 248, 253));
            TextC(g, "大肥鱼 · 招牌动作「干饭」 5 档饭量（含空盆）× 4 个相位", W / 2f, 16f, 24f, Ink, true, W - 40f);
            TextC(g, "p = (Time × 0.30) mod 1   |   1.00 满盆小山 / 0.60 齐盆口 / 0.25 露出盆壁 / 0.05 盆底薄层 / 0.00 空盆",
                  W / 2f, 54f, 12f, InkSoft, false, W - 40f);

            for (int c = 0; c < phases.Length; c++)
            {
                TextC(g, phases[c], left + c * (cw + gap) + cw / 2f, 80f, 15f, Ink, true);
            }

            for (int r = 0; r < rices.Length; r++)
            {
                float rice = rices[r];
                TextL(g, "米量 " + rice.ToString("0.00"), pad, top + r * (ch + gap) + ch / 2f - 22f, 17f, Ink, true);
                if (rice >= 0.99f) TextL(g, "满盆小山", pad + 24f, top + r * (ch + gap) + ch / 2f + 2f, 11f, InkSoft, false);
                else if (rice >= 0.60f) TextL(g, "齐盆口", pad + 38f, top + r * (ch + gap) + ch / 2f + 2f, 11f, InkSoft, false);
                else if (rice >= 0.25f) TextL(g, "半盆", pad + 46f, top + r * (ch + gap) + ch / 2f + 2f, 11f, InkSoft, false);
                else if (rice > 0.01f) TextL(g, "见底", pad + 46f, top + r * (ch + gap) + ch / 2f + 2f, 11f, InkSoft, false);
                else TextL(g, "空盆", pad + 46f, top + r * (ch + gap) + ch / 2f + 2f, 11f, InkSoft, false);

                for (int c = 0; c < samples.Length; c++)
                {
                    Rectangle cell = new Rectangle(left + c * (cw + gap), top + r * (ch + gap), cw, ch);
                    Checker(g, cell, 20);
                    using (Pen bd = new Pen(Color.FromArgb(150, 196, 208, 232), 1f))
                        g.DrawRectangle(bd, cell);

                    PetPose p = PetPose.Default;
                    p.Mood = PetMood.EatRice;
                    p.Rice = rice;
                    p.Time = samples[c] / 0.30f;
                    p.Happy = 0.5f;

                    RectangleF box = new RectangleF(cell.X + 8f, cell.Y + 6f, cell.Width - 16f, cell.Height - 12f);
                    Art.DrawGroundShadow(g, box, 0.75f);
                    Art.DrawFish(g, box, p);
                }
            }

            TextC(g, "四个相位的区别：① 嘴快速开合、腮帮鼓 ② 勺子快速送嘴 ③ 盆移到嘴前、头后仰、米粒成串 ④ 眯眼满足、嘴角粘米",
                  W / 2f, H - 36f, 12f, InkSoft, false, W - 40f);

            bmp.Save(Path.Combine(s_dir, "rice.png"), ImageFormat.Png);
        }
    }

    // ---------------------------------------------------------------- accessories
    static void RenderAccessories()
    {
        int cols = 4, rows = 4, cw = 236, ch = 262, gap = 10, pad = 22, top = 68;
        int W = pad * 2 + cols * cw + (cols - 1) * gap;
        int H = top + rows * ch + (rows - 1) * gap + 40;

        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 246, 248, 253));
            TextC(g, "大肥鱼 · 13 种装扮", W / 2f, 16f, 26f, Ink, true);
            TextC(g, "索引与 Accessory 枚举对齐；[0] = 无", W / 2f, 48f, 12f, InkSoft, false, W - 40f);

            string[] names = Art.AccessoryNames;
            for (int i = 0; i < names.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Rectangle cell = new Rectangle(pad + col * (cw + gap), top + row * (ch + gap), cw, ch);
                Checker(g, cell, 20);
                using (Pen bd = new Pen(Color.FromArgb(150, 196, 208, 232), 1f))
                    g.DrawRectangle(bd, cell);

                PetPose p = PetPose.Default;
                p.Mood = PetMood.Idle;
                p.Accessory = (Accessory)i;
                p.Time = 1.0f + i * 0.31f;
                p.Happy = 0.7f;
                p.Rice = 1f;

                RectangleF box = new RectangleF(cell.X + 6f, cell.Y + 6f, cell.Width - 12f, cell.Height - 46f);
                Art.DrawGroundShadow(g, box, 0.78f);
                Art.DrawFish(g, box, p);

                TextC(g, names[i], cell.X + cw / 2f, cell.Bottom - 38f, 16f, Ink, true);
                TextC(g, ((Accessory)i).ToString() + (Art.IsHeadSlot((Accessory)i) ? " · 头部件" : " · 身体件"),
                      cell.X + cw / 2f, cell.Bottom - 19f, 10.5f, InkSoft, false);
            }

            bmp.Save(Path.Combine(s_dir, "accessories.png"), ImageFormat.Png);
        }
    }

    // ---------------------------------------------------------------- sizes
    static void RenderSizes()
    {
        int[] sizes = new int[] { 96, 160, 260, 420, 600 };
        int gap = 26, pad = 24, top = 70, bottom = 60;
        int W = pad * 2;
        for (int i = 0; i < sizes.Length; i++) W += sizes[i] + (i < sizes.Length - 1 ? gap : 0);
        int H = top + 600 + bottom;

        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 246, 248, 253));
            TextC(g, "大肥鱼 · 尺寸适配 96 / 160 / 260 / 420 / 600 px", W / 2f, 16f, 24f, Ink, true, W - 40f);
            TextC(g, "同一份矢量代码，脚底始终踩在 box.Bottom，头顶 v = -1.10", W / 2f, 46f, 12f, InkSoft, false, W - 40f);

            float x = pad;
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                Rectangle cell = new Rectangle((int)x, top, sz, sz);
                Checker(g, cell, 20);
                using (Pen bd = new Pen(Color.FromArgb(150, 196, 208, 232), 1f))
                    g.DrawRectangle(bd, cell);

                PetPose p = PetPose.Default;
                p.Mood = (i % 2 == 0) ? PetMood.Idle : PetMood.Happy;
                p.Happy = 0.7f;
                p.Time = 0.85f;
                p.Rice = 1f;

                RectangleF box = new RectangleF(cell.X + 1f, cell.Y + 1f, cell.Width - 2f, cell.Height - 2f);
                Art.DrawGroundShadow(g, box, 0.8f);
                Art.DrawFish(g, box, p);

                TextC(g, sz + " px", cell.X + sz / 2f, cell.Bottom + 12f, 14f, Ink, true);
                if (sz <= 160) TextC(g, "小尺寸描边加粗", cell.X + sz / 2f, cell.Bottom + 32f, 10f, InkSoft, false);

                x += sz + gap;
            }

            bmp.Save(Path.Combine(s_dir, "sizes.png"), ImageFormat.Png);
        }
    }

    // ---------------------------------------------------------------- bubbles
    static void RenderBubbles()
    {
        string[] texts = new string[]
        {
            "嗯。",
            "今天也要加油摸鱼哦～",
            "主人，已经连续专注 25 分钟啦，起来喝口水、看看远处吧！",
            "I'm a big fat blue fish! 我会一直陪着你的。",
            "呜……CPU 好烫好烫，大肥鱼快要化成鱼汤了，让我喘口气好不好嘛……"
        };
        int[] maxW = new int[] { 150, 220, 300, 280, 300 };
        float[] fontPx = new float[] { 18f, 17f, 16f, 16f, 22f };

        int W = 980, H = 940;
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 247, 249, 253));
            TextC(g, "气泡自适应 · DrawBubble 永不越界", W / 2f, 16f, 24f, Ink, true, W - 60f);
            TextC(g, "红色虚线 = 调用方传入的 rect；绿色点 = 期望的 tail 方向（尖端会被钳制在 rect 内）",
                  W / 2f, 46f, 12f, InkSoft, false, W - 60f);

            float y = 84f;
            for (int i = 0; i < texts.Length; i++)
            {
                using (Font font = new Font(new FontFamily(s_family), fontPx[i], FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    SizeF sz = Art.MeasureBubble(g, texts[i], font, maxW[i]);
                    float x = 40f + i * 24f;
                    if (x + sz.Width > W - 30f) x = W - 30f - sz.Width;
                    float rectH = sz.Height + 16f;
                    RectangleF rect = new RectangleF(x, y, sz.Width, rectH);
                    PointF tail = new PointF(x + sz.Width * 0.42f, rect.Bottom + 26f);

                    TextL(g, "MeasureBubble = " + sz.Width.ToString("0") + " × " + sz.Height.ToString("0")
                        + "   maxTextWidth = " + maxW[i] + "   font = " + fontPx[i].ToString("0") + "px",
                        x, y - 20f, 11f, InkSoft, false);

                    using (Pen red = new Pen(Color.FromArgb(225, 232, 72, 72), 1.4f))
                    {
                        red.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(red, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                    Art.DrawBubble(g, texts[i], font, rect, tail, false);

                    using (SolidBrush dot = new SolidBrush(Color.FromArgb(220, 40, 180, 120)))
                        g.FillEllipse(dot, tail.X - 4f, tail.Y - 4f, 8f, 8f);
                    using (Pen ln = new Pen(Color.FromArgb(120, 40, 180, 120), 1f))
                    {
                        ln.DashStyle = DashStyle.Dot;
                        g.DrawLine(ln, rect.Left + rect.Width / 2f, rect.Bottom, tail.X, tail.Y);
                    }

                    y += Math.Max(rectH + 60f, 150f);
                }
            }

            TextC(g, "策略：先按 rect 内缩描边量 → 扣掉左右 padding 再排版 → 最多 6 级缩字（下限 7px）→ 最后 SetClip(inner) 兜底",
                  W / 2f, H - 34f, 11f, InkSoft, false, W - 40f);

            bmp.Save(Path.Combine(s_dir, "bubble.png"), ImageFormat.Png);
        }
    }

    // ---------------------------------------------------------------- tray
    static void RenderTray()
    {
        int W = 620, H = 380;
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            SetQuality(g);
            g.Clear(Color.FromArgb(255, 247, 249, 253));
            TextC(g, "托盘图标 · 头部特写（16 / 32 px）", W / 2f, 16f, 23f, Ink, true);
            TextC(g, "8 倍超采样 + HighQualityBicubic 缩小；左侧为实际尺寸，右侧为放大展示",
                  W / 2f, 46f, 12f, InkSoft, false, W - 40f);

            using (Bitmap i16 = Art.RenderTrayIcon(16))
            using (Bitmap i32 = Art.RenderTrayIcon(32))
            {
                Rectangle cell1 = new Rectangle(30, 80, 170, 170);
                Checker(g, cell1, 17);
                g.DrawImage(i16, new Rectangle(58, 140, 60, 60));
                TextC(g, "16px 实际（放大 3.75×）", cell1.X + 85, cell1.Bottom + 8, 11f, InkSoft, false);

                Rectangle cell2 = new Rectangle(30, 260, 170, 100);
                Checker(g, cell2, 17);
                g.DrawImage(i16, new Rectangle(57, 270, 16, 16));
                g.DrawImage(i32, new Rectangle(97, 262, 32, 32));
                TextC(g, "实际 16 / 32", 115, 302, 11f, InkSoft, false);

                Rectangle cell3 = new Rectangle(230, 76, 180, 180);
                Checker(g, cell3, 20);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(i16, new Rectangle(245, 90, 160, 160));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                TextC(g, "16px ×10 像素展示", cell3.X + 90, cell3.Bottom + 8, 12f, Ink, true);

                Rectangle cell4 = new Rectangle(420, 76, 180, 180);
                Checker(g, cell4, 20);
                g.DrawImage(i32, new Rectangle(430, 84, 160, 160));
                TextC(g, "32px 放大（bicubic）", cell4.X + 90, cell4.Bottom + 8, 12f, Ink, true);

                TextC(g, "目标：16px 也能认出「蓝发 + 白荷叶边 + 两只大眼 + 腮红」", W / 2f, H - 32f, 11f, InkSoft, false, W - 40f);
            }

            bmp.Save(Path.Combine(s_dir, "tray.png"), ImageFormat.Png);
        }
    }

    static void SelfCheck()
    {
        Console.WriteLine("AccessoryNames.Length = " + Art.AccessoryNames.Length + "  [0] = " + Art.AccessoryNames[0]);
        Console.WriteLine("IsHeadSlot(StrawHat) = " + Art.IsHeadSlot(Accessory.StrawHat)
            + "  IsHeadSlot(BowTie) = " + Art.IsHeadSlot(Accessory.BowTie));

        PetPose p = PetPose.Default;
        p.Mood = PetMood.EatRice;
        p.Rice = 0.6f;
        p.Time = 1.25f;
        using (Bitmap b = Art.RenderPose(p, 96, 120))
            Console.WriteLine("RenderPose(96x120) = " + b.Width + "x" + b.Height);
        using (Bitmap t = Art.RenderTrayIcon(16))
            Console.WriteLine("RenderTrayIcon(16) = " + t.Width + "x" + t.Height);

        using (Bitmap canvas = new Bitmap(200, 200, PixelFormat.Format32bppPArgb))
        using (Graphics g = Graphics.FromImage(canvas))
        using (Font f = new Font(new FontFamily(s_family), 15f, FontStyle.Regular, GraphicsUnit.Pixel))
        {
            SizeF sz = Art.MeasureBubble(g, "测试一下气泡尺寸", f, 120);
            Console.WriteLine("MeasureBubble = " + sz.Width.ToString("0.0") + " x " + sz.Height.ToString("0.0"));
            // 极端小 rect：不抛异常、不越界（内部有 SetClip 兜底）
            Art.DrawBubble(g, "很长很长很长很长的一句话，用来测试极小 rect 的兜底逻辑", f,
                new RectangleF(10, 10, 60, 34), new PointF(40, 80), false);
            Art.DrawBubble(g, "窄", f, new RectangleF(120, 10, 22, 20), new PointF(130, 60), true);
            Art.DrawBubble(g, null, f, new RectangleF(10, 100, 140, 50), new PointF(80, 170), false);
            Console.WriteLine("DrawBubble stress: OK");
        }
    }
}
