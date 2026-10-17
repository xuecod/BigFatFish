// Art.cs —— 大肥鱼桌宠：全部手绘矢量美术（纯 GDI+，无任何外部图片）
// 目标框架 .NET Framework 4.x / C# 5
//
// 坐标系约定（单位空间，随 box 等比缩放）：
//   u 向右为正，v 向下为正；角色脚底 v = +1.00，头顶 v = -1.10。
//   DrawFish 内部把 Graphics 变换到该单位空间，所有几何量都以单位书写，
//   描边宽度也按单位给定（内部再换算成像素、并做小尺寸加粗）。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace FishPet
{
    public static class Art
    {
        // ---------- 全局比例 ----------
        public const float UnitHeight = 2.10f;   // v: -1.10 .. +1.00
        public const float TopUnit = -1.10f;
        public const float FootUnit = 1.00f;
        const float WidthUnits = 1.86f;          // 需要的横向空间（含鲸尾/头发）

        // ---------- 皮肤 / 头发 ----------
        static readonly Color Skin = Color.FromArgb(255, 255, 233, 222);
        static readonly Color SkinLight = Color.FromArgb(255, 255, 245, 238);
        static readonly Color SkinShade = Color.FromArgb(255, 247, 208, 196);
        static readonly Color HairDeep = Color.FromArgb(255, 34, 50, 110);
        static readonly Color HairMid = Color.FromArgb(255, 56, 96, 186);
        static readonly Color HairLight = Color.FromArgb(255, 96, 148, 226);
        static readonly Color HairShine = Color.FromArgb(255, 142, 188, 248);

        // ---------- 服装 / 道具 ----------
        static readonly Color DressNavy = Color.FromArgb(255, 33, 47, 92);
        static readonly Color DressNavyLight = Color.FromArgb(255, 47, 67, 126);
        static readonly Color ApronWhite = Color.FromArgb(255, 250, 252, 255);
        static readonly Color ApronShade = Color.FromArgb(255, 226, 234, 248);
        static readonly Color LaceWhite = Color.FromArgb(255, 253, 254, 255);
        static readonly Color LaceShade = Color.FromArgb(255, 210, 221, 240);
        static readonly Color Gold = Color.FromArgb(255, 238, 199, 126);
        static readonly Color RiceWhite = Color.FromArgb(255, 255, 252, 243);
        static readonly Color RiceShade = Color.FromArgb(255, 230, 224, 209);
        static readonly Color RiceEdge = Color.FromArgb(255, 190, 184, 168);
        static readonly Color BowlLight = Color.FromArgb(255, 247, 249, 253);
        static readonly Color BowlMid = Color.FromArgb(255, 214, 221, 233);
        static readonly Color BowlDark = Color.FromArgb(255, 164, 175, 194);
        static readonly Color BowlBlue = Color.FromArgb(255, 40, 68, 124);
        static readonly Color BowlBlueDeep = Color.FromArgb(255, 24, 42, 80);
        static readonly Color BowlCavity = Color.FromArgb(255, 63, 67, 82);
        static readonly Color MouthDark = Color.FromArgb(255, 128, 54, 68);
        static readonly Color Tongue = Color.FromArgb(255, 234, 138, 152);
        static readonly Color TearBlue = Color.FromArgb(230, 128, 196, 250);
        static readonly Color SteamWhite = Color.FromArgb(120, 255, 255, 255);

        // ---------- 装扮 ----------
        static readonly string[] s_accessoryNames = new string[]
        {
            "无", "草帽", "墨镜", "蝴蝶结", "围巾", "王冠", "圣诞帽",
            "潜水镜", "竹蜻蜓", "耳机", "小黄鸭", "头花", "巫师帽"
        };

        public static string[] AccessoryNames
        {
            get { return s_accessoryNames; }
        }

        public static bool IsHeadSlot(Accessory a)
        {
            switch (a)
            {
                case Accessory.StrawHat:
                case Accessory.Sunglasses:
                case Accessory.Crown:
                case Accessory.SantaHat:
                case Accessory.DivingMask:
                case Accessory.BambooCopter:
                case Accessory.Headphones:
                case Accessory.RubberDuck:
                case Accessory.Flower:
                case Accessory.WizardHat:
                    return true;
                default:
                    return false;
            }
        }

        // =====================================================================
        // 公开绘制入口
        // =====================================================================
        public static void DrawFish(Graphics g, RectangleF box, PetPose pose)
        {
            if (g == null || box.Width < 6f || box.Height < 6f) return;

            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                float s = Math.Min(box.Height / UnitHeight, box.Width / WidthUnits);
                if (s < 0.5f) return;

                Cfg c = MakeCfg(pose);
                float cx = box.Left + box.Width * 0.5f;
                float fy = box.Bottom;
                float squash = Clamp(pose.Squash, 0.55f, 1.65f);

                g.TranslateTransform(cx, fy + pose.Bob + c.Jump * s);
                g.RotateTransform(pose.Tilt + c.Tilt);
                g.ScaleTransform(1f, squash);
                if (pose.FacingLeft) g.ScaleTransform(-1f, 1f);
                g.ScaleTransform(s, s);
                // 注意：单位空间的 y 统一用 P()/E() 里的 v-1，此处不再额外平移

                Sk k = new Sk(g, s);
                k.Flipped = pose.FacingLeft;
                k.Boost = Clamp(1.48f - s * 0.00175f, 0.95f, 1.55f);

                // 背后 → 前
                DrawTail(k, pose, c);
                DrawBackHair(k, pose, c);
                DrawLegs(k, pose, c);
                DrawLace(k, c);                 // 裙摆下的白色荷叶边（先画，裙身压住上缘）
                DrawBody(k, pose, c);
                DrawApron(k, pose, c);
                if (c.Book) DrawBook(k, pose, c);
                if (c.IsRice) DrawRiceArms(k, pose, c);
                if (!c.IsRice) DrawArms(k, pose, c);
                DrawNeckwear(k, pose, c);

                DrawHead(k, pose, c);

                if (c.IsRice) DrawRiceBowl(k, pose, c);
                DrawOuterFx(k, pose, c);
            }
            finally
            {
                g.Restore(state);
            }
        }

        public static void DrawGroundShadow(Graphics g, RectangleF box, float closeness)
        {
            if (g == null || box.Width < 4f || box.Height < 4f) return;

            float c = Clamp01(closeness);
            GraphicsState st = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                float s = Math.Min(box.Height / UnitHeight, box.Width / WidthUnits);
                if (s < 0.5f) return;

                float cx = box.Left + box.Width * 0.5f;
                float cy = box.Bottom - s * 0.012f;
                float rx = s * (0.32f + 0.24f * c);
                float ry = s * (0.070f + 0.055f * c);
                RectangleF r = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(r);
                    using (PathGradientBrush b = new PathGradientBrush(p))
                    {
                        int a = (int)(42f + 92f * c);
                        b.CenterColor = Color.FromArgb(a, 26, 46, 88);
                        b.SurroundColors = new Color[] { Color.FromArgb(0, 26, 46, 88) };
                        b.CenterPoint = new PointF(cx, cy - ry * 0.1f);
                        g.FillPath(b, p);
                    }
                }
            }
            finally
            {
                g.Restore(st);
            }
        }

        // =====================================================================
        // 气泡
        // =====================================================================
        static string s_bubbleFamily;

        static string BubbleFamily()
        {
            if (s_bubbleFamily == null)
            {
                string[] wanted = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" };
                for (int i = 0; i < wanted.Length; i++)
                {
                    try
                    {
                        using (FontFamily ff = new FontFamily(wanted[i]))
                        {
                            s_bubbleFamily = wanted[i];
                            break;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                if (s_bubbleFamily == null) s_bubbleFamily = FontFamily.GenericSansSerif.Name;
            }
            return s_bubbleFamily;
        }

        static Font MakeFont(float px, FontStyle style)
        {
            if (px < 7f) px = 7f;
            if (px > 200f) px = 200f;
            Font f = null;
            try
            {
                using (FontFamily fam = new FontFamily(BubbleFamily()))
                {
                    f = new Font(fam, px, style, GraphicsUnit.Pixel);
                }
            }
            catch (Exception)
            {
                f = new Font(FontFamily.GenericSansSerif, px, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            if (f == null) f = new Font(FontFamily.GenericSansSerif, px, FontStyle.Regular, GraphicsUnit.Pixel);
            return f;
        }

        static float BasePixelSize(Graphics g, Font font)
        {
            float px = 14f;
            if (font != null)
            {
                switch (font.Unit)
                {
                    case GraphicsUnit.Point: px = font.Size * g.DpiY / 72f; break;
                    case GraphicsUnit.Inch: px = font.Size * g.DpiY; break;
                    case GraphicsUnit.Millimeter: px = font.Size * g.DpiY / 25.4f; break;
                    case GraphicsUnit.Document: px = font.Size * g.DpiY / 300f; break;
                    default: px = font.Size; break;
                }
            }
            return Clamp(px, 7f, 160f);
        }

        static float BubblePadX(float basePx) { return Math.Max(8f, basePx * 0.62f); }
        static float BubblePadY(float basePx) { return Math.Max(6f, basePx * 0.48f); }

        static StringFormat MeasureFormat()
        {
            StringFormat sf = new StringFormat(StringFormat.GenericTypographic);
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            sf.Trimming = StringTrimming.None;
            return sf;
        }

        static List<string> WrapText(Graphics g, string text, Font font, float maxW)
        {
            List<string> lines = new List<string>();
            if (text == null) text = string.Empty;
            if (maxW < 4f) maxW = 4f;
            using (StringFormat sf = MeasureFormat())
            {
                string norm = text.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] paras = norm.Split('\n');
                for (int pi = 0; pi < paras.Length; pi++)
                {
                    string para = paras[pi];
                    if (para.Length == 0) { lines.Add(string.Empty); continue; }
                    System.Text.StringBuilder cur = new System.Text.StringBuilder();
                    for (int i = 0; i < para.Length; i++)
                    {
                        char ch = para[i];
                        cur.Append(ch);
                        float w = g.MeasureString(cur.ToString(), font, 100000, sf).Width;
                        if (w > maxW && cur.Length > 1)
                        {
                            string line = cur.ToString(0, cur.Length - 1);
                            int sp = line.LastIndexOf(' ');
                            if (sp > 0 && sp < line.Length - 1 && IsLatinWordChar(ch))
                            {
                                lines.Add(line.Substring(0, sp));
                                cur.Length = 0;
                                cur.Append(line.Substring(sp + 1));
                                cur.Append(ch);
                            }
                            else
                            {
                                lines.Add(line);
                                cur.Length = 0;
                                cur.Append(ch);
                            }
                        }
                    }
                    lines.Add(cur.ToString());
                }
            }
            return lines;
        }

        static bool IsLatinWordChar(char ch)
        {
            return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '\'';
        }

        static float MeasureMaxLine(Graphics g, List<string> lines, Font font)
        {
            float w = 0f;
            using (StringFormat sf = MeasureFormat())
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    float lw = g.MeasureString(lines[i], font, 100000, sf).Width;
                    if (lw > w) w = lw;
                }
            }
            return w;
        }

        public static SizeF MeasureBubble(Graphics g, string text, Font font, int maxTextWidth)
        {
            if (g == null) return new SizeF(60f, 40f);
            float basePx = BasePixelSize(g, font);
            float padX = BubblePadX(basePx);
            float padY = BubblePadY(basePx);
            float maxW = Math.Max(24f, (float)maxTextWidth);
            float textW;
            int lineCount;
            float lineH;

            GraphicsState st = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font f = MakeFont(basePx, font != null ? font.Style : FontStyle.Regular))
                {
                    List<string> lines = WrapText(g, text, f, maxW);
                    textW = Math.Min(maxW, MeasureMaxLine(g, lines, f));
                    lineCount = Math.Max(1, lines.Count);
                    lineH = f.GetHeight(g) * 1.20f;
                }
            }
            finally
            {
                g.Restore(st);
            }

            float w = Math.Max(textW + padX * 2f, 44f);
            float h = Math.Max(lineCount * lineH + padY * 2f, 30f);
            return new SizeF((float)Math.Ceiling(w), (float)Math.Ceiling(h));
        }

        public static void DrawBubble(Graphics g, string text, Font font, RectangleF rect, PointF tail, bool facingLeft)
        {
            if (g == null) return;
            if (rect.Width < 0f) rect = new RectangleF(rect.Right, rect.Top, -rect.Width, rect.Height);
            if (rect.Height < 0f) rect = new RectangleF(rect.Left, rect.Bottom, rect.Width, -rect.Height);
            if (rect.Width < 18f || rect.Height < 18f) return;

            GraphicsState st = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                float basePx = BasePixelSize(g, font);
                float padX = BubblePadX(basePx);
                float padY = BubblePadY(basePx);

                // 尾巴高度：按约定 = min(rect.Height * 11%, 15px)，且不小于 4px
                float tailH = Math.Min(rect.Height * 0.11f, 15f);
                if (tailH < 4f) tailH = Math.Min(4f, rect.Height * 0.2f);
                float stroke = Clamp(rect.Height * 0.014f, 1.2f, 2.6f);
                float inset = stroke * 0.5f + 0.6f;

                // 外框整体向内缩，保证描边也在 rect 内
                RectangleF outer = new RectangleF(rect.X + inset, rect.Y + inset,
                                                  rect.Width - inset * 2f, rect.Height - inset * 2f);
                if (outer.Width < 10f) return;
                float bodyH = outer.Height - tailH;
                if (bodyH < 12f) return;
                RectangleF body = new RectangleF(outer.X, outer.Y, outer.Width, bodyH);

                float radius = Math.Min(Math.Min(body.Width, body.Height) * 0.30f, 18f);
                if (radius < 2f) radius = 2f;

                float halfBase = Math.Min(body.Width * 0.09f, tailH * 1.05f);
                if (halfBase < 3f) halfBase = Math.Min(3f, body.Width * 0.2f);
                float minX = body.Left + radius + halfBase + 1f;
                float maxX = body.Right - radius - halfBase - 1f;
                if (maxX < minX) { float mid = (body.Left + body.Right) * 0.5f; minX = mid - 1f; maxX = mid + 1f; }
                float baseCx = Clamp(tail.X + (facingLeft ? -halfBase * 0.22f : halfBase * 0.22f), minX, maxX);
                float tipX = Clamp(tail.X, body.Left + 2.5f, body.Right - 2.5f);
                float tipY = outer.Bottom - stroke * 0.5f;

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddArc(new RectangleF(body.Left, body.Top, radius * 2f, radius * 2f), 180f, 90f);
                    p.AddArc(new RectangleF(body.Right - radius * 2f, body.Top, radius * 2f, radius * 2f), 270f, 90f);
                    p.AddArc(new RectangleF(body.Right - radius * 2f, body.Bottom - radius * 2f, radius * 2f, radius * 2f), 0f, 90f);
                    p.AddLine(new PointF(body.Right, body.Bottom), new PointF(baseCx + halfBase, body.Bottom));
                    p.AddLine(new PointF(baseCx + halfBase, body.Bottom), new PointF(tipX, tipY));
                    p.AddLine(new PointF(tipX, tipY), new PointF(baseCx - halfBase, body.Bottom));
                    p.AddLine(new PointF(baseCx - halfBase, body.Bottom), new PointF(body.Left, body.Bottom));
                    p.AddArc(new RectangleF(body.Left, body.Bottom - radius * 2f, radius * 2f, radius * 2f), 90f, 90f);
                    p.CloseFigure();

                    RectangleF gb = new RectangleF(body.Left, body.Top - 1f, body.Width, body.Height + 2f);
                    using (LinearGradientBrush br = new LinearGradientBrush(gb,
                        Color.FromArgb(250, 255, 255, 255), Color.FromArgb(246, 234, 244, 255), 90f))
                    {
                        g.FillPath(br, p);
                    }
                    using (Pen pen = new Pen(Color.FromArgb(210, 120, 156, 226), stroke))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, p);
                    }
                }

                // 文字区域（严格内缩，最后再 SetClip 一次，双保险）
                RectangleF inner = new RectangleF(body.X + padX, body.Y + padY,
                                                  body.Width - padX * 2f, body.Height - padY * 2f);
                if (inner.Width < 8f || inner.Height < 8f) return;

                float chosenPx = 7f;
                List<string> chosenLines = null;
                float chosenLineH = 0f;
                float baseUse = basePx;
                float[] factors = new float[] { 1f, 0.92f, 0.845f, 0.775f, 0.71f, 0.65f };
                for (int i = 0; i < factors.Length; i++)
                {
                    float px = Math.Max(7f, baseUse * factors[i]);
                    if (px < 7f) px = 7f;
                    Font f = MakeFont(px, font != null ? font.Style : FontStyle.Regular);
                    try
                    {
                        List<string> lines = WrapText(g, text, f, inner.Width);
                        float lh = f.GetHeight(g) * 1.18f;
                        float needH = lines.Count * lh;
                        float needW = MeasureMaxLine(g, lines, f);
                        bool fits = needH <= inner.Height + 0.5f && needW <= inner.Width + 0.5f;
                        if (fits || i == factors.Length - 1)
                        {
                            chosenPx = px;
                            chosenLines = lines;
                            chosenLineH = lh;
                            break;
                        }
                    }
                    finally
                    {
                        f.Dispose();
                    }
                }
                if (chosenLines == null) return;

                using (Font f = MakeFont(chosenPx, font != null ? font.Style : FontStyle.Regular))
                using (SolidBrush tb = new SolidBrush(Palette.BubbleText))
                using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags |= StringFormatFlags.NoWrap;
                    float blockH = chosenLines.Count * chosenLineH;
                    float y = inner.Top + Math.Max(0f, (inner.Height - blockH) * 0.42f);
                    GraphicsState ts = g.Save();
                    try
                    {
                        g.SetClip(inner);
                        for (int i = 0; i < chosenLines.Count; i++)
                        {
                            if (chosenLines[i].Length == 0) { y += chosenLineH; continue; }
                            g.DrawString(chosenLines[i], f, tb,
                                new RectangleF(inner.X, y, inner.Width, chosenLineH), sf);
                            y += chosenLineH;
                            if (y > inner.Bottom + 1f) break;
                        }
                    }
                    finally
                    {
                        g.Restore(ts);
                    }
                }
            }
            finally
            {
                g.Restore(st);
            }
        }

        // =====================================================================
        // 离屏渲染
        // =====================================================================
        public static Bitmap RenderPose(PetPose pose, int width, int height)
        {
            if (width < 4) width = 4;
            if (height < 4) height = 4;
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                float pad = Math.Max(1f, Math.Min(width, height) * 0.012f);
                RectangleF box = new RectangleF(pad, pad, width - pad * 2f, height - pad * 2f);
                DrawFish(g, box, pose);
            }
            return bmp;
        }

        public static Bitmap RenderTrayIcon(int size)
        {
            if (size < 8) size = 8;
            int big = Math.Max(96, size * 8);
            float cropUnits = 1.56f;
            float unit = big / cropUnits;
            int cw = (int)Math.Ceiling(2.10f * unit);
            int ch = (int)Math.Ceiling(2.16f * unit);

            Bitmap canvas = new Bitmap(cw, ch, PixelFormat.Format32bppPArgb);
            Rectangle crop;
            try
            {
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    PetPose p = PetPose.Default;
                    p.Mood = PetMood.Idle;
                    p.Time = 0.55f;
                    p.Happy = 0.9f;
                    p.MouthOpen = 0.22f;
                    p.Blink = 0f;

                    float boxW = 2.10f * unit;
                    RectangleF box = new RectangleF((cw - boxW) * 0.5f, 0.06f * unit, boxW, 2.10f * unit);
                    DrawFish(g, box, p);
                }

                int cropW = Math.Min(cw, (int)Math.Floor(cropUnits * unit));
                int cropH = Math.Min(ch, (int)Math.Floor(cropUnits * unit));
                crop = new Rectangle((cw - cropW) / 2, 0, cropW, cropH);

                Bitmap icon = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(icon))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawImage(canvas, new Rectangle(0, 0, size, size), crop, GraphicsUnit.Pixel);
                }
                return icon;
            }
            finally
            {
                canvas.Dispose();
            }
        }

        // =====================================================================
        // 小工具
        // =====================================================================
        static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static float S(float x) { return (float)Math.Sin(x); }
        static float C(float x) { return (float)Math.Cos(x); }
        static float Mod1(float x) { x = x % 1f; return x < 0f ? x + 1f : x; }
        static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

        static float Hash01(int n)
        {
            float x = (float)Math.Sin(n * 12.9898 + 78.233) * 43758.5453f;
            return x - (float)Math.Floor(x);
        }

        static PointF P(float u, float v) { return new PointF(u, v - 1f); }

        static GraphicsPath RectPath(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            if (r < 0.5f) { p.AddRectangle(new RectangleF(x, y - 1f, w, h)); return p; }
            float rr = Math.Min(r, Math.Min(w, h) * 0.5f);
            p.AddArc(new RectangleF(x, y - 1f, rr * 2f, rr * 2f), 180f, 90f);
            p.AddArc(new RectangleF(x + w - rr * 2f, y - 1f, rr * 2f, rr * 2f), 270f, 90f);
            p.AddArc(new RectangleF(x + w - rr * 2f, y - 1f + h - rr * 2f, rr * 2f, rr * 2f), 0f, 90f);
            p.AddArc(new RectangleF(x, y - 1f + h - rr * 2f, rr * 2f, rr * 2f), 90f, 90f);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath HeartPath(float u, float v, float size)
        {
            float x = u, y = v - 1f, s = size;
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(x, y - s * 0.10f,
                        x - s * 0.16f, y - s * 0.32f,
                        x - s * 0.50f, y - s * 0.26f,
                        x - s * 0.50f, y - s * 0.01f);
            p.AddBezier(x - s * 0.50f, y - s * 0.01f,
                        x - s * 0.50f, y + s * 0.24f,
                        x - s * 0.22f, y + s * 0.40f,
                        x, y + s * 0.56f);
            p.AddBezier(x, y + s * 0.56f,
                        x + s * 0.22f, y + s * 0.40f,
                        x + s * 0.50f, y + s * 0.24f,
                        x + s * 0.50f, y - s * 0.01f);
            p.AddBezier(x + s * 0.50f, y - s * 0.01f,
                        x + s * 0.50f, y - s * 0.26f,
                        x + s * 0.16f, y - s * 0.32f,
                        x, y - s * 0.10f);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath StarPath(float u, float v, float rOut, float rIn, int pts, float rotDeg)
        {
            GraphicsPath p = new GraphicsPath();
            List<PointF> list = new List<PointF>();
            for (int i = 0; i < pts * 2; i++)
            {
                float a = (float)(Math.PI * i / pts - Math.PI * 0.5) + rotDeg * (float)Math.PI / 180f;
                float r = (i % 2 == 0) ? rOut : rIn;
                list.Add(new PointF(u + C(a) * r, v - 1f + S(a) * r));
            }
            p.AddPolygon(list.ToArray());
            p.CloseFigure();
            return p;
        }

        static GraphicsPath TeardropPath(float u, float v, float w, float h)
        {
            float x = u, y = v - 1f;
            float hw = w * 0.5f;
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddBezier(x, y - h * 0.55f,
                        x + hw * 0.55f, y - h * 0.10f,
                        x + hw, y + h * 0.14f,
                        x + hw * 0.62f, y + h * 0.34f);
            p.AddBezier(x + hw * 0.62f, y + h * 0.34f,
                        x + hw * 0.24f, y + h * 0.55f,
                        x - hw * 0.24f, y + h * 0.55f,
                        x - hw * 0.62f, y + h * 0.34f);
            p.AddBezier(x - hw * 0.62f, y + h * 0.34f,
                        x - hw, y + h * 0.14f,
                        x - hw * 0.55f, y - h * 0.10f,
                        x, y - h * 0.55f);
            p.CloseFigure();
            return p;
        }

        static GraphicsPath ZPath(float u, float v, float w, float h)
        {
            float x = u, y = v - 1f;
            GraphicsPath p = new GraphicsPath();
            p.AddLine(x - w * 0.5f, y - h * 0.5f, x + w * 0.5f, y - h * 0.5f);
            p.AddLine(x + w * 0.5f, y - h * 0.5f, x - w * 0.5f, y + h * 0.5f);
            p.AddLine(x - w * 0.5f, y + h * 0.5f, x + w * 0.5f, y + h * 0.5f);
            return p;
        }

        // =====================================================================
        // 单位空间画笔/填充封装
        // =====================================================================
        sealed class Sk
        {
            public Graphics G;
            public float S;
            public float Boost;
            public bool Flipped;

            public Sk(Graphics g, float s) { G = g; S = s; Boost = 1f; Flipped = false; }

            public float Lw(float wu)
            {
                float w = wu * Boost;
                float min = 0.85f / S;
                return w < min ? min : w;
            }

            public Pen NewPen(Color col, float wu)
            {
                Pen p = new Pen(col, Lw(wu));
                p.LineJoin = LineJoin.Round;
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                return p;
            }

            public RectangleF E(float u, float v, float rx, float ry)
            {
                return new RectangleF(u - rx, v - 1f - ry, rx * 2f, ry * 2f);
            }

            public RectangleF Box(float u, float vTop, float w, float h)
            {
                return new RectangleF(u, vTop - 1f, w, h);
            }

            public void Fill(GraphicsPath p, Color c)
            {
                using (SolidBrush b = new SolidBrush(c)) G.FillPath(b, p);
            }

            public void FillGrad(GraphicsPath p, RectangleF rect, Color c1, Color c2, float angle)
            {
                if (rect.Width <= 0.001f || rect.Height <= 0.001f) { Fill(p, c1); return; }
                using (LinearGradientBrush b = new LinearGradientBrush(rect, c1, c2, angle))
                {
                    G.FillPath(b, p);
                }
            }

            public void Stroke(GraphicsPath p, Color c, float wu)
            {
                using (Pen pen = NewPen(c, wu)) G.DrawPath(pen, p);
            }

            public void FillStroke(GraphicsPath p, Color c, Color edge, float wu)
            {
                Fill(p, c);
                Stroke(p, edge, wu);
            }

            public void Line(Color c, float wu, PointF a, PointF b)
            {
                using (Pen pen = NewPen(c, wu)) G.DrawLine(pen, a, b);
            }

            public void Arc(Color c, float wu, RectangleF r, float start, float sweep)
            {
                if (r.Width <= 0.001f || r.Height <= 0.001f) return;
                using (Pen pen = NewPen(c, wu)) G.DrawArc(pen, r, start, sweep);
            }

            public void Dot(float u, float v, float r, Color c)
            {
                using (SolidBrush b = new SolidBrush(c)) G.FillEllipse(b, E(u, v, r, r));
            }

            public void Blob(float u, float v, float rx, float ry, Color c)
            {
                using (SolidBrush b = new SolidBrush(c)) G.FillEllipse(b, E(u, v, rx, ry));
            }

            public void BlobGrad(float u, float v, float rx, float ry, Color c1, Color c2)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(E(u, v, rx, ry), c1, c2, 90f))
                    G.FillEllipse(b, E(u, v, rx, ry));
            }

            public void Ring(float u, float v, float rx, float ry, Color c, float wu)
            {
                G.DrawEllipse(NewPen(c, wu), E(u, v, rx, ry));
            }
        }

        // =====================================================================
        // 情绪参数
        // =====================================================================
        enum EyeStyle { Open, Closed, Happy, Sleepy, Wide, Heart, Spiral, Sad }
        enum BrowStyle { Normal, Raised, Furrow, Sad, None }
        enum MouthStyle { Smile, Open, Small, O, Wavy, Chew, Cat, Flat }
        enum ArmMode { Side, Cheer, Flail, Book }

        sealed class Cfg
        {
            public EyeStyle Eye = EyeStyle.Open;
            public BrowStyle Brow = BrowStyle.Normal;
            public MouthStyle Mouth = MouthStyle.Smile;
            public ArmMode Arms = ArmMode.Side;
            public float Blink;
            public float Blush = 0.4f;
            public float HeadBob;
            public float HeadDx;
            public float HeadDy;
            public float HeadTilt;
            public float Jump;
            public float Tilt;
            public bool ZzzSmall;
            public bool ZzzBig;
            public bool Snot;
            public bool Tear;
            public bool Sweat;
            public bool Hearts;
            public bool Stars;
            public bool Steam;
            public bool Green;
            public bool Sparkle;
            public bool Crumbs;
            public bool Book;
            public bool Note;
            public bool Motion;
            public bool IsRice;
            public bool Puffed;
            public float Cheek = 0f;
        }

        static Cfg MakeCfg(PetPose pose)
        {
            Cfg c = new Cfg();
            float t = pose.Time;
            c.Blink = Clamp01(pose.Blink);
            c.Blush = 0.30f + 0.45f * Clamp01(pose.Happy);
            c.HeadBob = S(t * 1.7f) * 0.010f;

            switch (pose.Mood)
            {
                case PetMood.Idle:
                    c.Eye = EyeStyle.Open;
                    c.Brow = BrowStyle.Normal;
                    c.Mouth = MouthStyle.Smile;
                    c.HeadBob = S(t * 1.7f) * 0.016f;
                    break;
                case PetMood.Blink:
                    c.Eye = EyeStyle.Closed;
                    c.Blink = 1f;
                    c.Mouth = MouthStyle.Small;
                    break;
                case PetMood.Happy:
                    c.Eye = EyeStyle.Happy;
                    c.Brow = BrowStyle.Raised;
                    c.Mouth = MouthStyle.Open;
                    c.Blush = 0.95f;
                    c.Sparkle = true;
                    c.Jump = 0.012f + Math.Abs(S(t * 3.0f)) * 0.012f;
                    c.Mouth = MouthStyle.Cat;
                    break;
                case PetMood.Surprised:
                    c.Eye = EyeStyle.Wide;
                    c.Brow = BrowStyle.Raised;
                    c.Mouth = MouthStyle.O;
                    c.Jump = 0.055f + Math.Abs(S(t * 7.0f)) * 0.018f;
                    c.Sweat = true;
                    c.Blush = 0.5f;
                    break;
                case PetMood.Sleepy:
                    c.Eye = EyeStyle.Sleepy;
                    c.Brow = BrowStyle.Normal;
                    c.Mouth = MouthStyle.Small;
                    c.ZzzSmall = true;
                    c.HeadBob = S(t * 0.9f) * 0.014f + 0.012f;
                    c.HeadTilt = 4f;
                    c.Blush = 0.45f;
                    break;
                case PetMood.Sleep:
                    c.Eye = EyeStyle.Closed;
                    c.Blink = 1f;
                    c.Brow = BrowStyle.None;
                    c.Mouth = MouthStyle.Small;
                    c.ZzzBig = true;
                    c.Snot = true;
                    c.HeadTilt = 6f;
                    c.HeadBob = S(t * 0.7f) * 0.010f;
                    break;
                case PetMood.Eat:
                    c.Eye = EyeStyle.Happy;
                    c.Mouth = MouthStyle.Chew;
                    c.Crumbs = true;
                    c.Puffed = true;
                    c.Blush = 0.6f;
                    break;
                case PetMood.Drag:
                    c.Eye = EyeStyle.Wide;
                    c.Brow = BrowStyle.Furrow;
                    c.Mouth = MouthStyle.O;
                    c.Arms = ArmMode.Flail;
                    c.HeadBob = S(t * 9f) * 0.02f;
                    c.Sweat = true;
                    c.Motion = true;
                    c.Tilt = S(t * 4.2f) * 3.5f;
                    break;
                case PetMood.Walk:
                    c.Eye = EyeStyle.Open;
                    c.Mouth = MouthStyle.Smile;
                    c.Tilt = 7f;
                    c.HeadTilt = -4f;
                    c.Arms = ArmMode.Side;
                    c.Motion = true;
                    break;
                case PetMood.Focus:
                    c.Eye = EyeStyle.Open;
                    c.Brow = BrowStyle.Furrow;
                    c.Mouth = MouthStyle.Flat;
                    c.Book = true;
                    c.Arms = ArmMode.Book;
                    c.Sweat = true;
                    c.HeadDx = -0.005f;
                    break;
                case PetMood.Relax:
                    c.Eye = EyeStyle.Happy;
                    c.Brow = BrowStyle.Normal;
                    c.Mouth = MouthStyle.Smile;
                    c.Note = true;
                    c.Blush = 0.5f;
                    c.HeadTilt = 5f;
                    c.HeadBob = S(t * 1.1f) * 0.012f;
                    break;
                case PetMood.Sad:
                    c.Eye = EyeStyle.Sad;
                    c.Brow = BrowStyle.Sad;
                    c.Mouth = MouthStyle.Wavy;
                    c.Tear = true;
                    c.HeadBob = 0.018f;
                    c.Blush = 0.15f;
                    break;
                case PetMood.Love:
                    c.Eye = EyeStyle.Heart;
                    c.Brow = BrowStyle.Normal;
                    c.Mouth = MouthStyle.Cat;
                    c.Hearts = true;
                    c.Blush = 1f;
                    c.HeadTilt = 4f;
                    break;
                case PetMood.Dizzy:
                    c.Eye = EyeStyle.Spiral;
                    c.Brow = BrowStyle.None;
                    c.Mouth = MouthStyle.Wavy;
                    c.Stars = true;
                    c.Tilt = S(t * 6.0f) * 6.0f;
                    c.HeadTilt = S(t * 6.0f) * 8f;
                    c.Blush = 0.5f;
                    break;
                case PetMood.Sick:
                    c.Eye = EyeStyle.Sleepy;
                    c.Brow = BrowStyle.Sad;
                    c.Mouth = MouthStyle.Wavy;
                    c.Steam = true;
                    c.Green = true;
                    c.Sweat = true;
                    c.HeadBob = 0.02f;
                    c.Blush = 0.4f;
                    break;
                case PetMood.Cheer:
                    c.Eye = EyeStyle.Happy;
                    c.Brow = BrowStyle.Raised;
                    c.Mouth = MouthStyle.Open;
                    c.Arms = ArmMode.Cheer;
                    c.Sparkle = true;
                    c.Jump = Math.Abs(S(t * 4.0f)) * 0.022f;
                    c.Blush = 0.8f;
                    break;
                case PetMood.EatRice:
                    c.IsRice = true;
                    c.Arms = ArmMode.Side;
                    c.Mouth = MouthStyle.Chew;
                    c.Blush = 0.55f;
                    {
                        float p = Mod1(pose.Time * 0.30f);
                        float rb1, rb2, rb3, up;
                        RiceBowlState(pose.Time, out rb1, out rb2, out rb3, out up);
                        c.HeadTilt = -6f * up;
                        c.HeadDy = -0.018f * up;
                        if (p < 0.25f)
                        {
                            c.Eye = EyeStyle.Open;
                            c.Brow = BrowStyle.Normal;
                            c.Puffed = true;
                        }
                        else if (p < 0.5f)
                        {
                            c.Eye = EyeStyle.Open;
                            c.Brow = BrowStyle.Raised;
                            c.Mouth = MouthStyle.Smile;
                        }
                        else if (p < 0.75f)
                        {
                            c.Eye = EyeStyle.Happy;
                            c.Brow = BrowStyle.Raised;
                            c.Mouth = MouthStyle.Open;
                        }
                        else
                        {
                            c.Eye = EyeStyle.Happy;
                            c.Brow = BrowStyle.Normal;
                            c.Blush = 0.85f;
                            c.Puffed = true;
                            c.Mouth = MouthStyle.Cat;
                        }
                    }
                    break;
                default:
                    break;
            }
            return c;
        }

        // =====================================================================
        // 鲸尾
        // =====================================================================
        static void DrawTail(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            float sway = S(t * 1.7f) * 5.0f + (c.Motion ? S(t * 7f) * 3f : 0f);
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(0.18f, 0.42f - 1f);
                k.G.RotateTransform(sway);
                k.G.TranslateTransform(-0.18f, -(0.42f - 1f));

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddBezier(P(0.20f, 0.34f), P(0.41f, 0.32f), P(0.58f, 0.37f), P(0.70f, 0.45f));
                    p.AddBezier(P(0.70f, 0.45f), P(0.76f, 0.40f), P(0.82f, 0.35f), P(0.875f, 0.315f));
                    p.AddBezier(P(0.875f, 0.315f), P(0.865f, 0.40f), P(0.825f, 0.455f), P(0.775f, 0.49f));
                    p.AddBezier(P(0.775f, 0.49f), P(0.83f, 0.535f), P(0.868f, 0.585f), P(0.89f, 0.635f));
                    p.AddBezier(P(0.89f, 0.635f), P(0.82f, 0.61f), P(0.75f, 0.555f), P(0.675f, 0.505f));
                    p.AddBezier(P(0.675f, 0.505f), P(0.54f, 0.445f), P(0.36f, 0.44f), P(0.235f, 0.445f));
                    p.AddBezier(P(0.235f, 0.445f), P(0.20f, 0.42f), P(0.19f, 0.38f), P(0.20f, 0.34f));
                    p.CloseFigure();

                    k.FillGrad(p, k.Box(0.15f, 0.30f, 0.75f, 0.36f),
                               Color.FromArgb(255, 128, 172, 246), Color.FromArgb(255, 62, 104, 192), 90f);
                    k.Stroke(p, Palette.Outline, 0.018f);
                }

                using (Pen rib = k.NewPen(Color.FromArgb(150, 200, 226, 255), 0.013f))
                {
                    k.G.DrawBezier(rib, P(0.34f, 0.395f), P(0.50f, 0.40f), P(0.64f, 0.43f), P(0.74f, 0.475f));
                    k.G.DrawBezier(rib, P(0.43f, 0.43f), P(0.56f, 0.44f), P(0.65f, 0.47f), P(0.72f, 0.505f));
                }
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        // =====================================================================
        // 后发
        // =====================================================================
        static void DrawBackHair(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(0f, -0.30f - 1f);
                k.G.RotateTransform(S(t * 1.2f) * 1.3f);
                k.G.TranslateTransform(0f, 0.30f + 1f);

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddBezier(P(-0.20f, -0.945f), P(-0.45f, -0.93f), P(-0.565f, -0.79f), P(-0.59f, -0.56f));
                    p.AddBezier(P(-0.59f, -0.56f), P(-0.63f, -0.34f), P(-0.665f, -0.07f), P(-0.70f, 0.16f));
                    p.AddBezier(P(-0.70f, 0.16f), P(-0.745f, 0.38f), P(-0.70f, 0.56f), P(-0.62f, 0.625f));
                    p.AddBezier(P(-0.62f, 0.625f), P(-0.56f, 0.665f), P(-0.515f, 0.58f), P(-0.50f, 0.515f));
                    p.AddBezier(P(-0.50f, 0.515f), P(-0.46f, 0.64f), P(-0.40f, 0.70f), P(-0.335f, 0.655f));
                    p.AddBezier(P(-0.335f, 0.655f), P(-0.285f, 0.615f), P(-0.30f, 0.52f), P(-0.265f, 0.47f));
                    p.AddLine(P(-0.265f, 0.47f), P(0.265f, 0.47f));
                    p.AddBezier(P(0.265f, 0.47f), P(0.30f, 0.52f), P(0.285f, 0.615f), P(0.335f, 0.655f));
                    p.AddBezier(P(0.335f, 0.655f), P(0.40f, 0.70f), P(0.46f, 0.64f), P(0.50f, 0.515f));
                    p.AddBezier(P(0.50f, 0.515f), P(0.515f, 0.58f), P(0.56f, 0.665f), P(0.62f, 0.625f));
                    p.AddBezier(P(0.62f, 0.625f), P(0.70f, 0.56f), P(0.745f, 0.38f), P(0.70f, 0.16f));
                    p.AddBezier(P(0.70f, 0.16f), P(0.665f, -0.07f), P(0.63f, -0.34f), P(0.59f, -0.56f));
                    p.AddBezier(P(0.59f, -0.56f), P(0.565f, -0.79f), P(0.45f, -0.93f), P(0.20f, -0.945f));
                    p.AddBezier(P(0.20f, -0.945f), P(0.10f, -0.985f), P(-0.10f, -0.985f), P(-0.20f, -0.945f));
                    p.CloseFigure();

                    k.FillGrad(p, k.Box(-0.75f, -0.99f, 1.5f, 1.72f),
                               HairDeep, Color.FromArgb(255, 48, 86, 168), 90f);
                    k.Stroke(p, Palette.Outline, 0.018f);
                }

                // 内侧一层稍亮的发束
                using (GraphicsPath p2 = new GraphicsPath())
                {
                    p2.StartFigure();
                    p2.AddBezier(P(-0.38f, -0.78f), P(-0.50f, -0.50f), P(-0.50f, -0.10f), P(-0.55f, 0.18f));
                    p2.AddBezier(P(-0.55f, 0.18f), P(-0.58f, 0.38f), P(-0.54f, 0.52f), P(-0.48f, 0.575f));
                    p2.AddBezier(P(-0.48f, 0.575f), P(-0.44f, 0.50f), P(-0.43f, 0.36f), P(-0.42f, 0.18f));
                    p2.AddBezier(P(-0.40f, -0.10f), P(-0.42f, -0.48f), P(-0.30f, -0.68f), P(-0.24f, -0.80f));
                    p2.CloseFigure();
                    k.Fill(p2, Color.FromArgb(140, 92, 140, 220));

                    p2.Reset();
                    p2.StartFigure();
                    p2.AddBezier(P(0.38f, -0.78f), P(0.50f, -0.50f), P(0.50f, -0.10f), P(0.55f, 0.18f));
                    p2.AddBezier(P(0.55f, 0.18f), P(0.58f, 0.38f), P(0.54f, 0.52f), P(0.48f, 0.575f));
                    p2.AddBezier(P(0.48f, 0.575f), P(0.44f, 0.50f), P(0.43f, 0.36f), P(0.42f, 0.18f));
                    p2.AddBezier(P(0.40f, -0.10f), P(0.42f, -0.48f), P(0.30f, -0.68f), P(0.24f, -0.80f));
                    p2.CloseFigure();
                    k.Fill(p2, Color.FromArgb(140, 92, 140, 220));
                }

                // 发丝高光
                using (Pen hp = k.NewPen(Color.FromArgb(120, 150, 196, 250), 0.012f))
                {
                    k.G.DrawBezier(hp, P(-0.47f, 0.06f), P(-0.53f, 0.26f), P(-0.55f, 0.40f), P(-0.51f, 0.50f));
                    k.G.DrawBezier(hp, P(0.47f, 0.08f), P(0.53f, 0.28f), P(0.55f, 0.42f), P(0.51f, 0.52f));
                }
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        // =====================================================================
        // 腿 / 白袜 / 小黑鞋
        // =====================================================================
        static void DrawLegs(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            float liftL = 0f, liftR = 0f;
            float slideL = 0f, slideR = 0f;

            switch (pose.Mood)
            {
                case PetMood.Idle:
                    liftR = Math.Max(0f, S(t * 2.1f)) * 0.020f;
                    liftL = Math.Max(0f, S(t * 2.1f + 3.14159f)) * 0.007f;
                    slideR = Math.Max(0f, S(t * 2.1f)) * 0.008f;
                    break;
                case PetMood.Walk:
                    liftL = Math.Max(0f, S(t * 6.2f)) * 0.052f;
                    liftR = Math.Max(0f, S(t * 6.2f + 3.14159f)) * 0.052f;
                    slideL = S(t * 6.2f) * 0.022f;
                    slideR = -slideL;
                    break;
                case PetMood.Drag:
                    liftL = 0.045f + Math.Max(0f, S(t * 9f)) * 0.035f;
                    liftR = 0.045f + Math.Max(0f, S(t * 9f + 2.2f)) * 0.035f;
                    slideL = 0.03f + S(t * 9f) * 0.02f;
                    slideR = -0.03f + S(t * 9f + 2.2f) * 0.02f;
                    break;
                case PetMood.Surprised:
                    liftL = 0.030f;
                    liftR = 0.030f;
                    break;
                case PetMood.Happy:
                case PetMood.Cheer:
                    liftL = Math.Max(0f, S(t * 4f)) * 0.012f;
                    break;
                case PetMood.Sleep:
                case PetMood.Sleepy:
                case PetMood.Sick:
                    liftL = 0f;
                    liftR = 0f;
                    break;
            }

            DrawOneLeg(k, -0.165f - slideL, liftL, -1f);
            DrawOneLeg(k, 0.165f + slideR, liftR, 1f);
        }

        static void DrawOneLeg(Sk k, float u, float lift, float side)
        {
            float top = 0.815f;
            float sockBottom = 0.940f - lift;
            Color sockShade = Color.FromArgb(255, 214, 226, 246);

            // 白袜
            using (GraphicsPath p = RectPath(u - 0.057f, top, 0.114f, sockBottom - top + 0.03f, 0.05f))
            {
                k.FillGrad(p, k.Box(u - 0.06f, top, 0.12f, 0.16f), Color.FromArgb(255, 255, 255, 255), sockShade, 90f);
                k.Stroke(p, Color.FromArgb(255, 150, 160, 180), 0.012f);
            }
            using (Pen cuff = k.NewPen(Color.FromArgb(255, 196, 208, 228), 0.010f))
            {
                k.G.DrawLine(cuff, P(u - 0.052f, top + 0.022f), P(u + 0.052f, top + 0.022f));
            }

            // 小黑鞋
            float shoeV = 0.952f - lift;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(P(u - 0.108f, shoeV - 0.012f),
                            P(u - 0.108f, shoeV + 0.032f),
                            P(u - 0.062f, shoeV + 0.040f),
                            P(u + (0.015f * side), shoeV + 0.040f));
                p.AddBezier(P(u + (0.015f * side), shoeV + 0.040f),
                            P(u + 0.085f, shoeV + 0.040f),
                            P(u + 0.108f, shoeV + 0.030f),
                            P(u + 0.108f, shoeV - 0.008f));
                p.AddBezier(P(u + 0.108f, shoeV - 0.008f),
                            P(u + 0.108f, shoeV - 0.055f),
                            P(u + 0.060f, shoeV - 0.062f),
                            P(u, shoeV - 0.062f));
                p.AddBezier(P(u, shoeV - 0.062f),
                            P(u - 0.070f, shoeV - 0.062f),
                            P(u - 0.108f, shoeV - 0.050f),
                            P(u - 0.108f, shoeV - 0.012f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(u - 0.11f, shoeV - 0.07f, 0.22f, 0.14f),
                           Color.FromArgb(255, 58, 72, 110), Color.FromArgb(255, 28, 38, 66), 90f);
                k.Stroke(p, Palette.Outline, 0.014f);
            }
            using (Pen shine = k.NewPen(Color.FromArgb(160, 130, 158, 210), 0.012f))
            {
                k.G.DrawArc(shine, k.E(u + 0.005f * side, shoeV - 0.012f, 0.055f, 0.028f), 200f, 90f);
            }
            using (Pen strap = k.NewPen(Color.FromArgb(180, 90, 110, 160), 0.010f))
            {
                k.G.DrawLine(strap, P(u - 0.075f, shoeV - 0.030f), P(u + 0.075f, shoeV - 0.030f));
            }
        }

        // =====================================================================
        // 裙摆下白色荷叶边（先画，被裙身压住上缘）
        // =====================================================================
        static void DrawLace(Sk k, Cfg c)
        {
            float topV = 0.762f;
            float baseV = 0.812f;
            float btmV = 0.852f;
            float left = -0.575f, right = 0.575f;
            int bumps = 11;
            float step = (right - left) / bumps;

            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddLine(P(left, topV), P(right, topV));
                p.AddLine(P(right, topV), P(right, baseV));
                for (int i = 0; i < bumps; i++)
                {
                    float x1 = right - step * i;
                    float x0 = right - step * (i + 1);
                    p.AddBezier(P(x1, baseV),
                                P(x1 - step * 0.10f, btmV),
                                P(x0 + step * 0.10f, btmV),
                                P(x0, baseV));
                }
                p.AddLine(P(left, baseV), P(left, topV));
                p.CloseFigure();
                k.FillGrad(p, k.Box(left, topV, right - left, btmV - topV),
                           LaceWhite, Color.FromArgb(255, 226, 235, 249), 90f);
                k.Stroke(p, Color.FromArgb(255, 145, 160, 190), 0.011f);
            }

            using (Pen pleat = k.NewPen(Color.FromArgb(90, 150, 168, 200), 0.008f))
            {
                for (int i = 1; i < bumps; i++)
                {
                    float x = left + step * i;
                    k.G.DrawLine(pleat, P(x, baseV - 0.012f), P(x, baseV + 0.006f));
                }
            }
        }

        // =====================================================================
        // 连衣裙（伞状裙摆 + 藏青上身）
        // =====================================================================
        static void DrawBody(Sk k, PetPose pose, Cfg c)
        {
            float sway = (pose.Mood == PetMood.Walk) ? S(pose.Time * 6.2f) * 0.012f : 0f;

            // 裙摆
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.215f + sway, 0.295f), P(-0.32f, 0.36f), P(-0.475f, 0.52f), P(-0.565f, 0.70f));
                p.AddBezier(P(-0.565f, 0.70f), P(-0.615f, 0.755f), P(-0.575f, 0.80f), P(-0.46f, 0.802f));
                p.AddBezier(P(-0.46f, 0.802f), P(-0.28f, 0.812f), P(0.28f, 0.812f), P(0.46f, 0.802f));
                p.AddBezier(P(0.46f, 0.802f), P(0.575f, 0.80f), P(0.615f, 0.755f), P(0.565f, 0.70f));
                p.AddBezier(P(0.565f, 0.70f), P(0.475f, 0.52f), P(0.32f, 0.36f), P(0.215f + sway, 0.295f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.62f, 0.28f, 1.24f, 0.55f),
                           Color.FromArgb(255, 53, 74, 138), Color.FromArgb(255, 28, 41, 82), 90f);
                k.Stroke(p, Palette.Outline, 0.018f);
            }

            // 裙摆褶皱 + 金色缘饰
            using (Pen fold = k.NewPen(Color.FromArgb(120, 96, 126, 196), 0.012f))
            {
                k.G.DrawBezier(fold, P(-0.17f, 0.36f), P(-0.25f, 0.52f), P(-0.32f, 0.66f), P(-0.35f, 0.775f));
                k.G.DrawBezier(fold, P(0.05f, 0.36f), P(0.04f, 0.54f), P(0.03f, 0.68f), P(0.02f, 0.788f));
                k.G.DrawBezier(fold, P(0.19f, 0.36f), P(0.27f, 0.52f), P(0.34f, 0.66f), P(0.37f, 0.775f));
            }
            using (Pen trim = k.NewPen(Color.FromArgb(220, 238, 199, 126), 0.010f))
            {
                k.G.DrawBezier(trim, P(-0.44f, 0.755f), P(-0.24f, 0.768f), P(0.24f, 0.768f), P(0.44f, 0.755f));
            }

            // 裙侧小蝴蝶结
            DrawTinyBow(k, -0.40f, 0.665f, 0.075f, Color.FromArgb(255, 238, 199, 126), 5f);
            DrawTinyBow(k, 0.41f, 0.675f, 0.070f, Color.FromArgb(255, 238, 199, 126), -6f);

            // 上身
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.245f, 0.02f), P(-0.265f, 0.10f), P(-0.25f, 0.22f), P(-0.225f, 0.315f));
                p.AddLine(P(-0.225f, 0.315f), P(0.225f, 0.315f));
                p.AddBezier(P(0.225f, 0.315f), P(0.25f, 0.22f), P(0.265f, 0.10f), P(0.245f, 0.02f));
                p.AddBezier(P(0.245f, 0.02f), P(0.14f, -0.02f), P(-0.14f, -0.02f), P(-0.245f, 0.02f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.28f, 0.0f, 0.56f, 0.33f),
                           Color.FromArgb(255, 56, 78, 144), Color.FromArgb(255, 31, 45, 88), 90f);
                k.Stroke(p, Palette.Outline, 0.018f);
            }

            // 腰带
            using (GraphicsPath p = RectPath(-0.225f, 0.292f, 0.45f, 0.038f, 0.012f))
            {
                k.Fill(p, Color.FromArgb(255, 40, 56, 104));
                k.Stroke(p, Palette.Outline, 0.012f);
            }
            k.Dot(0f, 0.311f, 0.026f, Color.FromArgb(255, 238, 199, 126));
            k.Dot(0f, 0.311f, 0.011f, Color.FromArgb(255, 255, 246, 220));

            // 扣子
            k.Dot(0f, 0.155f, 0.014f, Color.FromArgb(255, 238, 199, 126));
            k.Dot(0f, 0.22f, 0.014f, Color.FromArgb(255, 238, 199, 126));
        }

        static void DrawTinyBow(Sk k, float u, float v, float size, Color col, float rot)
        {
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(u, v - 1f);
                k.G.RotateTransform(rot);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(-size, 0f, -size * 0.9f, -size * 0.8f, -size * 0.1f, -size * 0.35f, 0f, 0f);
                    p.AddBezier(0f, 0f, -size * 0.1f, size * 0.35f, -size * 0.9f, size * 0.8f, -size, 0f);
                    p.CloseFigure();
                    k.Fill(p, col);
                    k.Stroke(p, Palette.Outline, 0.008f);
                }
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(size, 0f, size * 0.9f, -size * 0.8f, size * 0.1f, -size * 0.35f, 0f, 0f);
                    p.AddBezier(0f, 0f, size * 0.1f, size * 0.35f, size * 0.9f, size * 0.8f, size, 0f);
                    p.CloseFigure();
                    k.Fill(p, col);
                    k.Stroke(p, Palette.Outline, 0.008f);
                }
                using (SolidBrush knot = new SolidBrush(col))
                    k.G.FillEllipse(knot, new RectangleF(-size * 0.22f, -size * 0.22f, size * 0.44f, size * 0.44f));
                using (Pen kp = k.NewPen(Palette.Outline, 0.008f))
                    k.G.DrawEllipse(kp, new RectangleF(-size * 0.22f, -size * 0.22f, size * 0.44f, size * 0.44f));
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        // =====================================================================
        // 围裙
        // =====================================================================
        static void DrawApron(Sk k, PetPose pose, Cfg c)
        {
            // 肩带
            using (Pen strap = k.NewPen(ApronWhite, 0.032f))
            {
                k.G.DrawLine(strap, P(-0.125f, 0.055f), P(-0.112f, 0.20f));
                k.G.DrawLine(strap, P(0.125f, 0.055f), P(0.112f, 0.20f));
            }

            // 围裙上片
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.130f, 0.135f), P(-0.140f, 0.22f), P(-0.145f, 0.28f), P(-0.150f, 0.315f));
                p.AddLine(P(-0.150f, 0.315f), P(0.150f, 0.315f));
                p.AddBezier(P(0.150f, 0.315f), P(0.145f, 0.28f), P(0.140f, 0.22f), P(0.130f, 0.135f));
                p.AddBezier(P(0.130f, 0.135f), P(0.068f, 0.118f), P(-0.068f, 0.118f), P(-0.130f, 0.135f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.16f, 0.11f, 0.32f, 0.21f),
                           ApronWhite, Color.FromArgb(255, 233, 240, 250), 90f);
                k.Stroke(p, Color.FromArgb(255, 150, 164, 192), 0.012f);
            }

            // 围裙裙片
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.160f, 0.31f), P(-0.235f, 0.40f), P(-0.272f, 0.50f), P(-0.282f, 0.595f));
                int bumps = 7;
                float x0 = -0.282f, x1 = 0.282f;
                float step = (x1 - x0) / bumps;
                float baseV = 0.615f, dipV = 0.668f;
                p.AddLine(P(x0, 0.595f), P(x0, baseV));
                for (int i = 0; i < bumps; i++)
                {
                    float xa = x0 + step * i;
                    float xb = xa + step;
                    p.AddBezier(P(xa, baseV), P(xa + step * 0.12f, dipV), P(xb - step * 0.12f, dipV), P(xb, baseV));
                }
                p.AddLine(P(x1, baseV), P(x1, 0.595f));
                p.AddBezier(P(x1, 0.595f), P(0.272f, 0.50f), P(0.235f, 0.40f), P(0.160f, 0.31f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.30f, 0.30f, 0.60f, 0.37f),
                           ApronWhite, Color.FromArgb(255, 236, 242, 252), 90f);
                k.Stroke(p, Color.FromArgb(255, 148, 162, 190), 0.012f);
            }

            // 围裙腰线
            using (Pen wl = k.NewPen(Color.FromArgb(255, 226, 234, 248), 0.022f))
            {
                k.G.DrawLine(wl, P(-0.21f, 0.318f), P(0.21f, 0.318f));
            }

            DrawWhaleMotif(k, 0f, 0.475f, 0.155f);
        }

        static void DrawWhaleMotif(Sk k, float u, float v, float w)
        {
            float h = w * 0.56f;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(P(u - w * 0.50f, v + h * 0.10f),
                            P(u - w * 0.44f, v - h * 0.30f),
                            P(u + w * 0.10f, v - h * 0.42f),
                            P(u + w * 0.20f, v - h * 0.02f));
                p.AddBezier(P(u + w * 0.20f, v - h * 0.02f),
                            P(u + w * 0.29f, v + h * 0.20f),
                            P(u + w * 0.02f, v + h * 0.32f),
                            P(u - w * 0.12f, v + h * 0.24f));
                p.AddBezier(P(u - w * 0.12f, v + h * 0.24f),
                            P(u - w * 0.28f, v + h * 0.32f),
                            P(u - w * 0.46f, v + h * 0.28f),
                            P(u - w * 0.50f, v + h * 0.10f));
                p.CloseFigure();
                k.Fill(p, Color.FromArgb(255, 58, 96, 170));
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(P(u + w * 0.15f, v + h * 0.06f),
                            P(u + w * 0.42f, v - h * 0.10f),
                            P(u + w * 0.48f, v - h * 0.32f),
                            P(u + w * 0.40f, v - h * 0.38f));
                p.AddBezier(P(u + w * 0.40f, v - h * 0.38f),
                            P(u + w * 0.44f, v - h * 0.10f),
                            P(u + w * 0.28f, v + h * 0.10f),
                            P(u + w * 0.15f, v + h * 0.06f));
                p.CloseFigure();
                k.Fill(p, Color.FromArgb(255, 58, 96, 170));
            }
            k.Dot(u + w * 0.06f, v - h * 0.04f, w * 0.035f, Color.FromArgb(255, 255, 255, 255));
            using (Pen sp = k.NewPen(Color.FromArgb(255, 84, 130, 200), 0.008f))
            {
                k.G.DrawBezier(sp, P(u + w * 0.16f, v - h * 0.28f), P(u + w * 0.10f, v - h * 0.55f), P(u + w * 0.14f, v - h * 0.70f), P(u + w * 0.22f, v - h * 0.80f));
                k.G.DrawBezier(sp, P(u + w * 0.28f, v - h * 0.30f), P(u + w * 0.30f, v - h * 0.50f), P(u + w * 0.36f, v - h * 0.60f), P(u + w * 0.42f, v - h * 0.66f));
            }
        }

        // =====================================================================
        // 翻开的小书（Focus）
        // =====================================================================
        static void DrawBook(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            float u = 0f, v = 0.29f + S(t * 1.4f) * 0.004f;
            float w = 0.44f, h = 0.22f;

            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(u - w * 0.5f, v - h * 0.20f), P(u - w * 0.25f, v - h * 0.36f), P(u - w * 0.06f, v - h * 0.28f), P(u, v - h * 0.10f));
                p.AddBezier(P(u + w * 0.06f, v - h * 0.28f), P(u + w * 0.25f, v - h * 0.36f), P(u + w * 0.5f, v - h * 0.20f), P(u + w * 0.5f, v - h * 0.20f));
                p.AddLine(P(u + w * 0.5f, v - h * 0.20f), P(u + w * 0.47f, v + h * 0.34f));
                p.AddBezier(P(u + w * 0.47f, v + h * 0.34f), P(u + w * 0.22f, v + h * 0.22f), P(u + w * 0.08f, v + h * 0.20f), P(u, v + h * 0.28f));
                p.AddBezier(P(u - w * 0.08f, v - h * 0.20f), P(u - w * 0.22f, v + h * 0.22f), P(u - w * 0.47f, v + h * 0.34f), P(u - w * 0.47f, v + h * 0.34f));
                p.AddLine(P(u - w * 0.47f, v + h * 0.34f), P(u - w * 0.5f, v - h * 0.20f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(u - w * 0.5f, v - h * 0.4f, w, h * 0.8f), Color.FromArgb(255, 255, 253, 246), Color.FromArgb(255, 240, 234, 218), 90f);
                k.Stroke(p, Palette.Outline, 0.014f);
            }
            using (Pen spine = k.NewPen(Color.FromArgb(255, 180, 170, 150), 0.010f))
            {
                k.G.DrawLine(spine, P(u, v - h * 0.10f), P(u, v + h * 0.28f));
            }
            using (Pen line = k.NewPen(Color.FromArgb(160, 150, 160, 178), 0.007f))
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = v + 0.005f + i * 0.032f;
                    k.G.DrawLine(line, P(u - w * 0.36f, y), P(u - w * 0.12f, y - 0.004f));
                    k.G.DrawLine(line, P(u + w * 0.11f, y - 0.004f), P(u + w * 0.35f, y));
                }
            }
            using (GraphicsPath cover = new GraphicsPath())
            {
                cover.AddLine(P(u - w * 0.52f, v - h * 0.18f), P(u - w * 0.52f, v + h * 0.32f));
                cover.AddLine(P(u - w * 0.42f, v + h * 0.32f), P(u - w * 0.42f, v - h * 0.18f));
                cover.CloseFigure();
                k.Fill(cover, Color.FromArgb(255, 58, 80, 142));
            }
            using (GraphicsPath cover2 = new GraphicsPath())
            {
                cover2.AddLine(P(u + w * 0.52f, v - h * 0.18f), P(u + w * 0.52f, v + h * 0.32f));
                cover2.AddLine(P(u + w * 0.42f, v + h * 0.32f), P(u + w * 0.42f, v - h * 0.18f));
                cover2.CloseFigure();
                k.Fill(cover2, Color.FromArgb(255, 58, 80, 142));
            }
            // 书签
            using (GraphicsPath bm = new GraphicsPath())
            {
                bm.AddLine(P(u + 0.02f, v + h * 0.22f), P(u + 0.02f, v + h * 0.52f));
                bm.AddLine(P(u + 0.02f, v + h * 0.52f), P(u + 0.055f, v + h * 0.44f));
                bm.AddLine(P(u + 0.055f, v + h * 0.44f), P(u + 0.09f, v + h * 0.52f));
                bm.AddLine(P(u + 0.09f, v + h * 0.52f), P(u + 0.09f, v + h * 0.20f));
                bm.CloseFigure();
                k.Fill(bm, Color.FromArgb(255, 236, 120, 130));
            }
        }

        // =====================================================================
        // 手臂
        // =====================================================================
        static void DrawArms(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            float jitter = 0f;
            if (c.Arms == ArmMode.Flail) jitter = S(t * 11f) * 0.025f;

            if (c.Arms == ArmMode.Cheer)
            {
                DrawArm(k, -1f, -0.29f, 0.12f, -0.46f, -0.055f);
                DrawArm(k, 1f, 0.29f, 0.12f, 0.46f, -0.055f);
                DrawHand(k, -0.46f, -0.055f);
                DrawHand(k, 0.46f, -0.055f);
                DrawSparkleAt(k, -0.56f, -0.18f, 0.07f, Color.FromArgb(255, 255, 226, 120), pose.Time);
                DrawSparkleAt(k, 0.56f, -0.18f, 0.07f, Color.FromArgb(255, 255, 226, 120), pose.Time + 0.4f);
            }
            else if (c.Arms == ArmMode.Book)
            {
                DrawArm(k, -1f, -0.29f, 0.12f, -0.18f, 0.27f);
                DrawArm(k, 1f, 0.29f, 0.12f, 0.18f, 0.27f);
                DrawHand(k, -0.185f, 0.285f);
                DrawHand(k, 0.185f, 0.285f);
            }
            else if (c.Arms == ArmMode.Flail)
            {
                DrawArm(k, -1f, -0.29f, 0.11f, -0.47f, 0.15f + jitter);
                DrawArm(k, 1f, 0.29f, 0.11f, 0.45f, 0.28f - jitter);
                DrawHand(k, -0.47f, 0.15f + jitter);
                DrawHand(k, 0.45f, 0.28f - jitter);
            }
            else
            {
                float swing = 0f;
                if (pose.Mood == PetMood.Walk) swing = S(t * 6.2f) * 0.045f;
                if (pose.Mood == PetMood.Idle) swing = S(t * 2.1f) * 0.012f;
                DrawArm(k, -1f, -0.285f, 0.115f, -0.355f - swing, 0.315f);
                DrawArm(k, 1f, 0.285f, 0.115f, 0.355f + swing, 0.315f);
                DrawHand(k, -0.355f - swing, 0.315f);
                DrawHand(k, 0.355f + swing, 0.315f);
            }
        }

        static void DrawArm(Sk k, float side, float sx, float sv, float hx, float hv)
        {
            // 泡泡袖
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(sx, sv + 0.005f, 0.085f, 0.078f));
                k.FillGrad(p, k.Box(sx - 0.09f, sv - 0.08f, 0.18f, 0.16f),
                           Color.FromArgb(255, 68, 92, 162), Color.FromArgb(255, 38, 54, 104), 90f);
                k.Stroke(p, Palette.Outline, 0.015f);
            }
            // 手臂：外层描边 + 内层肤色
            using (Pen outer = k.NewPen(Palette.Outline, 0.082f))
            {
                k.G.DrawLine(outer, P(sx + side * 0.015f, sv + 0.02f), P(hx, hv));
            }
            using (Pen inner = k.NewPen(Skin, 0.055f))
            {
                k.G.DrawLine(inner, P(sx + side * 0.015f, sv + 0.02f), P(hx, hv));
            }
        }

        static void DrawHand(Sk k, float u, float v)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(u, v, 0.062f, 0.058f));
                k.FillGrad(p, k.Box(u - 0.06f, v - 0.06f, 0.12f, 0.12f), SkinLight, Skin, 90f);
                k.Stroke(p, Palette.Outline, 0.013f);
            }
            using (Pen f = k.NewPen(Color.FromArgb(120, 190, 140, 130), 0.007f))
            {
                k.G.DrawLine(f, P(u - 0.018f, v + 0.020f), P(u - 0.014f, v + 0.040f));
                k.G.DrawLine(f, P(u + 0.004f, v + 0.022f), P(u + 0.007f, v + 0.042f));
            }
        }

        // =====================================================================
        // 领口：白领 + 蓝蝴蝶结（默认）
        // =====================================================================
        static void DrawNeckwear(Sk k, PetPose pose, Cfg c)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddBezier(P(-0.16f, 0.075f), P(-0.09f, 0.115f), P(0.09f, 0.115f), P(0.16f, 0.075f));
                p.AddBezier(P(0.16f, 0.075f), P(0.10f, 0.145f), P(-0.10f, 0.145f), P(-0.16f, 0.075f));
                p.CloseFigure();
                k.Fill(p, ApronWhite);
                k.Stroke(p, Color.FromArgb(255, 150, 164, 192), 0.010f);
            }

            DrawBow(k, 0f, 0.158f, 0.105f, Color.FromArgb(255, 62, 100, 196), true);

            if (c == null) return;
            if (pose.Accessory == Accessory.BowTie)
            {
                DrawBow(k, 0f, 0.165f, 0.145f, Color.FromArgb(255, 226, 92, 116), false);
            }
            else if (pose.Accessory == Accessory.Scarf)
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(P(-0.19f, 0.075f), P(-0.10f, 0.11f), P(0.10f, 0.11f), P(0.19f, 0.075f));
                    p.AddLine(P(0.19f, 0.075f), P(0.17f, 0.155f));
                    p.AddBezier(P(0.17f, 0.155f), P(0.08f, 0.135f), P(-0.08f, 0.135f), P(-0.17f, 0.155f));
                    p.CloseFigure();
                    k.FillGrad(p, k.Box(-0.2f, 0.065f, 0.4f, 0.10f), Color.FromArgb(255, 232, 92, 104), Color.FromArgb(255, 188, 58, 78), 90f);
                    k.Stroke(p, Palette.Outline, 0.012f);
                }
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(P(-0.12f, 0.145f), P(-0.19f, 0.22f), P(-0.20f, 0.30f), P(-0.185f, 0.365f));
                    p.AddLine(P(-0.185f, 0.365f), P(-0.115f, 0.345f));
                    p.AddLine(P(-0.115f, 0.345f), P(-0.10f, 0.23f));
                    p.CloseFigure();
                    k.Fill(p, Color.FromArgb(255, 214, 74, 92));
                    k.Stroke(p, Palette.Outline, 0.010f);
                }
            }
        }

        static void DrawBow(Sk k, float u, float v, float size, Color col, bool tail)
        {
            if (tail)
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(P(u - 0.02f, v + 0.01f), P(u - 0.06f, v + 0.09f), P(u - 0.075f, v + 0.14f), P(u - 0.095f, v + 0.175f));
                    p.AddLine(P(u - 0.095f, v + 0.175f), P(u - 0.01f, v + 0.155f));
                    p.AddLine(P(u - 0.01f, v + 0.155f), P(u + 0.02f, v + 0.03f));
                    p.CloseFigure();
                    k.Fill(p, Color.FromArgb(255, 46, 82, 172));
                    k.Stroke(p, Palette.Outline, 0.010f);
                }
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddBezier(P(u + 0.02f, v + 0.01f), P(u + 0.06f, v + 0.09f), P(u + 0.08f, v + 0.14f), P(u + 0.10f, v + 0.175f));
                    p.AddLine(P(u + 0.10f, v + 0.175f), P(u + 0.015f, v + 0.155f));
                    p.AddLine(P(u + 0.015f, v + 0.155f), P(u - 0.015f, v + 0.03f));
                    p.CloseFigure();
                    k.Fill(p, Color.FromArgb(255, 46, 82, 172));
                    k.Stroke(p, Palette.Outline, 0.010f);
                }
            }

            using (GraphicsPath left = new GraphicsPath())
            {
                left.StartFigure();
                left.AddBezier(P(u - 0.012f, v), P(u - 0.055f, v - size * 0.62f), P(u - size, v - size * 0.55f), P(u - size, v));
                left.AddBezier(P(u - size, v), P(u - size, v + size * 0.55f), P(u - 0.055f, v + size * 0.62f), P(u - 0.012f, v));
                left.CloseFigure();
                k.FillGrad(left, k.Box(u - size, v - size, size, size * 2f), Color.FromArgb(255, 92, 132, 232), Color.FromArgb(255, 48, 82, 172), 90f);
                k.Stroke(left, Palette.Outline, 0.011f);
            }
            using (GraphicsPath right = new GraphicsPath())
            {
                right.StartFigure();
                right.AddBezier(P(u + 0.012f, v), P(u + 0.055f, v - size * 0.62f), P(u + size, v - size * 0.55f), P(u + size, v));
                right.AddBezier(P(u + size, v), P(u + size, v + size * 0.55f), P(u + 0.055f, v + size * 0.62f), P(u + 0.012f, v));
                right.CloseFigure();
                k.FillGrad(right, k.Box(u, v - size, size, size * 2f), Color.FromArgb(255, 84, 124, 224), Color.FromArgb(255, 44, 76, 164), 90f);
                k.Stroke(right, Palette.Outline, 0.011f);
            }
            k.Blob(u, v, size * 0.30f, size * 0.28f, col);
            k.Ring(u, v, size * 0.30f, size * 0.28f, Palette.Outline, 0.010f);
        }

        // =====================================================================
        // 头部
        // =====================================================================
        static void DrawHead(Sk k, PetPose pose, Cfg c)
        {
            GraphicsState st = k.G.Save();
            try
            {
                float px = 0f, pv = -0.34f;
                k.G.TranslateTransform(px, pv - 1f);
                k.G.RotateTransform(c.HeadTilt);
                k.G.TranslateTransform(-px, -(pv - 1f));
                k.G.TranslateTransform(c.HeadDx, c.HeadBob + c.HeadDy);

                DrawBand(k, pose, c);
                DrawFace(k, pose, c);
                DrawFrontHair(k, pose, c);
                DrawFins(k, pose, c);
                DrawSpout(k, pose, c);
                if (IsHeadSlot(pose.Accessory)) DrawHeadAccessory(k, pose, c);
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        static PointF EllPt(float cu, float cv, float rx, float ry, float deg)
        {
            double a = deg * Math.PI / 180.0;
            return P(cu + (float)Math.Cos(a) * rx, cv - (float)Math.Sin(a) * ry);
        }

        // 连续扇贝形荷叶边：一条 band + 外缘半椭圆波浪
        static void DrawBand(Sk k, PetPose pose, Cfg c)
        {
            float cu = 0f, cv = -0.365f;
            float rx = 0.690f, ry = 0.645f;
            float thick = 0.068f;
            float a0 = 200f, a1 = -20f;
            int bumps = 8;
            float hf = 0.52f;

            using (GraphicsPath p = new GraphicsPath())
            {
                PointF prev = EllPt(cu, cv, rx - thick, ry - thick, a1);
                p.StartFigure();
                int steps = 52;
                for (int i = 1; i <= steps; i++)
                {
                    float a = a1 + (a0 - a1) * (float)i / steps;
                    PointF q = EllPt(cu, cv, rx - thick, ry - thick, a);
                    p.AddLine(prev, q);
                    prev = q;
                }
                PointF o0 = EllPt(cu, cv, rx, ry, a0);
                p.AddLine(prev, o0);

                for (int i = 0; i < bumps; i++)
                {
                    float aa = a0 + (a1 - a0) * (float)i / bumps;
                    float ab = a0 + (a1 - a0) * (float)(i + 1) / bumps;
                    PointF pa = EllPt(cu, cv, rx, ry, aa);
                    PointF pb = EllPt(cu, cv, rx, ry, ab);
                    AddScallopBump(p, pa, pb, cu, cv, rx, ry, hf);
                }
                p.CloseFigure();

                // 荷叶边的落影（让白边在浅色背景上也立得住）
                GraphicsState sh = k.G.Save();
                try
                {
                    k.G.TranslateTransform(0.010f, 0.014f);
                    k.Fill(p, Color.FromArgb(52, 66, 92, 150));
                }
                finally
                {
                    k.G.Restore(sh);
                }

                k.FillGrad(p, k.Box(-0.78f, -1.09f, 1.56f, 0.94f),
                           Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 206, 220, 242), 90f);
                k.Stroke(p, Color.FromArgb(255, 96, 114, 152), 0.016f);
            }

            // 褶裥
            using (Pen pleat = k.NewPen(Color.FromArgb(120, 142, 160, 196), 0.009f))
            {
                for (int i = 1; i < bumps; i++)
                {
                    float a = a0 + (a1 - a0) * (float)i / bumps;
                    PointF pa = EllPt(cu, cv, rx - thick * 0.92f, ry - thick * 0.92f, a);
                    PointF pb = EllPt(cu, cv, rx - 0.002f, ry - 0.002f, a);
                    k.G.DrawLine(pleat, pa, pb);
                }
            }
            // 内缘阴影
            using (Pen shade = k.NewPen(Color.FromArgb(95, 120, 140, 182), 0.012f))
            {
                PointF pa = EllPt(cu, cv, rx - thick, ry - thick, 208f);
                PointF pb = EllPt(cu, cv, rx - thick, ry - thick, 150f);
                k.G.DrawLine(shade, pa, pb);
            }

            // 右侧小蓝结
            DrawTinyBow(k, 0.552f, -0.225f, 0.052f, Color.FromArgb(255, 96, 142, 232), -18f);
        }

        static void AddScallopBump(GraphicsPath p, PointF a, PointF b, float cu, float cv, float rx, float ry, float hf)
        {
            float mx = (a.X + b.X) * 0.5f;
            float my = (a.Y + b.Y) * 0.5f;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) return;
            float tx = dx / len, ty = dy / len;

            float gx = (mx - cu) / (rx * rx);
            float gy = (my - (cv - 1f)) / (ry * ry);
            float gl = (float)Math.Sqrt(gx * gx + gy * gy);
            if (gl < 0.00001f) { gx = 0f; gy = -1f; gl = 1f; }
            float nx = gx / gl, ny = gy / gl;

            float r = len * 0.5f;
            float h = r * hf;
            float kq = 0.5523f;
            PointF m = new PointF(mx + nx * h, my + ny * h);
            PointF c1 = new PointF(a.X + nx * h * kq, a.Y + ny * h * kq);
            PointF c2 = new PointF(m.X - tx * r * kq, m.Y - ty * r * kq);
            p.AddBezier(a, c1, c2, m);
            PointF c3 = new PointF(m.X + tx * r * kq, m.Y + ty * r * kq);
            PointF c4 = new PointF(b.X + nx * h * kq, b.Y + ny * h * kq);
            p.AddBezier(m, c3, c4, b);
        }

        // =====================================================================
        // 脸（先画脸，最后才画头发）
        // =====================================================================
        static void DrawFace(Sk k, PetPose pose, Cfg c)
        {
            float fx = 0f, fv = -0.38f, fr = 0.47f;

            using (GraphicsPath face = new GraphicsPath())
            {
                face.AddEllipse(k.E(fx, fv, fr, fr * 0.985f));
                k.FillGrad(face, k.Box(fx - fr, fv - fr, fr * 2f, fr * 2f),
                           SkinLight, Color.FromArgb(255, 255, 226, 213), 90f);
                k.Stroke(face, Palette.Outline, 0.017f);

                // 刘海在额头投下的淡影
                GraphicsState fs = k.G.Save();
                try
                {
                    k.G.SetClip(face);
                    RectangleF sr = k.Box(-0.48f, -0.625f, 0.96f, 0.18f);
                    using (LinearGradientBrush sb = new LinearGradientBrush(sr,
                        Color.FromArgb(62, 118, 134, 186), Color.FromArgb(0, 118, 134, 186), 90f))
                    {
                        k.G.FillRectangle(sb, sr);
                    }
                }
                finally
                {
                    k.G.Restore(fs);
                }
            }

            if (c.Green)
            {
                using (SolidBrush gb = new SolidBrush(Color.FromArgb(58, 150, 205, 130)))
                    k.G.FillEllipse(gb, k.E(fx, fv + 0.02f, fr * 0.93f, fr * 0.92f));
            }

            DrawBlush(k, c);
            DrawEyes(k, pose, c);
            DrawBrows(k, c);
            DrawNose(k, c);
            DrawMouth(k, pose, c);
            if (c.Puffed) DrawPuff(k);
            if (c.Tear) DrawTear(k, pose);
            if (c.Sweat) DrawSweat(k, pose);
        }

        static void DrawBlush(Sk k, Cfg c)
        {
            float a = Clamp01(c.Blush);
            int alpha = (int)(34f + 120f * a);
            if (alpha < 20) alpha = 20;
            Color soft = Color.FromArgb(Math.Min(255, alpha / 2), Palette.Blush);
            Color strong = Color.FromArgb(alpha, Palette.Blush);

            k.Blob(-0.300f, -0.212f, 0.105f, 0.060f, soft);
            k.Blob(0.300f, -0.212f, 0.105f, 0.060f, soft);
            k.Blob(-0.298f, -0.214f, 0.078f, 0.042f, strong);
            k.Blob(0.298f, -0.214f, 0.078f, 0.042f, strong);

            using (Pen hatch = k.NewPen(Color.FromArgb((int)(alpha * 0.55f), 236, 110, 130), 0.010f))
            {
                for (int i = 0; i < 2; i++)
                {
                    float du = 0.022f * i;
                    k.G.DrawLine(hatch, P(-0.33f + du, -0.198f), P(-0.312f + du, -0.228f));
                    k.G.DrawLine(hatch, P(0.312f - du, -0.198f), P(0.33f - du, -0.228f));
                }
            }
        }

        // ---------------- 眼睛 ----------------
        static void DrawEyes(Sk k, PetPose pose, Cfg c)
        {
            float ex = 0.193f, ey = -0.335f, rx = 0.130f, ry = 0.152f;

            if (c.Eye == EyeStyle.Heart)
            {
                DrawHeartEye(k, -ex, ey, rx, ry, pose.Time);
                DrawHeartEye(k, ex, ey, rx, ry, pose.Time + 0.25f);
                return;
            }
            if (c.Eye == EyeStyle.Spiral)
            {
                DrawSpiralEye(k, -ex, ey, rx, ry, pose.Time);
                DrawSpiralEye(k, ex, ey, rx, ry, pose.Time + 0.4f);
                return;
            }
            if (c.Eye == EyeStyle.Happy)
            {
                DrawClosedEye(k, -ex, ey, rx, ry, true);
                DrawClosedEye(k, ex, ey, rx, ry, true);
                return;
            }
            if (c.Eye == EyeStyle.Closed || c.Blink >= 0.62f)
            {
                DrawClosedEye(k, -ex, ey, rx, ry, false);
                DrawClosedEye(k, ex, ey, rx, ry, false);
                return;
            }

            bool wide = (c.Eye == EyeStyle.Wide);
            bool sleepy = (c.Eye == EyeStyle.Sleepy);
            bool sad = (c.Eye == EyeStyle.Sad);
            DrawOpenEye(k, -ex, ey, rx, ry, wide, sleepy, sad, pose, c);
            DrawOpenEye(k, ex, ey, rx, ry, wide, sleepy, sad, pose, c);
        }

        static void DrawOpenEye(Sk k, float ex, float ey, float rx, float ry, bool wide, bool sleepy, bool sad, PetPose pose, Cfg c)
        {
            float ax = rx, ay = ry, acy = ey;
            float irisScale = 1f;

            if (wide) { ax = rx * 1.02f; ay = ry * 1.08f; irisScale = 0.74f; }
            if (sleepy) { acy = ey + ry * 0.33f; ay = ry * 0.62f; }
            if (sad) { acy = ey + ry * 0.10f; ay = ry * 0.86f; irisScale = 0.94f; }
            ay *= (1f - 0.84f * Clamp01(c.Blink));
            if (ay < 0.012f) { DrawClosedEye(k, ex, ey, rx, ry, false); return; }
            if (ax < 0.02f) return;

            float t = pose.Time;
            float lookX = S(t * 0.9f + (ex > 0 ? 1.4f : 0f)) * 0.007f;
            float lookY = 0.004f;
            if (c.Book) lookY = 0.016f;
            if (sad) { lookY = 0.014f; lookX *= 0.4f; }
            if (wide) { lookX = 0f; lookY = 0f; }

            RectangleF ap = k.E(ex, acy, ax, ay);
            using (GraphicsPath apPath = new GraphicsPath())
            {
                apPath.AddEllipse(ap);
                GraphicsState st = k.G.Save();
                try
                {
                    k.G.SetClip(apPath);

                    using (GraphicsPath white = new GraphicsPath())
                    {
                        white.AddEllipse(k.E(ex, ey, rx, ry));
                        k.FillGrad(white, k.E(ex, ey, rx, ry),
                                   Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 226, 238, 254), 90f);
                    }

                    float ix = ex + lookX;
                    float iy = ey + lookY;
                    float irx = 0.101f * irisScale;
                    float iry = 0.123f * irisScale;
                    using (GraphicsPath iris = new GraphicsPath())
                    {
                        iris.AddEllipse(k.E(ix, iy, irx, iry));
                        k.FillGrad(iris, k.E(ix, iy, irx, iry),
                                   Color.FromArgb(255, 96, 152, 244), Color.FromArgb(255, 22, 48, 100), 90f);
                        k.Stroke(iris, Color.FromArgb(255, 16, 34, 66), 0.012f);
                    }
                    using (SolidBrush gb = new SolidBrush(Color.FromArgb(105, 150, 206, 255)))
                    {
                        k.G.FillEllipse(gb, k.E(ix, iy + iry * 0.58f, irx * 0.72f, iry * 0.30f));
                    }
                    using (GraphicsPath pupil = new GraphicsPath())
                    {
                        pupil.AddEllipse(k.E(ix, iy + 0.010f, 0.052f * irisScale, 0.076f * irisScale));
                        k.Fill(pupil, Color.FromArgb(255, 12, 24, 48));
                    }
                    k.Blob(ix - 0.034f, iy - 0.048f, 0.037f, 0.032f, Color.FromArgb(255, 255, 255, 255));
                    k.Blob(ix - 0.047f, iy - 0.038f, 0.014f, 0.011f, Color.FromArgb(255, 255, 255, 255));
                    k.Blob(ix + 0.034f, iy + 0.046f, 0.016f, 0.014f, Color.FromArgb(210, 255, 255, 255));
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(60, 120, 168, 240)))
                    {
                        k.G.FillEllipse(sb, k.E(ex, ey - ry * 0.72f, rx * 0.92f, ry * 0.30f));
                    }
                }
                finally
                {
                    k.G.Restore(st);
                }

                k.Stroke(apPath, Palette.Outline, 0.016f);
            }

            float lidThick = sleepy ? 0.030f : 0.024f;
            using (Pen lash = k.NewPen(Color.FromArgb(255, 26, 44, 84), lidThick))
            {
                k.G.DrawArc(lash, ap, 188f, 164f);
            }
            float outer = ex < 0 ? -1f : 1f;
            using (Pen flick = k.NewPen(Color.FromArgb(255, 26, 44, 84), 0.021f))
            {
                PointF a = P(ex + outer * ax * 0.94f, acy - ay * 0.52f);
                PointF b = P(ex + outer * (ax + 0.040f), acy - ay * 0.98f);
                k.G.DrawLine(flick, a, b);
            }
            if (sleepy)
            {
                using (Pen crease = k.NewPen(Color.FromArgb(150, 220, 170, 160), 0.010f))
                {
                    k.G.DrawArc(crease, k.E(ex, acy - 0.012f, ax * 1.05f, ay * 1.15f), 190f, 160f);
                }
            }
        }

        static void DrawClosedEye(Sk k, float ex, float ey, float rx, float ry, bool happy)
        {
            float y = ey + (happy ? 0.012f : 0.006f);
            float arc = happy ? 0.070f : 0.040f;
            using (Pen p = k.NewPen(Color.FromArgb(255, 26, 44, 84), happy ? 0.032f : 0.028f))
            {
                k.G.DrawBezier(p,
                    P(ex - rx * 0.84f, y),
                    P(ex - rx * 0.34f, y - arc),
                    P(ex + rx * 0.34f, y - arc),
                    P(ex + rx * 0.84f, y - 0.004f));
            }
            float outer = ex < 0 ? -1f : 1f;
            using (Pen lash = k.NewPen(Color.FromArgb(255, 26, 44, 84), 0.019f))
            {
                k.G.DrawLine(lash, P(ex + outer * rx * 0.82f, y - 0.006f), P(ex + outer * (rx + 0.042f), y - 0.052f));
                k.G.DrawLine(lash, P(ex + outer * rx * 0.90f, y + 0.006f), P(ex + outer * (rx + 0.036f), y + 0.030f));
            }
        }

        static void DrawHeartEye(Sk k, float ex, float ey, float rx, float ry, float t)
        {
            float pulse = 1f + S(t * 4.5f) * 0.05f;
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(60, 255, 110, 150)))
            {
                k.G.FillEllipse(glow, k.E(ex, ey, rx * 1.15f, ry * 1.15f));
            }
            using (GraphicsPath h = HeartPath(ex, ey, rx * 1.72f * pulse))
            {
                k.FillGrad(h, k.Box(ex - rx, ey - ry, rx * 2f, ry * 2f),
                           Color.FromArgb(255, 255, 132, 168), Color.FromArgb(255, 234, 74, 118), 90f);
                k.Stroke(h, Color.FromArgb(255, 116, 40, 72), 0.016f);
            }
            k.Blob(ex - 0.040f, ey - 0.040f, 0.026f, 0.020f, Color.FromArgb(235, 255, 255, 255));
            k.Blob(ex + 0.046f, ey + 0.036f, 0.012f, 0.010f, Color.FromArgb(200, 255, 255, 255));
        }

        static void DrawSpiralEye(Sk k, float ex, float ey, float rx, float ry, float t)
        {
            using (GraphicsPath white = new GraphicsPath())
            {
                white.AddEllipse(k.E(ex, ey, rx, ry));
                k.FillGrad(white, k.E(ex, ey, rx, ry), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 222, 234, 250), 90f);
                k.Stroke(white, Palette.Outline, 0.015f);
            }
            using (Pen pen = k.NewPen(Color.FromArgb(255, 26, 44, 84), 0.015f))
            {
                float turns = 3.1f;
                int steps = 46;
                float cyc = t * 1.8f;
                PointF last = P(ex, ey);
                for (int i = 1; i <= steps; i++)
                {
                    float q = (float)i / steps;
                    float ang = cyc + q * turns * 6.28318f;
                    float rad = 0.008f + q * 0.086f;
                    PointF pt = P(ex + C(ang) * rad * 0.92f, ey + S(ang) * rad);
                    k.G.DrawLine(pen, last, pt);
                    last = pt;
                }
            }
            k.Blob(ex - 0.040f, ey - 0.050f, 0.020f, 0.016f, Color.FromArgb(220, 255, 255, 255));
        }

        static void DrawBrows(Sk k, Cfg c)
        {
            if (c.Brow == BrowStyle.None) return;
            float ex = 0.193f;
            float v = -0.548f;

            for (int side = -1; side <= 1; side += 2)
            {
                float cu = ex * side;
                float inner = cu - side * 0.070f;
                float outer = cu + side * 0.074f;
                float vi = v, vo = v, bulge = 0.018f;

                if (c.Brow == BrowStyle.Raised) { vi = v - 0.026f; vo = v - 0.020f; bulge = 0.030f; }
                else if (c.Brow == BrowStyle.Furrow) { vi = v + 0.020f; vo = v - 0.018f; bulge = 0.012f; }
                else if (c.Brow == BrowStyle.Sad) { vi = v - 0.020f; vo = v + 0.014f; bulge = 0.020f; }

                using (Pen p = k.NewPen(Color.FromArgb(255, 32, 50, 98), 0.021f))
                {
                    k.G.DrawBezier(p,
                        P(outer, vo),
                        P(cu + side * 0.030f, v - bulge),
                        P(cu - side * 0.030f, v - bulge),
                        P(inner, vi));
                }
            }
        }

        static void DrawNose(Sk k, Cfg c)
        {
            using (Pen p = k.NewPen(Color.FromArgb(85, 206, 142, 132), 0.010f))
            {
                k.G.DrawBezier(p, P(-0.018f, -0.192f), P(-0.006f, -0.180f), P(0.006f, -0.180f), P(0.018f, -0.192f));
            }
        }

        // ---------------- 嘴 ----------------
        static void DrawMouth(Sk k, PetPose pose, Cfg c)
        {
            float u = 0f, v = -0.112f;
            float open = Clamp01(pose.MouthOpen);

            if (c.IsRice)
            {
                float rp = Mod1(pose.Time * 0.30f);
                if (rp >= 0.25f && rp < 0.5f)
                {
                    float q = (rp - 0.25f) / 0.25f;
                    float sp = 0.5f - 0.5f * C(q * 6.28318f);
                    if (sp > 0.30f)
                    {
                        DrawOpenMouth(k, u, v, 0.072f, 0.032f + 0.040f * sp);
                        return;
                    }
                }
                else if (rp < 0.25f)
                {
                    float chomp = Math.Abs(S(pose.Time * 15f));
                    DrawOpenMouth(k, u, v + 0.004f, 0.068f, 0.018f + 0.042f * chomp);
                    return;
                }
            }

            switch (c.Mouth)
            {
                case MouthStyle.O:
                    DrawOpenMouth(k, u, v + 0.004f, 0.060f, 0.070f + open * 0.030f);
                    break;
                case MouthStyle.Open:
                    DrawOpenMouth(k, u, v, 0.082f, 0.050f + 0.055f * Math.Max(0.30f, open));
                    break;
                case MouthStyle.Chew:
                    {
                        float chomp = Math.Abs(S(pose.Time * 15f));
                        DrawOpenMouth(k, u, v + 0.006f, 0.064f, 0.026f + 0.042f * chomp + 0.02f * open);
                        break;
                    }
                case MouthStyle.Cat:
                    DrawCatMouth(k, u, v);
                    break;
                case MouthStyle.Wavy:
                    DrawWavyMouth(k, u, v, pose.Time);
                    break;
                case MouthStyle.Flat:
                    using (Pen p = k.NewPen(Color.FromArgb(255, 120, 70, 82), 0.016f))
                        k.G.DrawBezier(p, P(u - 0.048f, v), P(u - 0.016f, v + 0.010f), P(u + 0.016f, v + 0.010f), P(u + 0.048f, v));
                    break;
                case MouthStyle.Small:
                    using (Pen p = k.NewPen(Color.FromArgb(255, 150, 80, 96), 0.014f))
                        k.G.DrawBezier(p, P(u - 0.034f, v), P(u - 0.012f, v + 0.018f), P(u + 0.012f, v + 0.018f), P(u + 0.034f, v));
                    break;
                default:
                    if (open > 0.22f)
                        DrawOpenMouth(k, u, v, 0.070f, 0.030f + 0.055f * open);
                    else
                        using (Pen p = k.NewPen(Color.FromArgb(255, 128, 66, 82), 0.016f))
                            k.G.DrawBezier(p, P(u - 0.058f, v - 0.004f), P(u - 0.022f, v + 0.034f), P(u + 0.022f, v + 0.034f), P(u + 0.058f, v - 0.004f));
                    break;
            }
        }

        static void DrawOpenMouth(Sk k, float u, float v, float rx, float ry)
        {
            if (ry < 0.008f) ry = 0.008f;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(u, v, rx, ry));
                k.FillGrad(p, k.E(u, v, rx, ry), Color.FromArgb(255, 146, 62, 76), MouthDark, 90f);
                GraphicsState st = k.G.Save();
                try
                {
                    k.G.SetClip(p);
                    using (SolidBrush tongue = new SolidBrush(Tongue))
                    {
                        k.G.FillEllipse(tongue, k.E(u, v + ry * 0.62f, rx * 0.72f, ry * 0.52f));
                    }
                    using (SolidBrush teeth = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
                    {
                        k.G.FillEllipse(teeth, k.E(u, v - ry * 0.86f, rx * 0.88f, ry * 0.34f));
                    }
                }
                finally
                {
                    k.G.Restore(st);
                }
                k.Stroke(p, Color.FromArgb(255, 110, 46, 62), 0.014f);
            }
        }

        static void DrawCatMouth(Sk k, float u, float v)
        {
            using (Pen p = k.NewPen(Color.FromArgb(255, 130, 66, 82), 0.015f))
            {
                k.G.DrawBezier(p, P(u - 0.062f, v - 0.004f), P(u - 0.040f, v + 0.026f), P(u - 0.004f, v + 0.026f), P(u, v + 0.002f));
                k.G.DrawBezier(p, P(u, v + 0.002f), P(u + 0.004f, v + 0.026f), P(u + 0.040f, v + 0.026f), P(u + 0.062f, v - 0.004f));
            }
        }

        static void DrawWavyMouth(Sk k, float u, float v, float t)
        {
            using (Pen p = k.NewPen(Color.FromArgb(255, 136, 70, 86), 0.016f))
            using (GraphicsPath path = new GraphicsPath())
            {
                float amp = 0.016f;
                int n = 24;
                PointF prev = P(u - 0.058f, v + 0.006f);
                path.StartFigure();
                for (int i = 1; i <= n; i++)
                {
                    float q = (float)i / n;
                    float x = u - 0.058f + 0.116f * q;
                    float y = v + 0.006f + S(q * 6.28318f * 1.6f + t * 2f) * amp;
                    PointF pt = P(x, y);
                    path.AddLine(prev, pt);
                    prev = pt;
                }
                k.G.DrawPath(p, path);
            }
        }

        static void DrawPuff(Sk k)
        {
            using (Pen p = k.NewPen(Color.FromArgb(70, 220, 160, 148), 0.010f))
            {
                k.G.DrawArc(p, k.E(0.288f, -0.160f, 0.075f, 0.062f), 250f, 110f);
            }
            k.Blob(0.300f, -0.170f, 0.055f, 0.038f, Color.FromArgb(45, 255, 170, 160));
        }

        static void DrawTear(Sk k, PetPose pose)
        {
            float drop = Mod1(pose.Time * 0.35f);
            float u = -0.232f;
            float v = -0.215f + drop * 0.16f;
            using (GraphicsPath p = TeardropPath(u, v, 0.050f, 0.072f))
            {
                k.Fill(p, TearBlue);
                k.Stroke(p, Color.FromArgb(180, 92, 152, 210), 0.009f);
            }
            k.Blob(u - 0.010f, v + 0.006f, 0.008f, 0.011f, Color.FromArgb(230, 255, 255, 255));

            float d2 = Mod1(drop + 0.55f);
            if (d2 < 0.7f)
            {
                using (GraphicsPath p = TeardropPath(u + 0.028f, -0.198f + d2 * 0.12f, 0.030f, 0.045f))
                {
                    k.Fill(p, Color.FromArgb(200, 150, 206, 250));
                }
            }
        }

        static void DrawSweat(Sk k, PetPose pose)
        {
            float bob = S(pose.Time * 3.2f) * 0.008f;
            using (GraphicsPath p = TeardropPath(0.400f, -0.500f + bob, 0.062f, 0.090f))
            {
                k.FillGrad(p, k.Box(0.36f, -0.55f, 0.10f, 0.12f),
                           Color.FromArgb(235, 210, 240, 255), Color.FromArgb(225, 126, 188, 244), 90f);
                k.Stroke(p, Color.FromArgb(210, 92, 148, 206), 0.009f);
            }
            k.Blob(0.386f, -0.512f + bob, 0.011f, 0.014f, Color.FromArgb(200, 255, 255, 255));
        }

        // =====================================================================
        // 前发：刘海 + 两鬓长发（在脸和五官之后画）
        // =====================================================================
        static void DrawFrontHair(Sk k, PetPose pose, Cfg c)
        {
            float sway = S(pose.Time * 1.3f) * 1.0f;

            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(0f, -0.34f - 1f);
                k.G.RotateTransform(sway);
                k.G.TranslateTransform(0f, 0.34f + 1f);

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddBezier(P(-0.545f, -0.505f), P(-0.605f, -0.700f), P(-0.550f, -0.885f), P(-0.260f, -0.928f));
                    p.AddBezier(P(-0.260f, -0.928f), P(-0.020f, -0.965f), P(0.340f, -0.948f), P(0.525f, -0.800f));
                    p.AddBezier(P(0.525f, -0.815f), P(0.600f, -0.735f), P(0.605f, -0.565f), P(0.525f, -0.505f));
                    p.AddBezier(P(0.525f, -0.505f), P(0.512f, -0.548f), P(0.496f, -0.566f), P(0.475f, -0.578f));
                    p.AddBezier(P(0.475f, -0.578f), P(0.446f, -0.556f), P(0.424f, -0.540f), P(0.395f, -0.545f));
                    p.AddBezier(P(0.395f, -0.545f), P(0.364f, -0.566f), P(0.336f, -0.578f), P(0.305f, -0.585f));
                    p.AddBezier(P(0.305f, -0.585f), P(0.276f, -0.562f), P(0.248f, -0.544f), P(0.215f, -0.548f));
                    p.AddBezier(P(0.215f, -0.548f), P(0.186f, -0.566f), P(0.158f, -0.580f), P(0.120f, -0.588f));
                    p.AddBezier(P(0.120f, -0.588f), P(0.078f, -0.566f), P(0.040f, -0.548f), P(0.000f, -0.548f));
                    p.AddBezier(P(0.000f, -0.548f), P(-0.050f, -0.568f), P(-0.100f, -0.582f), P(-0.155f, -0.588f));
                    p.AddBezier(P(-0.155f, -0.588f), P(-0.204f, -0.566f), P(-0.250f, -0.545f), P(-0.295f, -0.545f));
                    p.AddBezier(P(-0.295f, -0.545f), P(-0.350f, -0.564f), P(-0.404f, -0.576f), P(-0.455f, -0.578f));
                    p.AddBezier(P(-0.455f, -0.578f), P(-0.492f, -0.552f), P(-0.522f, -0.520f), P(-0.545f, -0.505f));
                    p.CloseFigure();

                    k.FillGrad(p, k.Box(-0.62f, -0.99f, 1.24f, 0.55f),
                               Color.FromArgb(255, 52, 86, 168), Color.FromArgb(255, 84, 132, 216), 90f);
                    k.Stroke(p, Palette.Outline, 0.018f);
                }

                // 发束分组（浅色挑染）
                using (GraphicsPath strand = new GraphicsPath())
                {
                    strand.StartFigure();
                    strand.AddBezier(P(-0.425f, -0.800f), P(-0.360f, -0.700f), P(-0.300f, -0.640f), P(-0.230f, -0.605f));
                    strand.AddBezier(P(-0.230f, -0.605f), P(-0.300f, -0.650f), P(-0.350f, -0.700f), P(-0.390f, -0.780f));
                    strand.CloseFigure();
                    k.Fill(strand, Color.FromArgb(110, 132, 176, 244));

                    strand.Reset();
                    strand.StartFigure();
                    strand.AddBezier(P(0.030f, -0.930f), P(0.070f, -0.820f), P(0.100f, -0.730f), P(0.130f, -0.660f));
                    strand.AddBezier(P(0.130f, -0.660f), P(0.100f, -0.730f), P(0.070f, -0.820f), P(0.060f, -0.930f));
                    strand.CloseFigure();
                    k.Fill(strand, Color.FromArgb(100, 148, 188, 248));

                    strand.Reset();
                    strand.StartFigure();
                    strand.AddBezier(P(0.250f, -0.850f), P(0.320f, -0.760f), P(0.380f, -0.690f), P(0.430f, -0.640f));
                    strand.AddBezier(P(0.430f, -0.640f), P(0.380f, -0.710f), P(0.320f, -0.790f), P(0.290f, -0.860f));
                    strand.CloseFigure();
                    k.Fill(strand, Color.FromArgb(80, 142, 184, 246));
                }

                using (Pen shine = k.NewPen(Color.FromArgb(150, 132, 178, 242), 0.018f))
                {
                    k.G.DrawBezier(shine, P(-0.36f, -0.86f), P(-0.24f, -0.90f), P(-0.10f, -0.90f), P(0.02f, -0.87f));
                    k.G.DrawBezier(shine, P(0.08f, -0.90f), P(0.18f, -0.92f), P(0.30f, -0.89f), P(0.37f, -0.84f));
                }
                using (Pen shine2 = k.NewPen(Color.FromArgb(95, 170, 206, 255), 0.012f))
                {
                    k.G.DrawBezier(shine2, P(-0.47f, -0.74f), P(-0.44f, -0.66f), P(-0.42f, -0.60f), P(-0.41f, -0.55f));
                    k.G.DrawBezier(shine2, P(0.20f, -0.84f), P(0.23f, -0.78f), P(0.25f, -0.72f), P(0.27f, -0.66f));
                }
                using (Pen part = k.NewPen(Color.FromArgb(120, 24, 40, 90), 0.010f))
                {
                    k.G.DrawBezier(part, P(0.045f, -0.94f), P(0.06f, -0.82f), P(0.075f, -0.72f), P(0.09f, -0.62f));
                }

                DrawSideLock(k, 1f, pose.Time);
                DrawSideLock(k, -1f, pose.Time + 0.4f);
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        static void DrawSideLock(Sk k, float side, float t)
        {
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(side * 0.44f, -0.58f - 1f);
                k.G.RotateTransform(S(t * 1.6f) * 1.6f);
                k.G.TranslateTransform(-side * 0.44f, 0.58f + 1f);

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddBezier(P(side * 0.400f, -0.600f), P(side * 0.470f, -0.420f), P(side * 0.480f, -0.180f), P(side * 0.500f, 0.040f));
                    p.AddBezier(P(side * 0.500f, 0.040f), P(side * 0.512f, 0.220f), P(side * 0.550f, 0.360f), P(side * 0.620f, 0.460f));
                    p.AddBezier(P(side * 0.620f, 0.460f), P(side * 0.690f, 0.560f), P(side * 0.790f, 0.625f), P(side * 0.835f, 0.560f));
                    p.AddBezier(P(side * 0.835f, 0.560f), P(side * 0.760f, 0.535f), P(side * 0.700f, 0.480f), P(side * 0.665f, 0.390f));
                    p.AddBezier(P(side * 0.665f, 0.390f), P(side * 0.620f, 0.220f), P(side * 0.640f, -0.060f), P(side * 0.615f, -0.300f));
                    p.AddBezier(P(side * 0.615f, -0.300f), P(side * 0.600f, -0.460f), P(side * 0.550f, -0.580f), P(side * 0.500f, -0.645f));
                    p.CloseFigure();

                    k.FillGrad(p, k.Box(-0.85f, -0.66f, 1.70f, 1.30f),
                               Color.FromArgb(255, 70, 110, 200), Color.FromArgb(255, 126, 176, 242), 90f);
                    k.Stroke(p, Palette.Outline, 0.017f);
                }

                using (Pen shine = k.NewPen(Color.FromArgb(120, 178, 214, 255), 0.012f))
                {
                    k.G.DrawBezier(shine, P(side * 0.520f, -0.320f), P(side * 0.565f, -0.060f), P(side * 0.585f, 0.200f), P(side * 0.640f, 0.400f));
                    k.G.DrawBezier(shine, P(side * 0.470f, -0.400f), P(side * 0.500f, -0.200f), P(side * 0.525f, 0.040f), P(side * 0.555f, 0.240f));
                }
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        // =====================================================================
        // 脸侧鲸鱼鳍发饰
        // =====================================================================
        static void DrawFins(Sk k, PetPose pose, Cfg c)
        {
            float flap = S(pose.Time * 2.0f) * 2.5f;
            DrawOneFin(k, 1f, flap);
            DrawOneFin(k, -1f, -flap);
        }

        static void DrawOneFin(Sk k, float side, float rot)
        {
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(side * 0.46f, -0.19f - 1f);
                k.G.RotateTransform(rot + side * 8f);
                k.G.ScaleTransform(side, 1f);

                using (GraphicsPath p = new GraphicsPath())
                {
                    p.StartFigure();
                    p.AddBezier(Lp(0.010f, -0.120f), Lp(0.120f, -0.165f), Lp(0.230f, -0.150f), Lp(0.310f, -0.070f));
                    p.AddBezier(Lp(0.310f, -0.070f), Lp(0.350f, -0.032f), Lp(0.330f, 0.010f), Lp(0.285f, 0.006f));
                    p.AddBezier(Lp(0.285f, 0.006f), Lp(0.245f, 0.000f), Lp(0.222f, -0.038f), Lp(0.190f, -0.052f));
                    p.AddBezier(Lp(0.190f, -0.052f), Lp(0.192f, 0.020f), Lp(0.196f, 0.070f), Lp(0.170f, 0.095f));
                    p.AddBezier(Lp(0.170f, 0.095f), Lp(0.126f, 0.052f), Lp(0.108f, -0.006f), Lp(0.080f, -0.028f));
                    p.AddBezier(Lp(0.080f, -0.028f), Lp(0.052f, -0.060f), Lp(0.020f, -0.086f), Lp(0.010f, -0.120f));
                    p.CloseFigure();

                    k.FillGrad(p, new RectangleF(0f, -0.18f, 0.36f, 0.28f),
                               Color.FromArgb(255, 78, 120, 210), Color.FromArgb(255, 30, 50, 106), 90f);
                    k.Stroke(p, Palette.Outline, 0.016f);
                }

                using (Pen rib = k.NewPen(Color.FromArgb(120, 150, 192, 255), 0.009f))
                {
                    k.G.DrawBezier(rib, Lp(0.055f, -0.100f), Lp(0.140f, -0.120f), Lp(0.220f, -0.100f), Lp(0.278f, -0.052f));
                    k.G.DrawBezier(rib, Lp(0.070f, -0.058f), Lp(0.130f, -0.070f), Lp(0.175f, -0.056f), Lp(0.205f, -0.020f));
                    k.G.DrawBezier(rib, Lp(0.090f, -0.024f), Lp(0.130f, -0.030f), Lp(0.150f, -0.014f), Lp(0.162f, 0.018f));
                }
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(80, 200, 224, 255)))
                    k.G.FillEllipse(hb, new RectangleF(0.140f, -0.084f, 0.040f, 0.028f));
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        // =====================================================================
        // 头顶小鲸鱼喷水
        // =====================================================================
        static void DrawSpout(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;

            using (Pen splash = k.NewPen(Color.FromArgb(170, 150, 210, 255), 0.012f))
            {
                k.G.DrawBezier(splash, P(-0.075f, -1.028f), P(-0.055f, -1.058f), P(-0.035f, -1.060f), P(-0.022f, -1.048f));
                k.G.DrawBezier(splash, P(0.075f, -1.028f), P(0.055f, -1.058f), P(0.035f, -1.060f), P(0.022f, -1.048f));
            }

            for (int i = 0; i < 3; i++)
            {
                float ph = t * 2.1f + i * 1.05f;
                float bob = S(ph) * 0.012f;
                float u = -0.088f + i * 0.088f;
                float v = -1.043f + bob - (i == 1 ? 0.006f : 0f);
                float w = (i == 1 ? 0.040f : 0.032f);
                float h = (i == 1 ? 0.056f : 0.045f);

                using (GraphicsPath p = TeardropPath(u, v, w, h))
                {
                    k.FillGrad(p, k.Box(u - w, v - h, w * 2f, h * 2f),
                               Color.FromArgb(240, 226, 246, 255), Color.FromArgb(235, 128, 190, 250), 90f);
                    k.Stroke(p, Color.FromArgb(230, 74, 126, 196), 0.009f);
                }
                k.Blob(u - w * 0.20f, v + h * 0.10f, w * 0.18f, h * 0.16f, Color.FromArgb(245, 255, 255, 255));
            }
        }

        // =====================================================================
        // 招牌动作：铁盆扒饭
        // =====================================================================
        static PointF Lp(float x, float y) { return new PointF(x, y); }
        static RectangleF Lr(float x, float y, float rx, float ry)
        {
            return new RectangleF(x - rx, y - ry, rx * 2f, ry * 2f);
        }

        static void RiceBowlState(float time, out float bx, out float by, out float rot, out float up)
        {
            float p = Mod1(time * 0.30f);
            up = 0f;
            if (p >= 0.5f && p < 0.75f)
            {
                float q = (p - 0.5f) / 0.25f;
                if (q < 0.35f) up = Smooth(q / 0.35f);
                else if (q < 0.82f) up = 1f;
                else up = 1f - Smooth((q - 0.82f) / 0.18f);
            }
            bx = 0f;
            by = 0.37f - 0.37f * up;
            rot = -22f * up;
        }

        static void RiceSpoonState(float time, out float tu, out float tv, out float hu, out float hv)
        {
            float p = Mod1(time * 0.30f);
            float s = 0f;
            if (p >= 0.25f && p < 0.5f)
            {
                float q = (p - 0.25f) / 0.25f;
                s = 0.5f - 0.5f * C(q * 6.28318f);
            }
            tu = Lerp(0.150f, 0.055f, s);
            tv = Lerp(0.075f, -0.135f, s);
            hu = tu + 0.300f;
            hv = tv + 0.160f;
        }

        static void BowlHand(float lx, float ly, float bx, float by, float rot, out float u, out float v)
        {
            double a = rot * Math.PI / 180.0;
            float ca = (float)Math.Cos(a);
            float sa = (float)Math.Sin(a);
            u = bx + lx * ca - ly * sa;
            v = by + lx * sa + ly * ca;
        }

        static void DrawRiceArms(Sk k, PetPose pose, Cfg c)
        {
            float bx, by, rot, up;
            RiceBowlState(pose.Time, out bx, out by, out rot, out up);

            float hlu, hlv, hru, hrv;
            BowlHand(-0.292f, 0.055f, bx, by, rot, out hlu, out hlv);
            BowlHand(0.292f, 0.055f, bx, by, rot, out hru, out hrv);

            float p = Mod1(pose.Time * 0.30f);
            if (p >= 0.25f && p < 0.5f)
            {
                float tu, tv, hu, hv;
                RiceSpoonState(pose.Time, out tu, out tv, out hu, out hv);
                hru = hu;
                hrv = hv;
            }

            DrawArm(k, -1f, -0.285f, 0.12f, hlu, hlv);
            DrawArm(k, 1f, 0.285f, 0.12f, hru, hrv);
            DrawHand(k, hlu, hlv);
            DrawHand(k, hru, hrv);
        }

        static void RiceSurface(float rice, out float v, out float rx, out float ry)
        {
            if (rice >= 0.60f) { v = -0.145f; rx = 0.214f; ry = 0.054f; }
            else if (rice >= 0.25f)
            {
                float t = (rice - 0.25f) / 0.35f;
                v = Lerp(-0.108f, -0.145f, t);
                rx = Lerp(0.142f, 0.214f, t);
                ry = Lerp(0.037f, 0.054f, t);
            }
            else if (rice >= 0.05f)
            {
                float t = (rice - 0.05f) / 0.20f;
                v = Lerp(-0.090f, -0.108f, t);
                rx = Lerp(0.082f, 0.142f, t);
                ry = Lerp(0.023f, 0.037f, t);
            }
            else { v = -0.090f; rx = 0.068f; ry = 0.019f; }
        }

        static void RiceGrain(Sk k, float u, float v, float ang, float scale)
        {
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(k.E(u, v, 0.025f * scale, 0.011f * scale));
                using (Matrix m = new Matrix())
                {
                    m.RotateAt(ang, new PointF(u, v - 1f));
                    gp.Transform(m);
                }
                k.Fill(gp, RiceWhite);
                k.Stroke(gp, Color.FromArgb(200, 158, 146, 120), 0.0055f);
            }
            k.Blob(u - 0.007f * scale, v - 0.003f * scale, 0.006f * scale, 0.003f * scale, Color.FromArgb(210, 255, 255, 255));
        }

        static void DrawRiceBowl(Sk k, PetPose pose, Cfg c)
        {
            float bx, by, rot, up;
            RiceBowlState(pose.Time, out bx, out by, out rot, out up);
            float rice = Clamp01(pose.Rice);
            float p = Mod1(pose.Time * 0.30f);
            float t = pose.Time;

            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(bx, by - 1f);
                k.G.RotateTransform(rot);

                // 盆身
                using (GraphicsPath body = new GraphicsPath())
                {
                    body.StartFigure();
                    body.AddBezier(Lp(-0.268f, -0.145f), Lp(-0.268f, 0.005f), Lp(-0.232f, 0.122f), Lp(-0.158f, 0.165f));
                    body.AddBezier(Lp(-0.158f, 0.165f), Lp(-0.070f, 0.198f), Lp(0.070f, 0.198f), Lp(0.158f, 0.165f));
                    body.AddBezier(Lp(0.158f, 0.165f), Lp(0.232f, 0.122f), Lp(0.268f, 0.005f), Lp(0.268f, -0.145f));
                    body.CloseFigure();
                    k.FillGrad(body, new RectangleF(-0.27f, -0.15f, 0.54f, 0.36f),
                               Color.FromArgb(255, 244, 248, 254), Color.FromArgb(255, 186, 198, 220), 90f);
                    k.Stroke(body, Palette.Outline, 0.017f);
                }
                // 盆身反光
                using (SolidBrush hl = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                {
                    k.G.FillEllipse(hl, Lr(-0.132f, 0.020f, 0.042f, 0.092f));
                }
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(110, 118, 134, 164)))
                {
                    k.G.FillEllipse(sh, Lr(0.148f, 0.055f, 0.058f, 0.082f));
                }
                using (Pen band = k.NewPen(Color.FromArgb(190, 96, 126, 178), 0.013f))
                {
                    k.G.DrawArc(band, Lr(0f, 0.045f, 0.232f, 0.150f), 20f, 140f);
                }

                // 深蓝卷边盆口（外圈）
                using (GraphicsPath rim = new GraphicsPath())
                {
                    rim.AddEllipse(Lr(0f, -0.145f, 0.272f, 0.080f));
                    k.FillGrad(rim, new RectangleF(-0.28f, -0.23f, 0.56f, 0.17f),
                               Color.FromArgb(255, 82, 120, 186), Color.FromArgb(255, 30, 56, 112), 90f);
                    k.Stroke(rim, Palette.Outline, 0.015f);
                }
                // 盆内深色
                using (GraphicsPath cav = new GraphicsPath())
                {
                    cav.AddEllipse(Lr(0f, -0.145f, 0.238f, 0.062f));
                    k.FillGrad(cav, new RectangleF(-0.24f, -0.20f, 0.48f, 0.13f),
                               Color.FromArgb(255, 88, 92, 108), Color.FromArgb(255, 38, 42, 54), 90f);
                    k.Stroke(cav, Color.FromArgb(255, 26, 34, 48), 0.010f);
                }

                // 饭
                if (rice > 0.004f)
                {
                    float sv, srx, sry;
                    RiceSurface(rice, out sv, out srx, out sry);

                    GraphicsState rs = k.G.Save();
                    try
                    {
                        using (GraphicsPath clip = new GraphicsPath())
                        {
                            clip.AddEllipse(Lr(0f, -0.145f, 0.236f, 0.060f));
                            k.G.SetClip(clip);
                            using (GraphicsPath surf = new GraphicsPath())
                            {
                                surf.AddEllipse(Lr(0f, sv, srx, sry));
                                k.FillGrad(surf, new RectangleF(-0.24f, sv - sry, 0.48f, sry * 2f),
                                           Color.FromArgb(255, 255, 254, 246), Color.FromArgb(255, 225, 215, 194), 90f);
                                k.Stroke(surf, Color.FromArgb(210, 166, 154, 128), 0.0065f);
                            }
                            // 低饭量：盆内壁的阴影弧
                            if (rice < 0.55f)
                            {
                                using (Pen inner = k.NewPen(Color.FromArgb(110, 20, 24, 34), 0.014f))
                                {
                                    k.G.DrawArc(inner, Lr(0f, -0.145f, 0.222f, 0.052f), 200f, 140f);
                                }
                            }
                        }
                    }
                    finally
                    {
                        k.G.Restore(rs);
                    }

                    // 高饭量：冒出盆口的小山尖
                    float peak = -0.145f;
                    if (rice >= 0.55f)
                    {
                        peak = -0.145f - (rice - 0.55f) / 0.45f * 0.150f;
                        using (GraphicsPath heap = new GraphicsPath())
                        {
                            heap.StartFigure();
                            heap.AddBezier(Lp(-0.256f, -0.130f), Lp(-0.238f, peak + 0.050f), Lp(-0.125f, peak), Lp(0f, peak));
                            heap.AddBezier(Lp(0f, peak), Lp(0.125f, peak), Lp(0.238f, peak + 0.050f), Lp(0.256f, -0.130f));
                            heap.AddBezier(Lp(0.256f, -0.130f), Lp(0.170f, -0.068f), Lp(-0.170f, -0.068f), Lp(-0.256f, -0.130f));
                            heap.CloseFigure();
                            k.FillGrad(heap, new RectangleF(-0.26f, peak, 0.52f, -0.13f - peak),
                                       Color.FromArgb(255, 255, 254, 247), Color.FromArgb(255, 226, 216, 196), 90f);
                        }
                        using (GraphicsPath top = new GraphicsPath())
                        {
                            top.StartFigure();
                            top.AddBezier(Lp(-0.256f, -0.130f), Lp(-0.238f, peak + 0.050f), Lp(-0.125f, peak), Lp(0f, peak));
                            top.AddBezier(Lp(0f, peak), Lp(0.125f, peak), Lp(0.238f, peak + 0.050f), Lp(0.256f, -0.130f));
                            k.Stroke(top, Color.FromArgb(210, 166, 154, 128), 0.0075f);
                        }
                    }

                    // 米粒
                    int n = 8 + (int)Math.Round(6f * rice);
                    if (rice < 0.55f) n = 3 + (int)Math.Round(5f * rice / 0.55f);
                    if (n < 3) n = 3;
                    for (int i = 0; i < n; i++)
                    {
                        float h1 = Hash01(i * 17 + 3);
                        float h2 = Hash01(i * 29 + 11);
                        float gu, gv;
                        if (rice >= 0.55f)
                        {
                            gu = (h1 * 2f - 1f) * 0.158f;
                            gv = peak + 0.048f + h2 * 0.085f + Math.Abs(gu) * 0.15f;
                        }
                        else
                        {
                            gu = (h1 * 2f - 1f) * srx * 0.68f;
                            gv = sv + (h2 * 2f - 1f) * sry * 0.38f;
                        }
                        RiceGrain(k, gu, gv, (h1 - 0.5f) * 120f, 0.85f + h2 * 0.35f);
                    }
                }

                // 前面的盆口（盖住饭的前缘）
                using (GraphicsPath front = new GraphicsPath())
                {
                    front.AddArc(Lr(0f, -0.145f, 0.272f, 0.080f), 0f, 180f);
                    front.AddArc(Lr(0f, -0.145f, 0.238f, 0.062f), 180f, -180f);
                    front.CloseFigure();
                    k.FillGrad(front, new RectangleF(-0.28f, -0.16f, 0.56f, 0.12f),
                               Color.FromArgb(255, 74, 108, 172), BowlBlueDeep, 90f);
                    k.Stroke(front, Palette.Outline, 0.012f);
                }
                // 盆口高光
                using (Pen rimHl = k.NewPen(Color.FromArgb(170, 176, 208, 246), 0.010f))
                {
                    k.G.DrawArc(rimHl, Lr(0f, -0.150f, 0.258f, 0.076f), 15f, 150f);
                }

                // 倒入嘴里的成串米粒（相位③）
                if (up > 0.45f)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float q = Mod1(t * 1.4f + i * 0.19f);
                        float gu = -0.120f - i * 0.011f + S(t * 4f + i) * 0.016f;
                        float gv = -0.235f + q * 0.070f;
                        RiceGrain(k, gu, gv, 50f + i * 20f, 0.8f);
                    }
                }
            }
            finally
            {
                k.G.Restore(st);
            }

            // 勺子（相位②）
            if (p >= 0.25f && p < 0.5f) DrawSpoon(k, pose);

            // 嘴角 / 脸上粘的米粒
            if (rice > 0.04f && up < 0.45f)
            {
                RiceGrain(k, 0.152f, -0.092f, -25f, 0.9f);
                RiceGrain(k, 0.205f, -0.130f, 40f, 0.8f);
                if (rice > 0.3f) RiceGrain(k, 0.176f, -0.052f, 10f, 0.75f);
                if (rice > 0.7f) RiceGrain(k, 0.108f, -0.062f, -60f, 0.7f);
            }

            // 空中飞散的米粒
            if (rice > 0.03f)
            {
                int fn = 3 + (int)Math.Round(3f * rice);
                for (int i = 0; i < fn; i++)
                {
                    float tt = Mod1(t * 0.55f + i * 0.23f);
                    float fu = -0.20f + i * 0.145f + S(t * 2.3f + i * 1.7f) * 0.045f;
                    float fv = by - 0.03f - tt * 0.20f;
                    if (i % 2 == 0) fv = by + 0.06f + tt * 0.14f;
                    if (fv > 0.55f) fv = 0.55f;
                    RiceGrain(k, fu, fv, t * 120f + i * 60f, 0.75f);
                }
            }
        }

        static void DrawSpoon(Sk k, PetPose pose)
        {
            float tu, tv, hu, hv;
            RiceSpoonState(pose.Time, out tu, out tv, out hu, out hv);

            using (Pen ho = k.NewPen(Palette.Outline, 0.030f))
                k.G.DrawLine(ho, P(hu, hv), P(tu, tv));
            using (Pen hi = k.NewPen(Color.FromArgb(255, 220, 228, 240), 0.017f))
                k.G.DrawLine(hi, P(hu, hv), P(tu, tv));

            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(tu, tv, 0.052f, 0.038f));
                k.FillGrad(p, k.Box(tu - 0.05f, tv - 0.04f, 0.10f, 0.08f),
                           Color.FromArgb(255, 250, 252, 255), Color.FromArgb(255, 200, 210, 226), 90f);
                k.Stroke(p, Palette.Outline, 0.012f);
            }
            k.Blob(tu - 0.020f, tv - 0.020f, 0.012f, 0.008f, Color.FromArgb(220, 255, 255, 255));
            RiceGrain(k, tu + 0.008f, tv - 0.002f, -18f, 0.9f);
            RiceGrain(k, tu - 0.012f, tv + 0.008f, 25f, 0.75f);
        }
        // =====================================================================
        // 情绪特效
        // =====================================================================
        static void DrawOuterFx(Sk k, PetPose pose, Cfg c)
        {
            float t = pose.Time;
            if (c.ZzzSmall || c.ZzzBig) DrawZzz(k, t, c.ZzzBig);
            if (c.Snot) DrawSnot(k, t);
            if (c.Crumbs) DrawCrumbs(k);
            if (c.Hearts) DrawHearts(k, t);
            if (c.Stars) DrawDizzyStars(k, t);
            if (c.Steam) DrawSteam(k, t);
            if (c.Sparkle) DrawSparkles(k, t);
            if (c.Note) DrawMusicNote(k, t);
            if (c.Motion) DrawMotion(k, pose);
            if (pose.Mood == PetMood.Surprised) DrawExclaim(k, t);
        }

        static void DrawSparkleAt(Sk k, float u, float v, float size, Color col, float t)
        {
            float pulse = 0.75f + 0.25f * (0.5f + 0.5f * S(t * 5f));
            using (GraphicsPath p = StarPath(u, v, size * pulse, size * 0.24f * pulse, 4, t * 40f))
            {
                k.Fill(p, col);
            }
            k.Blob(u, v, size * 0.10f, size * 0.10f, Color.FromArgb(240, 255, 255, 255));
        }

        static void DrawZGlyph(Sk k, float u, float v, float w, float h, Color col, float thick)
        {
            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(u, v - 1f);
                if (k.Flipped) k.G.ScaleTransform(-1f, 1f);
                using (GraphicsPath z = ZPath(0f, 1f, w, h))
                using (Pen p = k.NewPen(col, thick))
                {
                    k.G.DrawPath(p, z);
                }
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        static void DrawZzz(Sk k, float t, bool big)
        {
            int n = big ? 3 : 2;
            for (int i = 0; i < n; i++)
            {
                float q = Mod1(t * 0.32f + i * 0.34f);
                float u = 0.50f + q * 0.10f + i * 0.035f;
                float v = -0.60f - q * 0.17f - i * 0.055f;
                float sz = (big ? 0.160f : 0.115f) * (1.05f - q * 0.30f);
                int alpha = (int)(240f * (1f - q * 0.60f));
                if (alpha < 40) alpha = 40;
                DrawZGlyph(k, u, v, sz, sz * 1.05f, Color.FromArgb(alpha, 58, 100, 190), big ? 0.024f : 0.019f);
            }
        }

        static void DrawSnot(Sk k, float t)
        {
            float r = 0.036f + 0.022f * (0.5f + 0.5f * S(t * 1.9f));
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(0.090f, -0.170f, r, r * 1.06f));
                k.FillGrad(p, k.Box(0.09f - r, -0.170f - r, r * 2f, r * 2f),
                           Color.FromArgb(225, 238, 250, 255), Color.FromArgb(210, 152, 204, 250), 90f);
                k.Stroke(p, Color.FromArgb(210, 96, 156, 214), 0.010f);
            }
            k.Blob(0.076f, -0.184f, 0.011f, 0.014f, Color.FromArgb(240, 255, 255, 255));
        }

        static void DrawCrumbs(Sk k)
        {
            k.Blob(0.128f, -0.075f, 0.014f, 0.011f, Color.FromArgb(255, 244, 218, 160));
            k.Blob(0.170f, -0.098f, 0.010f, 0.008f, Color.FromArgb(255, 240, 208, 144));
            k.Blob(0.108f, -0.118f, 0.008f, 0.006f, Color.FromArgb(255, 246, 224, 170));
        }

        static void DrawHearts(Sk k, float t)
        {
            for (int i = 0; i < 3; i++)
            {
                float q = Mod1(t * 0.30f + i * 0.33f);
                float side = (i == 1) ? 1f : -1f;
                float u = side * (0.54f + i * 0.03f) + S(t * 1.4f + i) * 0.045f;
                float v = -0.46f - i * 0.085f - q * 0.10f;
                float sz = (0.115f + i * 0.018f) * (0.92f + 0.08f * S(t * 4f + i));
                int alpha = (int)(230f * (1f - q * 0.55f));
                if (alpha < 40) alpha = 40;
                using (GraphicsPath h = HeartPath(u, v, sz))
                {
                    k.FillGrad(h, k.Box(u - sz, v - sz, sz * 2f, sz * 2f),
                               Color.FromArgb(alpha, 255, 150, 180), Color.FromArgb(alpha, 236, 84, 128), 90f);
                    k.Stroke(h, Color.FromArgb(Math.Min(255, alpha), 150, 46, 84), 0.010f);
                }
                k.Blob(u - 0.020f, v - 0.022f, 0.012f, 0.010f, Color.FromArgb(220, 255, 255, 255));
            }
        }

        static void DrawDizzyStars(Sk k, float t)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = t * 2.3f + i * 2.0944f;
                float u = C(a) * 0.80f;
                float v = -0.66f + S(a) * 0.30f;
                using (GraphicsPath p = StarPath(u, v, 0.080f, 0.034f, 5, t * 90f + i * 40f))
                {
                    k.FillGrad(p, k.Box(u - 0.08f, v - 0.08f, 0.16f, 0.16f),
                               Color.FromArgb(255, 255, 232, 150), Color.FromArgb(255, 246, 190, 84), 90f);
                    k.Stroke(p, Color.FromArgb(210, 150, 96, 40), 0.010f);
                }
                k.Blob(u - 0.012f, v - 0.014f, 0.008f, 0.008f, Color.FromArgb(230, 255, 255, 255));
            }
        }

        static void DrawSteam(Sk k, float t)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = (i == 0) ? -1f : 1f;
                float x = side * (0.66f + i * 0.035f);
                float ph = t * 1.5f + i * 1.7f;
                using (GraphicsPath p = new GraphicsPath())
                {
                    PointF prev = P(x, -0.50f);
                    p.StartFigure();
                    for (int j = 1; j <= 8; j++)
                    {
                        float q = (float)j / 8f;
                        float px = x + side * (0.035f + S(ph + q * 5.2f) * 0.030f) * q;
                        float py = -0.50f - q * 0.40f;
                        PointF pt = P(px, py);
                        p.AddLine(prev, pt);
                        prev = pt;
                    }
                    using (Pen outer = k.NewPen(Color.FromArgb(215, 150, 186, 234), 0.028f))
                        k.G.DrawPath(outer, p);
                    using (Pen inner = k.NewPen(Color.FromArgb(235, 255, 255, 255), 0.012f))
                        k.G.DrawPath(inner, p);
                }
            }
        }

        static void DrawSparkles(Sk k, float t)
        {
            DrawSparkleAt(k, -0.58f, -0.56f, 0.085f, Color.FromArgb(235, 255, 240, 170), t);
            DrawSparkleAt(k, 0.60f, -0.44f, 0.070f, Color.FromArgb(235, 255, 255, 255), t + 0.5f);
            DrawSparkleAt(k, 0.47f, -0.83f, 0.060f, Color.FromArgb(225, 220, 238, 255), t + 1.1f);
            DrawSparkleAt(k, -0.44f, -0.82f, 0.050f, Color.FromArgb(210, 255, 236, 160), t + 1.7f);
        }

        static void DrawMusicNote(Sk k, float t)
        {
            float q = Mod1(t * 0.32f);
            float u = 0.64f - q * 0.03f;
            float v = -0.44f - q * 0.12f + S(t * 1.6f) * 0.012f;
            int alpha = (int)(230f * (1f - q * 0.5f));
            Color col = Color.FromArgb(alpha, 70, 112, 198);

            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(u, v, 0.030f, 0.023f));
                k.Fill(p, col);
                k.Stroke(p, Color.FromArgb(alpha, 30, 52, 110), 0.008f);
            }
            using (Pen stem = k.NewPen(col, 0.014f))
            {
                k.G.DrawLine(stem, P(u + 0.026f, v - 0.008f), P(u + 0.026f, v - 0.100f));
            }
            using (Pen flag = k.NewPen(col, 0.014f))
            {
                k.G.DrawBezier(flag, P(u + 0.026f, v - 0.098f), P(u + 0.070f, v - 0.090f), P(u + 0.070f, v - 0.060f), P(u + 0.050f, v - 0.050f));
            }
        }

        static void DrawMotion(Sk k, PetPose pose)
        {
            using (Pen p = k.NewPen(Color.FromArgb(130, 96, 128, 190), 0.020f))
            {
                for (int i = 0; i < 3; i++)
                {
                    float v = 0.16f + i * 0.12f;
                    float u = -0.82f - i * 0.015f;
                    k.G.DrawBezier(p, P(u, v), P(u - 0.03f, v + 0.02f), P(u - 0.05f, v + 0.04f), P(u - 0.10f, v + 0.05f));
                }
            }
        }

        static void DrawExclaim(Sk k, float t)
        {
            float bob = S(t * 6f) * 0.015f;
            using (Pen bar = k.NewPen(Color.FromArgb(255, 240, 102, 84), 0.036f))
            {
                k.G.DrawLine(bar, P(0.545f, -0.770f + bob), P(0.568f, -0.640f + bob));
            }
            k.Blob(0.573f, -0.598f + bob, 0.028f, 0.028f, Color.FromArgb(255, 240, 102, 84));
            k.Blob(0.567f, -0.606f + bob, 0.010f, 0.010f, Color.FromArgb(220, 255, 240, 220));
        }

        // =====================================================================
        // 头部装扮
        // =====================================================================
        static void DrawHeadAccessory(Sk k, PetPose pose, Cfg c)
        {
            switch (pose.Accessory)
            {
                case Accessory.StrawHat: DrawStrawHat(k); break;
                case Accessory.Sunglasses: DrawSunglasses(k); break;
                case Accessory.Crown: DrawCrown(k); break;
                case Accessory.SantaHat: DrawSantaHat(k); break;
                case Accessory.DivingMask: DrawDivingMask(k); break;
                case Accessory.BambooCopter: DrawBambooCopter(k, pose.Time); break;
                case Accessory.Headphones: DrawHeadphones(k); break;
                case Accessory.RubberDuck: DrawRubberDuck(k); break;
                case Accessory.Flower: DrawFlower(k); break;
                case Accessory.WizardHat: DrawWizardHat(k); break;
            }
        }

        static void DrawStrawHat(Sk k)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(k.E(0f, -0.895f, 0.615f, 0.105f));
                k.FillGrad(p, k.Box(-0.62f, -1.00f, 1.24f, 0.21f),
                           Color.FromArgb(255, 250, 225, 166), Color.FromArgb(255, 214, 172, 100), 90f);
                k.Stroke(p, Palette.Outline, 0.016f);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.375f, -0.895f), P(-0.372f, -0.995f), P(-0.205f, -1.055f), P(0f, -1.055f));
                p.AddBezier(P(0f, -1.055f), P(0.205f, -1.055f), P(0.372f, -0.995f), P(0.375f, -0.895f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.38f, -1.06f, 0.76f, 0.17f),
                           Color.FromArgb(255, 253, 236, 188), Color.FromArgb(255, 228, 190, 122), 90f);
                k.Stroke(p, Palette.Outline, 0.016f);
            }
            using (Pen ribbon = k.NewPen(Color.FromArgb(255, 220, 88, 96), 0.045f))
            {
                k.G.DrawBezier(ribbon, P(-0.330f, -0.905f), P(-0.130f, -0.940f), P(0.130f, -0.940f), P(0.330f, -0.905f));
            }
            using (Pen weave = k.NewPen(Color.FromArgb(90, 170, 128, 62), 0.008f))
            {
                k.G.DrawArc(weave, k.E(0f, -0.895f, 0.53f, 0.070f), 10f, 160f);
                k.G.DrawArc(weave, k.E(0f, -0.895f, 0.35f, 0.042f), 10f, 160f);
                k.G.DrawArc(weave, k.E(0f, -1.000f, 0.24f, 0.075f), 200f, 140f);
            }
        }

        static void DrawSunglasses(Sk k)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(k.E(s * 0.198f, -0.338f, 0.168f, 0.122f));
                    k.FillGrad(p, k.Box(s * 0.198f - 0.17f, -0.46f, 0.34f, 0.25f),
                               Color.FromArgb(255, 58, 76, 110), Color.FromArgb(255, 12, 18, 32), 90f);
                    k.Stroke(p, Palette.Outline, 0.016f);
                }
                using (SolidBrush glare = new SolidBrush(Color.FromArgb(120, 180, 214, 255)))
                {
                    PointF[] pts = new PointF[]
                    {
                        P(s * 0.198f - 0.075f, -0.420f),
                        P(s * 0.198f - 0.020f, -0.440f),
                        P(s * 0.198f + 0.045f, -0.290f),
                        P(s * 0.198f - 0.010f, -0.275f)
                    };
                    k.G.FillPolygon(glare, pts);
                }
            }
            using (Pen bridge = k.NewPen(Palette.Outline, 0.024f))
            {
                k.G.DrawLine(bridge, P(-0.045f, -0.352f), P(0.045f, -0.352f));
                k.G.DrawLine(bridge, P(-0.360f, -0.360f), P(-0.470f, -0.380f));
                k.G.DrawLine(bridge, P(0.360f, -0.360f), P(0.470f, -0.380f));
            }
        }

        static void DrawCrown(Sk k)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddLine(P(-0.205f, -0.930f), P(-0.205f, -0.995f));
                p.AddLine(P(-0.205f, -0.995f), P(-0.125f, -0.945f));
                p.AddLine(P(-0.125f, -0.945f), P(-0.060f, -1.040f));
                p.AddLine(P(-0.060f, -1.040f), P(0.000f, -0.955f));
                p.AddLine(P(0.000f, -0.955f), P(0.060f, -1.040f));
                p.AddLine(P(0.060f, -1.040f), P(0.125f, -0.945f));
                p.AddLine(P(0.125f, -0.945f), P(0.205f, -0.995f));
                p.AddLine(P(0.205f, -0.995f), P(0.205f, -0.930f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.21f, -1.04f, 0.42f, 0.12f),
                           Color.FromArgb(255, 255, 236, 160), Color.FromArgb(255, 226, 170, 60), 90f);
                k.Stroke(p, Color.FromArgb(255, 110, 76, 30), 0.014f);
            }
            using (GraphicsPath p = RectPath(-0.212f, -0.938f, 0.424f, 0.030f, 0.010f))
            {
                k.Fill(p, Color.FromArgb(255, 246, 200, 96));
                k.Stroke(p, Color.FromArgb(255, 110, 76, 30), 0.010f);
            }
            k.Blob(0f, -0.972f, 0.020f, 0.020f, Color.FromArgb(255, 226, 84, 96));
            k.Dot(0f, -0.972f, 0.008f, Color.FromArgb(255, 255, 220, 220));
            k.Blob(-0.130f, -0.960f, 0.013f, 0.013f, Color.FromArgb(255, 92, 148, 236));
            k.Blob(0.130f, -0.960f, 0.013f, 0.013f, Color.FromArgb(255, 92, 148, 236));
        }

        static void DrawSantaHat(Sk k)
        {
            // 白色绒边先画，帽身压在其上
            using (GraphicsPath brim = new GraphicsPath())
            {
                brim.AddEllipse(k.E(0f, -0.882f, 0.342f, 0.052f));
                k.FillGrad(brim, k.Box(-0.35f, -0.94f, 0.70f, 0.12f),
                           Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 222, 230, 244), 90f);
                k.Stroke(brim, Palette.Outline, 0.012f);
            }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.StartFigure();
                p.AddBezier(P(-0.300f, -0.880f), P(-0.300f, -0.990f), P(-0.080f, -1.070f), P(0.120f, -1.055f));
                p.AddBezier(P(0.120f, -1.055f), P(0.200f, -1.046f), P(0.218f, -1.012f), P(0.186f, -0.994f));
                p.AddBezier(P(0.186f, -0.994f), P(0.120f, -0.966f), P(0.140f, -0.910f), P(0.300f, -0.878f));
                p.CloseFigure();
                k.FillGrad(p, k.Box(-0.30f, -1.06f, 0.60f, 0.19f),
                           Color.FromArgb(255, 250, 110, 118), Color.FromArgb(255, 192, 40, 64), 90f);
                k.Stroke(p, Palette.Outline, 0.015f);
            }
            // 帽尖绒球
            k.Blob(0.178f, -1.037f, 0.045f, 0.043f, Color.FromArgb(255, 255, 255, 255));
            k.Ring(0.178f, -1.037f, 0.045f, 0.043f, Palette.Outline, 0.012f);
            using (Pen fold = k.NewPen(Color.FromArgb(120, 255, 196, 196), 0.010f))
            {
                k.G.DrawBezier(fold, P(-0.230f, -0.925f), P(-0.170f, -1.000f), P(-0.040f, -1.035f), P(0.100f, -1.035f));
            }
            using (Pen fur = k.NewPen(Color.FromArgb(160, 190, 202, 228), 0.008f))
            {
                k.G.DrawArc(fur, k.E(0f, -0.882f, 0.300f, 0.040f), 200f, 140f);
            }
        }

        static void DrawDivingMask(Sk k)
        {
            using (GraphicsPath frame = new GraphicsPath())
            {
                frame.AddEllipse(k.E(0f, -0.338f, 0.430f, 0.150f));
                k.FillGrad(frame, k.Box(-0.43f, -0.49f, 0.86f, 0.31f),
                           Color.FromArgb(255, 96, 150, 220), Color.FromArgb(255, 42, 86, 158), 90f);
                k.Stroke(frame, Palette.Outline, 0.016f);
            }
            using (GraphicsPath glass = new GraphicsPath())
            {
                glass.AddEllipse(k.E(0f, -0.338f, 0.380f, 0.112f));
                k.Fill(glass, Color.FromArgb(120, 190, 232, 255));
            }
            using (Pen glare = k.NewPen(Color.FromArgb(160, 255, 255, 255), 0.012f))
            {
                k.G.DrawArc(glare, k.E(0f, -0.338f, 0.330f, 0.092f), 200f, 80f);
            }
            using (Pen strap = k.NewPen(Color.FromArgb(255, 52, 88, 150), 0.040f))
            {
                k.G.DrawLine(strap, P(-0.420f, -0.330f), P(-0.560f, -0.300f));
                k.G.DrawLine(strap, P(0.420f, -0.330f), P(0.560f, -0.300f));
            }
            using (Pen tubeO = k.NewPen(Palette.Outline, 0.062f))
            {
                k.G.DrawBezier(tubeO, P(0.435f, -0.300f), P(0.500f, -0.380f), P(0.480f, -0.620f), P(0.505f, -0.700f));
            }
            using (Pen tube = k.NewPen(Color.FromArgb(255, 126, 176, 238), 0.042f))
            {
                k.G.DrawBezier(tube, P(0.435f, -0.300f), P(0.500f, -0.380f), P(0.480f, -0.620f), P(0.505f, -0.700f));
            }
            using (GraphicsPath mouth = new GraphicsPath())
            {
                mouth.AddEllipse(k.E(0.510f, -0.715f, 0.060f, 0.030f));
                k.Fill(mouth, Color.FromArgb(255, 248, 190, 96));
                k.Stroke(mouth, Palette.Outline, 0.010f);
            }
        }

        static void DrawBambooCopter(Sk k, float t)
        {
            using (Pen stem = k.NewPen(Color.FromArgb(255, 86, 150, 88), 0.024f))
            {
                k.G.DrawLine(stem, P(0f, -0.965f), P(0f, -1.030f));
            }
            k.Blob(0f, -1.038f, 0.028f, 0.024f, Color.FromArgb(255, 224, 186, 96));
            k.Ring(0f, -1.038f, 0.028f, 0.024f, Palette.Outline, 0.009f);

            GraphicsState st = k.G.Save();
            try
            {
                k.G.TranslateTransform(0f, -1.038f - 1f);
                k.G.RotateTransform(t * 240f);
                for (int i = 0; i < 2; i++)
                {
                    float ang = i * 90f;
                    GraphicsState bs = k.G.Save();
                    try
                    {
                        k.G.RotateTransform(ang);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            using (GraphicsPath blade = new GraphicsPath())
                            {
                                blade.AddEllipse(Lr(s * 0.175f, 0f, 0.175f, 0.044f));
                                k.FillGrad(blade, new RectangleF(s * 0.175f - 0.175f, -0.044f, 0.35f, 0.088f),
                                           Color.FromArgb(255, 162, 222, 146), Color.FromArgb(255, 74, 156, 88), 90f);
                                k.Stroke(blade, Palette.Outline, 0.011f);
                            }
                            using (SolidBrush sh = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                            {
                                k.G.FillEllipse(sh, Lr(s * 0.175f, -0.012f, 0.085f, 0.010f));
                            }
                        }
                    }
                    finally
                    {
                        k.G.Restore(bs);
                    }
                }
            }
            finally
            {
                k.G.Restore(st);
            }
        }

        static void DrawHeadphones(Sk k)
        {
            using (GraphicsPath band = new GraphicsPath())
            {
                band.StartFigure();
                band.AddBezier(P(-0.560f, -0.330f), P(-0.585f, -0.850f), P(-0.300f, -1.020f), P(0f, -1.020f));
                band.AddBezier(P(0f, -1.020f), P(0.300f, -1.020f), P(0.585f, -0.850f), P(0.560f, -0.330f));
                using (Pen outer = k.NewPen(Palette.Outline, 0.080f))
                    k.G.DrawPath(outer, band);
                using (Pen inner = k.NewPen(Color.FromArgb(255, 70, 92, 136), 0.052f))
                    k.G.DrawPath(inner, band);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                using (GraphicsPath cup = new GraphicsPath())
                {
                    cup.AddEllipse(k.E(s * 0.555f, -0.320f, 0.100f, 0.135f));
                    k.FillGrad(cup, k.Box(s * 0.555f - 0.10f, -0.46f, 0.20f, 0.28f),
                               Color.FromArgb(255, 84, 108, 156), Color.FromArgb(255, 36, 50, 84), 90f);
                    k.Stroke(cup, Palette.Outline, 0.015f);
                }
                using (GraphicsPath pad = new GraphicsPath())
                {
                    pad.AddEllipse(k.E(s * 0.535f, -0.310f, 0.062f, 0.092f));
                    k.Fill(pad, Color.FromArgb(255, 52, 68, 104));
                }
                k.Blob(s * 0.590f, -0.360f, 0.018f, 0.024f, Color.FromArgb(140, 210, 230, 255));
            }
        }

        static void DrawRubberDuck(Sk k)
        {
            using (GraphicsPath tail = new GraphicsPath())
            {
                tail.AddPolygon(new PointF[]
                {
                    P(0.180f, -0.935f), P(0.108f, -0.968f), P(0.176f, -0.900f)
                });
                k.Fill(tail, Color.FromArgb(255, 244, 200, 80));
                k.Stroke(tail, Palette.Outline, 0.010f);
            }
            using (GraphicsPath body = new GraphicsPath())
            {
                body.AddEllipse(k.E(0.272f, -0.905f, 0.135f, 0.092f));
                k.FillGrad(body, k.Box(0.14f, -1.00f, 0.28f, 0.19f),
                           Color.FromArgb(255, 255, 236, 144), Color.FromArgb(255, 240, 186, 58), 90f);
                k.Stroke(body, Palette.Outline, 0.014f);
            }
            k.Blob(0.345f, -0.985f, 0.078f, 0.072f, Color.FromArgb(255, 255, 238, 152));
            k.Ring(0.345f, -0.985f, 0.078f, 0.072f, Palette.Outline, 0.012f);
            using (GraphicsPath beak = new GraphicsPath())
            {
                beak.AddPolygon(new PointF[]
                {
                    P(0.408f, -0.998f), P(0.478f, -0.982f), P(0.408f, -0.965f)
                });
                k.Fill(beak, Color.FromArgb(255, 246, 152, 62));
                k.Stroke(beak, Palette.Outline, 0.008f);
            }
            k.Dot(0.363f, -1.000f, 0.010f, Color.FromArgb(255, 36, 48, 70));
            k.Blob(0.240f, -0.930f, 0.050f, 0.016f, Color.FromArgb(140, 255, 255, 255));
        }

        static void DrawFlower(Sk k)
        {
            float u = 0.435f, v = -0.770f;
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(i * Math.PI * 2.0 / 5.0 - Math.PI / 2.0);
                float pu = u + C(a) * 0.055f;
                float pv = v + S(a) * 0.055f;
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(k.E(pu, pv, 0.050f, 0.056f));
                    k.FillGrad(p, k.Box(pu - 0.05f, pv - 0.06f, 0.10f, 0.12f),
                               Color.FromArgb(255, 255, 186, 206), Color.FromArgb(255, 240, 110, 152), 90f);
                    k.Stroke(p, Color.FromArgb(255, 160, 70, 110), 0.010f);
                }
            }
            k.Blob(u, v, 0.040f, 0.040f, Color.FromArgb(255, 255, 220, 110));
            k.Ring(u, v, 0.040f, 0.040f, Color.FromArgb(255, 170, 120, 40), 0.009f);
            using (Pen leaf = k.NewPen(Color.FromArgb(255, 96, 168, 120), 0.016f))
            {
                k.G.DrawBezier(leaf, P(u + 0.030f, v + 0.065f), P(u + 0.070f, v + 0.100f), P(u + 0.090f, v + 0.120f), P(u + 0.078f, v + 0.145f));
            }
        }

        static void DrawWizardHat(Sk k)
        {
            using (GraphicsPath brim = new GraphicsPath())
            {
                brim.AddEllipse(k.E(0f, -0.952f, 0.455f, 0.052f));
                k.FillGrad(brim, k.Box(-0.46f, -1.01f, 0.92f, 0.11f),
                           Color.FromArgb(255, 118, 106, 216), Color.FromArgb(255, 66, 56, 156), 90f);
                k.Stroke(brim, Palette.Outline, 0.014f);
            }
            using (GraphicsPath cone = new GraphicsPath())
            {
                cone.StartFigure();
                cone.AddBezier(P(-0.300f, -0.950f), P(-0.250f, -1.012f), P(-0.100f, -1.070f), P(0.030f, -1.088f));
                cone.AddBezier(P(0.030f, -1.088f), P(0.080f, -1.090f), P(0.098f, -1.070f), P(0.078f, -1.050f));
                cone.AddBezier(P(0.078f, -1.050f), P(0.010f, -1.002f), P(0.150f, -0.970f), P(0.300f, -0.950f));
                cone.CloseFigure();
                k.FillGrad(cone, k.Box(-0.31f, -1.09f, 0.62f, 0.15f),
                           Color.FromArgb(255, 158, 146, 248), Color.FromArgb(255, 96, 82, 196), 90f);
                k.Stroke(cone, Palette.Outline, 0.015f);
            }
            using (GraphicsPath band = new GraphicsPath())
            {
                band.AddBezier(P(-0.288f, -0.958f), P(-0.140f, -0.984f), P(0.140f, -0.984f), P(0.288f, -0.958f));
                using (Pen p = k.NewPen(Color.FromArgb(255, 244, 206, 110), 0.028f))
                    k.G.DrawPath(p, band);
            }
            DrawSparkleAt(k, -0.110f, -1.000f, 0.042f, Color.FromArgb(255, 255, 232, 150), 0.4f);
            DrawSparkleAt(k, 0.120f, -1.020f, 0.030f, Color.FromArgb(255, 255, 255, 220), 1.2f);
            DrawSparkleAt(k, 0.010f, -0.920f, 0.030f, Color.FromArgb(255, 255, 226, 140), 2.0f);
        }
    }
}
