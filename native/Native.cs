// 윈도우 API 모음 + 투명 창(레이어드 윈도우) 그리기 도구 + 도트 폰트
// 투명 창은 한 번 만든 메모리(DIB)에 계속 그려서 UpdateLayeredWindow 로 보내요. 매번 비트맵을 새로 만들지 않아요.
// 완전히 투명한 픽셀은 클릭이 뒤 창으로 그냥 통과해서, 마우스 위치를 계속 물어볼 필요가 없어요.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PotPet {
  static class Native {
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;
    public const int WM_HOTKEY = 0x312, WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
    public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER {
      public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public int cbSize; public uint dwTime; }

    [DllImport("user32.dll", SetLastError = true)] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] public static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, int mods, int vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);
    [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

    /** 키보드·마우스를 안 쓴 시간(초) */
    public static double IdleSeconds() {
      var li = new LASTINPUTINFO { cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO)) };
      if (!GetLastInputInfo(ref li)) return 0;
      return unchecked((uint)Environment.TickCount - li.dwTime) / 1000.0;
    }

    /** 화면 배율 (100% = 1, 125% = 1.25) */
    public static float Dpi = 1f;
    public static void InitDpi() {
      try { SetProcessDPIAware(); } catch { }
      IntPtr dc = GetDC(IntPtr.Zero);
      try { Dpi = GetDeviceCaps(dc, 88) / 96f; } finally { ReleaseDC(IntPtr.Zero, dc); }
      if (Dpi <= 0) Dpi = 1;
    }
    public static int D(double v) { return (int)Math.Round(v * Dpi); }

    /* ---------- 도트 폰트 (Neo둥근모, OFL) ---------- */
    static PrivateFontCollection fonts;
    static IntPtr fontMem;
    public static FontFamily Family {
      get {
        if (fonts == null) {
          fonts = new PrivateFontCollection();
          try {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PotPet.Font")) {
              var bytes = new byte[s.Length]; s.Read(bytes, 0, bytes.Length);
              fontMem = Marshal.AllocCoTaskMem(bytes.Length);
              Marshal.Copy(bytes, 0, fontMem, bytes.Length);
              fonts.AddMemoryFont(fontMem, bytes.Length);
              uint n = 0; AddFontMemResourceEx(fontMem, (uint)bytes.Length, IntPtr.Zero, ref n); // GDI(일반 Label) 에서도 쓰려면 필요
            }
          } catch { }
        }
        return fonts.Families.Length > 0 ? fonts.Families[0] : FontFamily.GenericSansSerif;
      }
    }
    /** 픽셀 크기로 폰트 (Neo둥근모는 16px 배수에서 가장 또렷해요) */
    public static Font Font(float px, FontStyle style = FontStyle.Regular) { return new Font(Family, px * Dpi, style, GraphicsUnit.Pixel); }

    public static Bitmap ResourceImage(string name) {
      using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) return s == null ? null : new Bitmap(s);
    }
  }

  /** 투명 창 하나를 위한 그리기 판. Bits 에 직접 쓰거나 Graphics 로 그린 뒤 Present */
  sealed class LayeredSurface : IDisposable {
    public int Width { get; private set; }
    public int Height { get; private set; }
    public IntPtr Bits { get; private set; }
    IntPtr dc, bmp, old;
    Bitmap gdiView;
    public LayeredSurface(int w, int h) { Resize(w, h); }
    public void Resize(int w, int h) {
      w = Math.Max(1, w); h = Math.Max(1, h);
      if (w == Width && h == Height && bmp != IntPtr.Zero) return;
      Free();
      Width = w; Height = h;
      var bi = new Native.BITMAPINFOHEADER { biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
      IntPtr screen = Native.GetDC(IntPtr.Zero);
      dc = Native.CreateCompatibleDC(screen);
      Native.ReleaseDC(IntPtr.Zero, screen);
      IntPtr bits;
      bmp = Native.CreateDIBSection(dc, ref bi, 0, out bits, IntPtr.Zero, 0);
      Bits = bits;
      old = Native.SelectObject(dc, bmp);
      gdiView = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
    }
    /** GDI+ 로 그리기 (말풍선 글씨 등). 미리 곱한 알파(PArgb) 그대로 써져요 */
    public Graphics Graphics() { return System.Drawing.Graphics.FromImage(gdiView); }
    public void Clear() { var zero = new int[Width]; for (int y = 0; y < Height; y++) Marshal.Copy(zero, 0, Bits + y * Width * 4, Width); }
    public void CopyFrom(int[] premultiplied) { Marshal.Copy(premultiplied, 0, Bits, Math.Min(premultiplied.Length, Width * Height)); }
    public void Present(IntPtr hwnd, int x, int y) {
      var pos = new Native.POINT(x, y); var size = new Native.SIZE(Width, Height); var src = new Native.POINT(0, 0);
      var blend = new Native.BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
      Native.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref pos, ref size, dc, ref src, 0, ref blend, 2);
    }
    void Free() {
      if (gdiView != null) { gdiView.Dispose(); gdiView = null; }
      if (dc != IntPtr.Zero) { Native.SelectObject(dc, old); if (bmp != IntPtr.Zero) Native.DeleteObject(bmp); Native.DeleteDC(dc); }
      dc = bmp = old = IntPtr.Zero;
    }
    public void Dispose() { Free(); }
  }

  /** 테두리 없는 투명 창. 클릭 통과 여부만 고르면 돼요 */
  class LayeredForm : Form {
    readonly bool clickThrough;
    public LayeredForm(bool clickThrough) {
      this.clickThrough = clickThrough;
      FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; TopMost = true;
      AutoScaleMode = AutoScaleMode.None;
    }
    protected override CreateParams CreateParams {
      get {
        var cp = base.CreateParams;
        cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
        if (clickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT;
        return cp;
      }
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override void WndProc(ref Message m) {
      if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
      base.WndProc(ref m);
    }
    /** 위치만 옮기기 (그림은 그대로) */
    public void MoveTo(int x, int y) { Native.SetWindowPos(Handle, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE); }
  }
}
