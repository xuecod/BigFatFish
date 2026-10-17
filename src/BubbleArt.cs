// BubbleArt.cs —— 台词气泡（自己实现，保证任何文本都严格画在给定 rect 内、尾巴是干净的小三角）
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace FishPet
{
    public static class BubbleArt
    {
        private const float PadX = 12f;
        private const float PadY = 8f;
        private const float LineGap = 1.22f;

        // ---------- 文本换行 ----------
        private static List<string> Wrap(Graphics g, string text, Font font, float maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            string[] paragraphs = text.Replace("\r", "").Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                string para = paragraphs[p];
                if (para.Length == 0) { lines.Add(""); continue; }

                StringBuilder cur = new StringBuilder();
                for (int i = 0; i < para.Length; i++)
                {
                    char ch = para[i];
                    cur.Append(ch);
                    float w = g.MeasureString(cur.ToString(), font).Width;
                    if (w > maxWidth && cur.Length > 1)
                    {
                        // 把最后一个字符挪到下一行
                        cur.Length -= 1;
                        lines.Add(cur.ToString());
                        cur.Length = 0;
                        cur.Append(ch);
                    }
                }
                if (cur.Length > 0) lines.Add(cur.ToString());
            }
            return lines;
        }

        public static SizeF Measure(Graphics g, string text, Font font, int maxTextWidth)
        {
            float maxW = Math.Max(40f, maxTextWidth - PadX * 2f);
            List<string> lines = Wrap(g, text, font, maxW);
            float lineH = font.GetHeight(g) * LineGap;
            float widest = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                float w = g.MeasureString(lines[i], font).Width;
                if (w > widest) widest = w;
            }
            float w2 = Math.Min(maxTextWidth, widest + PadX * 2f);
            float h2 = lines.Count * lineH + PadY * 2f;
            if (h2 < 30f) h2 = 30f;
            return new SizeF(w2, h2);
        }

        /// <summary>
        /// 画气泡。rect 是气泡外框（严格不超出），tail 是尾巴尖想指向的点（会自动钳制在 rect 内）。
        /// </summary>
        public static void Draw(Graphics g, string text, Font font, RectangleF rect, PointF tail, bool facingLeft)
        {
            if (rect.Width < 20f || rect.Height < 16f) return;

            float radius = Math.Min(13f, rect.Height / 2.4f);
            float tailH = Math.Min(rect.Height * 0.13f, 12f);
            float tailW = tailH * 1.5f;

            // 尾巴尖的 x（钳制在气泡内，留出圆弧）
            float tailX = tail.X;
            float minX = rect.X + radius + tailW * 0.7f;
            float maxX = rect.Right - radius - tailW * 0.7f;
            if (maxX < minX) { minX = rect.X + rect.Width * 0.35f; maxX = rect.X + rect.Width * 0.65f; }
            if (tailX < minX) tailX = minX;
            if (tailX > maxX) tailX = maxX;
            float baseY = rect.Bottom;

            using (GraphicsPath body = RoundedRect(rect, radius))
            {
                // 柔和阴影
                using (SolidBrush sh = new SolidBrush(Color.FromArgb(28, 20, 40, 80)))
                {
                    RectangleF sr = rect;
                    sr.Offset(0f, 2.5f);
                    using (GraphicsPath sp = RoundedRect(sr, radius)) g.FillPath(sh, sp);
                }
                // 气泡底色（白 -> 极淡蓝，稍微有渐变）
                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new RectangleF(rect.X, rect.Y, rect.Width, rect.Height + 1f),
                    Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 236, 244, 253), 90f))
                    g.FillPath(bg, body);

                // 尾巴：先填白（盖掉底边一段描边），再补两条斜边
                PointF tip = new PointF(tailX, baseY + tailH);
                PointF left = new PointF(tailX - tailW / 2f, baseY - 1.5f);
                PointF right = new PointF(tailX + tailW / 2f, baseY - 1.5f);
                using (SolidBrush fb = new SolidBrush(Color.FromArgb(255, 246, 250, 255)))
                    g.FillPolygon(fb, new PointF[] { left, tip, right });
                using (Pen bp = new Pen(Color.FromArgb(255, 150, 180, 225), 1.2f))
                {
                    g.DrawLine(bp, left, tip);
                    g.DrawLine(bp, tip, right);
                }
                // 气泡边框
                using (Pen bp = new Pen(Color.FromArgb(255, 150, 180, 225), 1.2f))
                    g.DrawPath(bp, body);

                // 文字：严格画在 rect 内
                float maxW = rect.Width - PadX * 2f;
                List<string> lines = Wrap(g, text, font, maxW);
                float lineH = font.GetHeight(g) * LineGap;
                float totalH = lines.Count * lineH;
                float y = rect.Y + (rect.Height - totalH) / 2f;

                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Near;
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    using (SolidBrush tb = new SolidBrush(Palette.BubbleText))
                    {
                        for (int i = 0; i < lines.Count; i++)
                        {
                            RectangleF lr = new RectangleF(rect.X + PadX, y + i * lineH, maxW, lineH);
                            if (lr.Bottom > rect.Bottom - PadY * 0.5f) break;
                            g.DrawString(lines[i], font, tb, lr, sf);
                        }
                    }
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            float d = Math.Max(2f, radius * 2f);
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            gp.AddArc(r.X, r.Y, d, d, 180f, 90f);
            gp.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            gp.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            gp.CloseFigure();
            return gp;
        }
    }
}
