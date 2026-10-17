// Native.cs —— Win32 逐像素透明分层窗口（UpdateLayeredWindow + DIBSection）
// 透明的像素天然鼠标穿透：只有角色身上不透明的部分能接收点击。
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FishPet
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; public SIZE(int w, int h) { cx = w; cy = h; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBQUAD { public byte Blue, Green, Red, Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public RGBQUAD bmiColors;
        }

        public const int ULW_ALPHA = 0x00000002;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;
        public const int DIB_RGB_COLORS = 0;
        public const int BI_RGB = 0;

        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const int WS_MAXIMIZE = 0x01000000;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int SWP_NOSIZE = 0x0001;
        public const int SWP_NOMOVE = 0x0002;
        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_NOACTIVATE = 0x0010;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        /// <summary>系统空闲毫秒数（用于判断用户是否离开）</summary>
        public static uint GetIdleMilliseconds()
        {
            LASTINPUTINFO lii = new LASTINPUTINFO();
            lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref lii)) return 0;
            return (uint)Environment.TickCount - lii.dwTime;
        }

        /// <summary>
        /// 前台窗口是否"真正的全屏"（游戏/视频那种），用于自动隐身。
        /// 关键：最大化的窗口虽然也铺满屏幕，但**不算全屏**，否则桌宠会一直躲着不出现。
        /// </summary>
        public static bool IsForegroundFullscreen()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return false;

            System.Text.StringBuilder cls = new System.Text.StringBuilder(64);
            GetClassName(hwnd, cls, cls.Capacity);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return false;

            int style = GetWindowLong(hwnd, GWL_STYLE);
            if ((style & WS_MAXIMIZE) != 0) return false;

            RECT r;
            if (!GetWindowRect(hwnd, out r)) return false;
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return false;
            Rectangle scr = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds;
            bool coversX = r.Left <= scr.Left + 1 && r.Right >= scr.Right - 1;
            bool coversY = r.Top <= scr.Top + 1 && r.Bottom >= scr.Bottom - 1;
            return coversX && coversY;
        }
    }

    /// <summary>
    /// 把一个 32bppPArgb 位图推到分层窗口上。绘制 → Commit()。
    /// </summary>
    public sealed class LayeredCanvas : IDisposable
    {
        private IntPtr _hwnd;
        private Bitmap _bitmap;
        private Graphics _graphics;
        private IntPtr _hDib;
        private IntPtr _dibBits;
        private int _w;
        private int _h;
        private int _globalAlpha = 255;
        private bool _lastCommitOk;
        private int _lastCommitError;
        private int _commitCount;

        public IntPtr Handle { get { return _hwnd; } }
        public Bitmap Bitmap { get { return _bitmap; } }
        public Graphics Graphics { get { return _graphics; } }
        public int Width { get { return _w; } }
        public int Height { get { return _h; } }

        /// <summary>整体不透明度 0~255（由分层窗口的 SourceConstantAlpha 实现，边缘依然平滑）</summary>
        public int GlobalAlpha
        {
            get { return _globalAlpha; }
            set { _globalAlpha = value < 0 ? 0 : (value > 255 ? 255 : value); }
        }

        /// <summary>最近一次 UpdateLayeredWindow 是否成功（排障用）</summary>
        public bool LastCommitSucceeded { get { return _lastCommitOk; } }
        public int LastCommitError { get { return _lastCommitError; } }
        public int CommitCount { get { return _commitCount; } }

        public LayeredCanvas(IntPtr hwnd, int width, int height)
        {
            _hwnd = hwnd;
            CreateSurface(width, height);
        }

        private void CreateSurface(int width, int height)
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;
            ReleaseSurface();

            _w = width;
            _h = height;
            _bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            _graphics = Graphics.FromImage(_bitmap);
            _graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            _graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            _graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            _graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Native.BITMAPINFO bmi = new Native.BITMAPINFO();
            bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height;   // 负数 = 自上而下
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = Native.BI_RGB;

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            _hDib = Native.CreateDIBSection(screenDc, ref bmi, Native.DIB_RGB_COLORS, out _dibBits, IntPtr.Zero, 0);
            Native.ReleaseDC(IntPtr.Zero, screenDc);
            if (_hDib == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection 失败");
        }

        private void ReleaseSurface()
        {
            if (_graphics != null) { _graphics.Dispose(); _graphics = null; }
            if (_bitmap != null) { _bitmap.Dispose(); _bitmap = null; }
            if (_hDib != IntPtr.Zero) { Native.DeleteObject(_hDib); _hDib = IntPtr.Zero; _dibBits = IntPtr.Zero; }
        }

        public void Resize(int width, int height)
        {
            if (width == _w && height == _h) return;
            CreateSurface(width, height);
        }

        /// <summary>把当前位图内容提交到屏幕上的 (x,y)（屏幕像素坐标）。</summary>
        public void Commit(int x, int y)
        {
            BitmapData data = _bitmap.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int rowBytes = _w * 4;
                for (int row = 0; row < _h; row++)
                {
                    IntPtr src = new IntPtr(data.Scan0.ToInt64() + (long)row * data.Stride);
                    IntPtr dst = new IntPtr(_dibBits.ToInt64() + (long)row * rowBytes);
                    CopyMemory(dst, src, (uint)rowBytes);
                }
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr oldBmp = Native.SelectObject(memDc, _hDib);
            try
            {
                Native.POINT dstPt = new Native.POINT(x, y);
                Native.POINT srcPt = new Native.POINT(0, 0);
                Native.SIZE size = new Native.SIZE(_w, _h);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = (byte)_globalAlpha;
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                _lastCommitOk = Native.UpdateLayeredWindow(_hwnd, screenDc, ref dstPt, ref size, memDc, ref srcPt, 0, ref blend, Native.ULW_ALPHA);
                _lastCommitError = _lastCommitOk ? 0 : Marshal.GetLastWin32Error();
                _commitCount++;
            }
            finally
            {
                Native.SelectObject(memDc, oldBmp);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        /// <summary>从 DIB 回读像素（Windows 真正收到的那份数据），用于排障自检。</summary>
        public Bitmap SnapshotFromDib()
        {
            Bitmap bmp = new Bitmap(_w, _h, PixelFormat.Format32bppPArgb);
            if (_dibBits == IntPtr.Zero) return bmp;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, _w, _h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                int rowBytes = _w * 4;
                for (int row = 0; row < _h; row++)
                {
                    IntPtr src = new IntPtr(_dibBits.ToInt64() + (long)row * rowBytes);
                    IntPtr dst = new IntPtr(data.Scan0.ToInt64() + (long)row * data.Stride);
                    CopyMemory(dst, src, (uint)rowBytes);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        public void Dispose()
        {
            ReleaseSurface();
        }
    }
}
