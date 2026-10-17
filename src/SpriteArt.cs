// SpriteArt.cs —— 立绘（精灵图）模式：直接使用真实角色 PNG，而不是代码手绘
//
// 用法：把透明背景的角色立绘放进 assets\sprites\（或 %APPDATA%\BigFatFish\sprites\），
// 文件名对应动作，缺哪个就用 idle.png 顶替；一个都没有时自动回退到 Art.cs 的手绘角色。
//
//   idle.png 待机 / walk.png 走动 / happy.png 开心 / surprise.png 惊吓 / sleep.png 睡着
//   sad.png 委屈 / cry.png 大哭 / drag.png 被拎起 / focus.png 专注 / relax.png 休息
//   eat.png 吃小鱼干 / eat_rice.png 铁盆扒饭 / love.png / dizzy.png / sick.png / cheer.png
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace FishPet
{
    /// <summary>角色绘制门面：优先用立绘，没有立绘就用手绘 Art</summary>
    public static class CharacterArt
    {
        /// <summary>角色外观：auto（有立绘用立绘）/ sprite（只用立绘）/ draw（只用手绘）</summary>
        public static string CharacterStyle = "auto";

        public static void ApplyStyleSetting(Settings cfg)
        {
            CharacterStyle = cfg == null || string.IsNullOrEmpty(cfg.CharacterStyle) ? "auto" : cfg.CharacterStyle;
        }

        public static bool UsingSprite
        {
            get { return CharacterStyle != "draw" && SpriteArt.HasAny; }
        }

        public static void Draw(Graphics g, RectangleF box, PetPose pose)
        {
            if (CharacterStyle == "draw") { Art.DrawFish(g, box, pose); return; }
            if (!SpriteArt.DrawCharacter(g, box, pose))
                Art.DrawFish(g, box, pose);
        }

        public static Bitmap RenderPose(PetPose pose, int width, int height)
        {
            if (!SpriteArt.HasAny) return Art.RenderPose(pose, width, height);
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Draw(g, new RectangleF(0, 0, width, height), pose);
            }
            return bmp;
        }

        /// <summary>贴地阴影：半身立绘（宽大于高）不画影子，免得悬空感奇怪</summary>
        public static bool WantShadow
        {
            get { return !(SpriteArt.HasAny && SpriteArt.CurrentIsBust); }
        }

        /// <summary>托盘/程序图标：优先用立绘的头部裁切，没有立绘才退回手绘图标</summary>
        public static Bitmap RenderTrayIcon(int size)
        {
            Bitmap src = SpriteArt.Get("idle");
            if (src == null) return Art.RenderTrayIcon(size);
            if (size < 8) size = 8;

            int cw = (int)(src.Width * 0.66f);
            int ch = cw;
            int cx = src.Width / 2 - cw / 2;
            int cy = (int)(src.Height * 0.03f);
            if (cx < 0) { cx = 0; cw = src.Width; }
            if (cy + ch > src.Height) ch = Math.Max(8, src.Height - cy);

            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (GraphicsPath clip = new GraphicsPath())
                {
                    clip.AddEllipse(0, 0, size - 1, size - 1);
                    g.SetClip(clip);
                    // 圆形底色，避免小尺寸下透明边缘发灰
                    using (SolidBrush back = new SolidBrush(Color.FromArgb(255, 246, 249, 255)))
                        g.FillEllipse(back, 0, 0, size - 1, size - 1);
                    g.DrawImage(src, new Rectangle(0, 0, size, size),
                        new Rectangle(cx, cy, cw, ch), GraphicsUnit.Pixel);
                }
            }
            return bmp;
        }
    }

    public static class SpriteArt
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly List<string> Dirs = new List<string>();
        private static bool _scanned;

        private static readonly Dictionary<PetMood, string> MoodFile = BuildMoodMap();

        private static Dictionary<PetMood, string> BuildMoodMap()
        {
            Dictionary<PetMood, string> m = new Dictionary<PetMood, string>();
            m[PetMood.Idle] = "idle";
            m[PetMood.Blink] = "idle";
            m[PetMood.Walk] = "walk";
            m[PetMood.Happy] = "happy";
            m[PetMood.Surprised] = "surprise";
            m[PetMood.Sleepy] = "sleep";
            m[PetMood.Sleep] = "sleep";
            m[PetMood.Eat] = "eat";
            m[PetMood.EatRice] = "eat_rice";
            m[PetMood.Drag] = "drag";
            m[PetMood.Focus] = "focus";
            m[PetMood.Relax] = "relax";
            m[PetMood.Sad] = "sad";
            m[PetMood.Love] = "love";
            m[PetMood.Dizzy] = "dizzy";
            m[PetMood.Sick] = "sick";
            m[PetMood.Cheer] = "cheer";
            return m;
        }

        private static void Scan()
        {
            if (_scanned) return;
            _scanned = true;
            Dirs.Clear();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Dirs.Add(Path.Combine(baseDir, "assets", "sprites"));
            Dirs.Add(Path.Combine(baseDir, "..", "assets", "sprites"));
            Dirs.Add(Path.Combine(baseDir, "..", "..", "assets", "sprites"));
            Dirs.Add(Path.Combine(Environment.CurrentDirectory, "assets", "sprites"));
            Dirs.Add(Path.Combine(Settings.DataDir, "sprites"));
        }

        public static void Rescan()
        {
            _scanned = false;
            foreach (KeyValuePair<string, Bitmap> kv in Cache)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            Cache.Clear();
            Scan();
        }

        /// <summary>首选立绘目录（用于菜单里"打开立绘目录"）</summary>
        public static string PrimaryDir
        {
            get
            {
                Scan();
                for (int i = 0; i < Dirs.Count; i++)
                {
                    try { if (Directory.Exists(Dirs[i])) return Dirs[i]; }
                    catch { }
                }
                return Dirs.Count > 0 ? Dirs[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sprites");
            }
        }

        public static bool HasAny
        {
            get
            {
                Scan();
                for (int i = 0; i < Dirs.Count; i++)
                {
                    try { if (Directory.Exists(Dirs[i]) && Directory.GetFiles(Dirs[i], "*.png").Length > 0) return true; }
                    catch { }
                }
                return false;
            }
        }

        public static Bitmap Get(string key)
        {
            Scan();
            if (string.IsNullOrEmpty(key)) return null;
            Bitmap cached;
            if (Cache.TryGetValue(key, out cached)) return cached;
            Bitmap found = null;
            for (int i = 0; i < Dirs.Count && found == null; i++)
            {
                try
                {
                    string p = Path.Combine(Dirs[i], key + ".png");
                    if (File.Exists(p))
                    {
                        using (Bitmap raw = new Bitmap(p))
                        {
                            found = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppPArgb);
                            using (Graphics g = Graphics.FromImage(found))
                            {
                                g.CompositingMode = CompositingMode.SourceCopy;
                                g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
                            }
                        }
                    }
                }
                catch { found = null; }
            }
            Cache[key] = found;
            return found;
        }

        private static Bitmap Pick(PetMood mood)
        {
            string file;
            if (!MoodFile.TryGetValue(mood, out file)) file = "idle";
            Bitmap b = Get(file);
            if (b == null) b = Get("idle");
            if (b == null)
            {
                for (int i = 0; i < Dirs.Count && b == null; i++)
                {
                    try
                    {
                        if (!Directory.Exists(Dirs[i])) continue;
                        string[] files = Directory.GetFiles(Dirs[i], "*.png");
                        if (files.Length > 0) b = Get(Path.GetFileNameWithoutExtension(files[0]));
                    }
                    catch { }
                }
            }
            return b;
        }

        /// <summary>当前使用的是否为"半身"立绘（宽 > 高）</summary>
        public static bool CurrentIsBust
        {
            get
            {
                Bitmap b = Pick(PetMood.Idle);
                if (b == null) return false;
                return b.Width > b.Height * 1.05f;
            }
        }

        /// <summary>画角色本体，成功返回 true（false 表示没有立绘，调用方回退到手绘）</summary>
        public static bool DrawCharacter(Graphics g, RectangleF box, PetPose pose)
        {
            // 1) 优先播放"由立绘生成的动画帧"（呼吸/摆动/鲸鱼尾都烘焙在帧里）
            string poseName = PoseNameFor(pose.Mood);
            Bitmap[] frames;
            int fps;
            if (TryGetFrames(poseName, out frames, out fps) && frames != null && frames.Length > 0)
            {
                int idx = (int)(pose.Time * fps) % frames.Length;
                if (idx < 0) idx += frames.Length;
                return DrawBitmap(g, box, frames[idx], pose, false);
            }

            // 2) 退回单张立绘（用 Squash/Bob 补一点动感）
            Bitmap bmp = Pick(pose.Mood);
            if (bmp == null) return false;
            return DrawBitmap(g, box, bmp, pose, true);
        }

        private static bool DrawBitmap(Graphics g, RectangleF box, Bitmap bmp, PetPose pose, bool applyMotion)
        {
            // 立绘允许比鱼框略宽（画布四周本来就是透明的），这样半身立绘不至于太小。
            float maxW = box.Width * 1.28f;
            float fit = Math.Min(maxW / bmp.Width, box.Height / bmp.Height);
            float w = bmp.Width * fit;
            float h = bmp.Height * fit;

            GraphicsState st = g.Save();
            // 立绘帧是从 560px 缩到 ~250px 画的：用 Bilinear 比 HighQualityBicubic 快好几倍，
            // 肉眼几乎看不出差别（这一步占渲染耗时的大头）。
            System.Drawing.Drawing2D.InterpolationMode oldInterp = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.TranslateTransform(box.X + box.Width / 2f, box.Bottom + (applyMotion ? pose.Bob : 0f));
            if (pose.Tilt != 0f) g.RotateTransform(pose.Tilt);
            float sy = (applyMotion && pose.Squash > 0f) ? pose.Squash : 1f;
            g.ScaleTransform(pose.FacingLeft ? -1f : 1f, sy);
            g.DrawImage(bmp, -w / 2f, -h, w, h);
            g.Restore(st);
            g.InterpolationMode = oldInterp;

            // 叠加层：铁盆白饭 / 装扮 / 情绪符号
            if (pose.Mood == PetMood.EatRice && Get("eat_rice") == null)
                DrawRiceBasin(g, box, pose, w, h, bmp.Width > bmp.Height * 1.05f);
            DrawAccessory(g, box, pose, w, h);
            DrawEmotes(g, box, pose, w, h);
            return true;
        }

        private static string PoseNameFor(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Walk: return "walk";
                case PetMood.Happy: return "happy";
                case PetMood.Love: return "happy";
                case PetMood.Surprised: return "surprise";
                case PetMood.Sleepy: return "sleep";
                case PetMood.Sleep: return "sleep";
                case PetMood.Eat: return "eat";
                case PetMood.Drag: return "drag";
                case PetMood.Focus: return "focus";
                case PetMood.Relax: return "relax";
                case PetMood.Sad: return "sad";
                case PetMood.Dizzy: return "dizzy";
                case PetMood.Sick: return "sick";
                case PetMood.Cheer: return "cheer";
                default: return "idle";
            }
        }

        // ---------- 动画帧（tools/make_anim.py 用立绘生成）----------
        private static readonly Dictionary<string, Bitmap[]> AnimCache = new Dictionary<string, Bitmap[]>();
        private static readonly Dictionary<string, int> AnimFps = new Dictionary<string, int>();
        private static readonly List<string> AnimOrder = new List<string>();
        private const int AnimCacheMax = 2;

        private static string FindAnimDir()
        {
            Scan();
            for (int i = 0; i < Dirs.Count; i++)
            {
                try
                {
                    string a = Path.Combine(Dirs[i], "anim");
                    if (Directory.Exists(a) && Directory.GetFiles(a, "*.png").Length > 0) return a;
                }
                catch { }
            }
            return null;
        }

        private static bool TryGetFrames(string poseName, out Bitmap[] frames, out int fps)
        {
            frames = null;
            fps = 12;
            if (string.IsNullOrEmpty(poseName)) return false;

            Bitmap[] cached;
            if (AnimCache.TryGetValue(poseName, out cached))
            {
                frames = cached;
                int f2;
                if (AnimFps.TryGetValue(poseName, out f2)) fps = f2;
                return frames != null && frames.Length > 0;
            }

            string dir = FindAnimDir();
            if (dir == null) { AnimCache[poseName] = null; return false; }

            try
            {
                string[] files = Directory.GetFiles(dir, poseName + "_*.png");
                if (files.Length == 0) { AnimCache[poseName] = null; return false; }
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                Bitmap[] arr = new Bitmap[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    using (Bitmap raw = new Bitmap(files[i]))
                    {
                        Bitmap conv = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(conv))
                        {
                            g.CompositingMode = CompositingMode.SourceCopy;
                            g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
                        }
                        arr[i] = conv;
                    }
                }

                fps = 12;
                try
                {
                    string manifest = Path.Combine(dir, "manifest.txt");
                    if (File.Exists(manifest))
                    {
                        foreach (string line in File.ReadAllLines(manifest))
                        {
                            string s = line.Trim();
                            if (s.Length == 0 || s[0] == '#') continue;
                            string[] parts = s.Split(' ');
                            if (parts.Length >= 3 && parts[0] == poseName)
                            {
                                int f;
                                if (int.TryParse(parts[2], out f) && f > 0) fps = f;
                            }
                        }
                    }
                }
                catch { }

                evictIfNeeded();
                AnimCache[poseName] = arr;
                AnimFps[poseName] = fps;
                AnimOrder.Add(poseName);
                frames = arr;
                return true;
            }
            catch
            {
                AnimCache[poseName] = null;
                return false;
            }
        }

        private static void evictIfNeeded()
        {
            while (AnimOrder.Count >= AnimCacheMax)
            {
                string oldest = AnimOrder[0];
                AnimOrder.RemoveAt(0);
                Bitmap[] old;
                if (AnimCache.TryGetValue(oldest, out old) && old != null)
                {
                    for (int i = 0; i < old.Length; i++)
                    {
                        try { old[i].Dispose(); } catch { }
                    }
                    AnimCache.Remove(oldest);
                }
            }
        }

        // =====================================================================
        //  招牌动作：双手捧铁盆扒白米饭（叠在立绘前面）
        // =====================================================================
        private static void DrawRiceBasin(Graphics g, RectangleF box, PetPose pose, float w, float h, bool bust)
        {
            float p = (pose.Time * 0.42f) % 1f;
            if (p < 0f) p += 1f;

            // 半身立绘：盆小一点、位置压低，别糊在脸上；竖版全身立绘：盆在胸前
            float cx = box.X + box.Width / 2f;
            float baseY = box.Bottom - h * (bust ? 0.12f : 0.30f);
            float tipY = box.Bottom - h * (bust ? 0.24f : 0.62f);
            float basinW = w * (bust ? 0.30f : 0.56f);
            float basinH = basinW * 0.34f;

            float cy;
            float tilt = 0f;
            float rice = pose.Rice;
            if (rice < 0f) rice = 0f;
            if (rice > 1f) rice = 1f;

            if (p < 0.25f)
            {
                cy = baseY + (float)Math.Sin(pose.Time * 9f) * h * 0.006f;
            }
            else if (p < 0.5f)
            {
                cy = baseY - h * 0.03f;
                tilt = -6f;
            }
            else if (p < 0.75f)
            {
                cy = tipY;
                tilt = bust ? -18f : -30f;
            }
            else
            {
                cy = baseY + h * 0.01f;
                tilt = 4f;
            }
            if (rice <= 0.001f)
            {
                cy = baseY + h * 0.02f;
                tilt = 6f;
            }

            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(tilt);

            RectangleF outer = new RectangleF(-basinW / 2f, 0f, basinW, basinH);
            using (LinearGradientBrush bg = new LinearGradientBrush(
                new RectangleF(-basinW / 2f, 0f, basinW, basinH * 2f),
                Color.FromArgb(255, 246, 248, 252), Color.FromArgb(255, 206, 214, 228), 90f))
                g.FillEllipse(bg, outer);
            using (SolidBrush inner = new SolidBrush(Color.FromArgb(255, 168, 180, 200)))
                g.FillEllipse(inner, new RectangleF(outer.X + basinW * 0.04f, outer.Y + basinH * 0.10f, basinW * 0.92f, basinH * 0.78f));

            if (rice > 0.001f)
            {
                float moundH = basinH * (0.45f + 1.05f * rice);
                RectangleF mound = new RectangleF(outer.X + basinW * 0.07f, outer.Y + basinH * 0.30f - moundH * 0.55f,
                    basinW * 0.86f, moundH);
                using (LinearGradientBrush rb = new LinearGradientBrush(
                    new RectangleF(mound.X, mound.Y, mound.Width, mound.Height + 4f),
                    Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 232, 238, 248), 90f))
                    g.FillEllipse(rb, mound);
                using (Pen rp = new Pen(Color.FromArgb(90, 120, 140, 175), Math.Max(1f, basinW * 0.006f)))
                    g.DrawEllipse(rp, mound);
                using (SolidBrush grain = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                using (Pen gp = new Pen(Color.FromArgb(120, 150, 168, 200), Math.Max(0.8f, basinW * 0.004f)))
                {
                    int n = 10 + (int)(10 * rice);
                    for (int i = 0; i < n; i++)
                    {
                        float ang = (float)(i * 2.399 + pose.Time * 0.2);
                        float rr = mound.Width * 0.40f * (0.25f + 0.75f * ((i % 5) / 4f));
                        float gx = mound.X + mound.Width / 2f + (float)Math.Cos(ang) * rr;
                        float gy = mound.Y + mound.Height * 0.55f + (float)Math.Sin(ang) * rr * 0.5f;
                        float gs = basinW * 0.035f;
                        RectangleF gr = new RectangleF(gx - gs / 2f, gy - gs * 0.35f, gs, gs * 0.7f);
                        g.FillEllipse(grain, gr);
                        g.DrawEllipse(gp, gr);
                    }
                }
            }

            using (Pen rim = new Pen(Color.FromArgb(255, 44, 74, 160), Math.Max(1.5f, basinW * 0.022f)))
                g.DrawEllipse(rim, outer);
            using (Pen hl = new Pen(Color.FromArgb(150, 255, 255, 255), Math.Max(1f, basinW * 0.010f)))
                g.DrawArc(hl, outer, 200f, 60f);

            if (p >= 0.25f && p < 0.75f && rice > 0.02f)
            {
                using (SolidBrush grain = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                using (Pen gp = new Pen(Color.FromArgb(140, 150, 168, 200), Math.Max(0.8f, basinW * 0.004f)))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        float t = (pose.Time * 3.1f + i * 0.7f) % 1f;
                        float gx = cx - basinW * 0.18f + basinW * 0.36f * i / 4f;
                        float gy = cy - basinH * 0.2f - t * h * 0.14f;
                        float gs = basinW * 0.032f;
                        RectangleF gr = new RectangleF(gx - gs / 2f, gy - gs * 0.35f, gs, gs * 0.7f);
                        g.FillEllipse(grain, gr);
                        g.DrawEllipse(gp, gr);
                    }
                }
            }
            g.Restore(st);
        }

        // =====================================================================
        //  装扮叠加（简化版，位置跟着立绘头部走）
        // =====================================================================
        private static void DrawAccessory(Graphics g, RectangleF box, PetPose pose, float w, float h)
        {
            if (pose.Accessory == Accessory.None) return;
            float top = box.Bottom - h;
            float headCY = top + h * 0.30f;
            float headR = w * 0.27f;
            float cx = box.X + box.Width / 2f;
            float s = pose.FacingLeft ? -1f : 1f;

            GraphicsState st = g.Save();
            g.TranslateTransform(cx, headCY);
            g.RotateTransform(pose.Tilt);
            g.ScaleTransform(s, 1f);

            Color straw1 = Color.FromArgb(255, 246, 216, 148);
            Color straw2 = Color.FromArgb(255, 206, 162, 84);
            switch (pose.Accessory)
            {
                case Accessory.StrawHat:
                    using (SolidBrush b = new SolidBrush(straw1))
                        g.FillEllipse(b, -headR * 1.5f, -headR * 0.95f, headR * 3f, headR * 0.5f);
                    using (SolidBrush b = new SolidBrush(straw2))
                        g.FillPie(b, -headR * 0.8f, -headR * 1.5f, headR * 1.6f, headR * 1.2f, 180f, 180f);
                    break;
                case Accessory.Crown:
                    {
                        PointF[] pts = new PointF[] {
                            new PointF(-headR*0.8f, -headR*0.85f), new PointF(-headR*0.55f, -headR*1.35f),
                            new PointF(-headR*0.28f, -headR*0.9f), new PointF(0f, -headR*1.45f),
                            new PointF(headR*0.28f, -headR*0.9f), new PointF(headR*0.55f, -headR*1.35f),
                            new PointF(headR*0.8f, -headR*0.85f)
                        };
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 252, 214, 92)))
                            g.FillPolygon(b, pts);
                        using (Pen p = new Pen(Color.FromArgb(255, 198, 148, 30), 2f))
                            g.DrawPolygon(p, pts);
                    }
                    break;
                case Accessory.SantaHat:
                    {
                        PointF[] pts = new PointF[] {
                            new PointF(-headR*0.85f, -headR*0.75f), new PointF(headR*0.55f, -headR*1.9f),
                            new PointF(headR*0.85f, -headR*0.75f)
                        };
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 226, 70, 80)))
                            g.FillPolygon(b, pts);
                        using (SolidBrush b = new SolidBrush(Color.White))
                        {
                            g.FillEllipse(b, -headR * 1.0f, -headR * 0.95f, headR * 2f, headR * 0.45f);
                            g.FillEllipse(b, headR * 0.3f, -headR * 2.1f, headR * 0.5f, headR * 0.5f);
                        }
                    }
                    break;
                case Accessory.WizardHat:
                    {
                        PointF[] cone = new PointF[] {
                            new PointF(-headR*0.75f, -headR*0.8f), new PointF(headR*0.1f, -headR*2.2f),
                            new PointF(headR*0.8f, -headR*0.8f)
                        };
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 92, 70, 178)))
                            g.FillPolygon(b, cone);
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 74, 54, 150)))
                            g.FillEllipse(b, -headR * 1.35f, -headR * 0.98f, headR * 2.7f, headR * 0.45f);
                    }
                    break;
                case Accessory.BambooCopter:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 156, 116, 62)))
                        g.FillRectangle(b, -headR * 0.06f, -headR * 1.7f, headR * 0.12f, headR * 0.7f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 168, 226, 122)))
                    {
                        float wob = (float)Math.Sin(pose.Time * 22f) * headR * 0.05f;
                        g.FillEllipse(b, -headR * 1.3f, -headR * 2.0f + wob, headR * 1.25f, headR * 0.22f);
                        g.FillEllipse(b, headR * 0.05f, -headR * 2.0f - wob, headR * 1.25f, headR * 0.22f);
                    }
                    break;
                case Accessory.Sunglasses:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 26, 34, 56)))
                    {
                        g.FillRectangle(b, -headR * 0.95f, -headR * 0.10f, headR * 0.78f, headR * 0.42f);
                        g.FillRectangle(b, headR * 0.17f, -headR * 0.10f, headR * 0.78f, headR * 0.42f);
                    }
                    using (Pen p = new Pen(Color.FromArgb(230, 26, 34, 56), Math.Max(1.5f, headR * 0.06f)))
                        g.DrawLine(p, -headR * 0.17f, headR * 0.06f, headR * 0.17f, headR * 0.06f);
                    break;
                case Accessory.DivingMask:
                    using (Pen p = new Pen(Color.FromArgb(255, 40, 130, 180), Math.Max(2f, headR * 0.09f)))
                        g.DrawEllipse(p, -headR * 1.0f, -headR * 0.16f, headR * 1.95f, headR * 0.62f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(70, 190, 235, 255)))
                        g.FillEllipse(b, -headR * 0.98f, -headR * 0.14f, headR * 1.9f, headR * 0.58f);
                    break;
                case Accessory.Headphones:
                    using (Pen p = new Pen(Color.FromArgb(255, 48, 58, 84), Math.Max(3f, headR * 0.14f)))
                        g.DrawArc(p, -headR * 1.05f, -headR * 1.08f, headR * 2.1f, headR * 1.9f, 190f, 160f);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 70, 84, 116)))
                    {
                        g.FillEllipse(b, -headR * 1.22f, -headR * 0.30f, headR * 0.42f, headR * 0.72f);
                        g.FillEllipse(b, headR * 0.80f, -headR * 0.30f, headR * 0.42f, headR * 0.72f);
                    }
                    break;
                case Accessory.BowTie:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 232, 62, 92)))
                    {
                        PointF[] l = new PointF[] { new PointF(-headR*0.5f, headR*1.25f), new PointF(-headR*0.08f, headR*1.45f), new PointF(-headR*0.5f, headR*1.65f) };
                        PointF[] r = new PointF[] { new PointF(headR*0.5f, headR*1.25f), new PointF(headR*0.08f, headR*1.45f), new PointF(headR*0.5f, headR*1.65f) };
                        g.FillPolygon(b, l);
                        g.FillPolygon(b, r);
                        g.FillEllipse(b, -headR * 0.12f, headR * 1.32f, headR * 0.24f, headR * 0.26f);
                    }
                    break;
                case Accessory.Scarf:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 248, 92, 104)))
                    {
                        g.FillEllipse(b, -headR * 0.95f, headR * 1.15f, headR * 1.9f, headR * 0.5f);
                        g.FillRectangle(b, headR * 0.25f, headR * 1.45f, headR * 0.35f, headR * 0.9f);
                    }
                    break;
                case Accessory.RubberDuck:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 250, 200, 60)))
                    {
                        g.FillEllipse(b, -headR * 1.55f, headR * 0.95f, headR * 0.75f, headR * 0.55f);
                        g.FillEllipse(b, -headR * 1.42f, headR * 0.72f, headR * 0.42f, headR * 0.42f);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 240, 140, 30)))
                        g.FillPolygon(b, new PointF[] { new PointF(-headR*1.05f, headR*1.02f), new PointF(-headR*0.92f, headR*1.10f), new PointF(-headR*1.05f, headR*1.18f) });
                    break;
                case Accessory.Flower:
                    for (int i = 0; i < 5; i++)
                    {
                        double ang = i * Math.PI * 2 / 5;
                        float px = (float)Math.Cos(ang) * headR * 0.3f;
                        float py = -headR * 1.05f + (float)Math.Sin(ang) * headR * 0.3f;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 252, 150, 186)))
                            g.FillEllipse(b, px - headR * 0.2f, py - headR * 0.2f, headR * 0.4f, headR * 0.4f);
                    }
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 206, 74)))
                        g.FillEllipse(b, -headR * 0.12f, -headR * 1.17f, headR * 0.24f, headR * 0.24f);
                    break;
            }
            g.Restore(st);
        }

        // =====================================================================
        //  情绪符号叠加
        // =====================================================================
        private static void DrawEmotes(Graphics g, RectangleF box, PetPose pose, float w, float h)
        {
            float top = box.Bottom - h;
            float headCY = top + h * 0.30f;
            float headR = w * 0.27f;
            float cx = box.X + box.Width / 2f;
            float t = pose.Time;

            GraphicsState st = g.Save();
            switch (pose.Mood)
            {
                case PetMood.Sleep:
                case PetMood.Sleepy:
                    {
                        using (Font f = new Font("Microsoft YaHei UI", Math.Max(9f, headR * 0.55f), FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 92, 130, 200)))
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float ph = (t * 0.5f + i * 0.33f) % 1f;
                                float sz = 0.6f + ph * 0.6f;
                                g.DrawString("Z", f, b, cx + headR * (0.7f + ph * 0.5f), headCY - headR * (0.9f + ph * 1.1f) - sz * 6f);
                            }
                        }
                    }
                    break;
                case PetMood.Love:
                    for (int i = 0; i < 3; i++)
                    {
                        float ph = (t * 0.6f + i * 0.4f) % 1f;
                        float hx = cx + (float)Math.Sin((i + 1) * 2.1) * headR * 1.1f;
                        float hy = headCY - headR * (1.0f + ph * 1.2f);
                        DrawHeart(g, hx, hy, headR * 0.24f * (1f - ph * 0.3f), Color.FromArgb((int)(240 * (1 - ph)), 255, 90, 130));
                    }
                    break;
                case PetMood.Dizzy:
                    using (Pen p = new Pen(Color.FromArgb(230, 90, 120, 190), Math.Max(2f, headR * 0.07f)))
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            float sx = cx + (i == 0 ? -headR * 1.0f : headR * 1.0f);
                            g.DrawArc(p, sx - headR * 0.28f, headCY - headR * 0.30f, headR * 0.56f, headR * 0.56f, 0f, 280f);
                        }
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float ang = (float)(t * 3.2 + i * 2.09);
                        float sx = cx + (float)Math.Cos(ang) * headR * 1.1f;
                        float sy = headCY - headR * 1.25f + (float)Math.Sin(ang) * headR * 0.22f;
                        DrawStar(g, sx, sy, headR * 0.15f, Color.FromArgb(240, 255, 214, 80));
                    }
                    break;
                case PetMood.Sad:
                case PetMood.Surprised:
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 150, 210, 250)))
                    {
                        float dy = (t * 0.8f) % 1f;
                        g.FillEllipse(b, cx + headR * 0.55f, headCY - headR * 0.9f + dy * headR * 0.5f, headR * 0.18f, headR * 0.26f);
                        if (pose.Mood == PetMood.Sad)
                            g.FillEllipse(b, cx - headR * 0.75f, headCY - headR * 0.7f + dy * headR * 0.6f, headR * 0.16f, headR * 0.24f);
                    }
                    break;
                case PetMood.Sick:
                    using (Pen p = new Pen(Color.FromArgb(180, 240, 120, 140), Math.Max(1.5f, headR * 0.05f)))
                    {
                        for (int i = -1; i <= 1; i++)
                            g.DrawArc(p, cx + i * headR * 0.35f - headR * 0.15f, headCY - headR * 1.5f, headR * 0.3f, headR * 0.45f, 200f, 140f);
                    }
                    break;
                case PetMood.Cheer:
                    for (int i = 0; i < 4; i++)
                    {
                        float ph = (t * 1.6f + i * 0.25f) % 1f;
                        float sx = cx + (i % 2 == 0 ? -1 : 1) * headR * (0.9f + ph * 0.8f);
                        float sy = headCY - headR * (0.9f + ph * 0.6f);
                        DrawStar(g, sx, sy, headR * 0.16f * (1f - ph * 0.4f), Color.FromArgb((int)(250 * (1 - ph)), 255, 226, 120));
                    }
                    break;
            }
            g.Restore(st);
        }

        private static void DrawHeart(Graphics g, float x, float y, float r, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddArc(x - r, y - r, r, r, 180f, 180f);
                gp.AddArc(x, y - r, r, r, 270f, 180f);
                gp.AddLine(x + r, y, x, y + r * 1.6f);
                gp.AddLine(x, y + r * 1.6f, x - r, y);
                gp.CloseFigure();
                g.FillPath(b, gp);
            }
        }

        private static void DrawStar(Graphics g, float x, float y, float r, Color c)
        {
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double ang = -Math.PI / 2 + i * Math.PI / 5;
                float rr = (i % 2 == 0) ? r : r * 0.42f;
                pts[i] = new PointF(x + (float)Math.Cos(ang) * rr, y + (float)Math.Sin(ang) * rr);
            }
            using (SolidBrush b = new SolidBrush(c))
                g.FillPolygon(b, pts);
        }
    }
}
