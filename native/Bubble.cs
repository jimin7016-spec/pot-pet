// 말풍선: 화분 위에 잠깐 뜨는 도트 느낌 네모 말풍선. 클릭은 통과하고, 글이 바뀔 때만 다시 그려요.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace PotPet {
  sealed class Bubble : LayeredForm {
    readonly LayeredSurface surf = new LayeredSurface(1, 1);
    readonly Font font = Native.Font(16);
    string text = "";
    int lastX = int.MinValue, lastY = int.MinValue;
    public bool IsShown { get; private set; }

    public Bubble() : base(true) { Size = new Size(1, 1); }

    static readonly Color Bg = ColorTranslator.FromHtml("#fffdf3"), Ink = ColorTranslator.FromHtml("#3a2a33");

    /** 한글은 띄어쓰기 단위로 줄바꿈 (keep-all) */
    List<string> Wrap(Graphics g, string s, float maxW) {
      var lines = new List<string>();
      var fmt = StringFormat.GenericTypographic;
      foreach (var para in s.Split('\n')) {
        string cur = "";
        foreach (var word in para.Split(' ')) {
          string next = cur.Length == 0 ? word : cur + " " + word;
          if (cur.Length > 0 && g.MeasureString(next, font, 10000, fmt).Width > maxW) { lines.Add(cur); cur = word; }
          else cur = next;
        }
        lines.Add(cur);
      }
      return lines;
    }

    public void SetText(string t) {
      if (t == text && surf.Width > 1) return;
      text = t ?? "";
      int pad = Native.D(6), padX = Native.D(10), b = Native.D(2), shX = Native.D(3), shY = Native.D(5), maxW = Native.D(180);
      int lineH = (int)Math.Ceiling(font.Size * 1.4);
      List<string> lines; float w = 0;
      using (var bmp = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bmp)) {
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        lines = Wrap(g, text, maxW);
        foreach (var l in lines) w = Math.Max(w, g.MeasureString(l, font, 10000, StringFormat.GenericTypographic).Width);
      }
      int boxW = (int)Math.Ceiling(w) + padX * 2, boxH = lines.Count * lineH + pad * 2;
      surf.Resize(boxW + b * 2 + shX, boxH + b * 2 + shY);
      surf.Clear();
      using (var g = surf.Graphics()) {
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        using (var sh = new SolidBrush(Color.FromArgb(46, 0, 0, 0))) g.FillRectangle(sh, b + shX, b + shY, boxW, boxH);
        using (var ink = new SolidBrush(Ink)) {
          // 모서리를 깎은 네모 테두리 (box-shadow 4방향 2px 와 같은 모양)
          g.FillRectangle(ink, b, 0, boxW, b); g.FillRectangle(ink, b, b + boxH, boxW, b);
          g.FillRectangle(ink, 0, b, b, boxH); g.FillRectangle(ink, b + boxW, b, b, boxH);
          using (var bg = new SolidBrush(Bg)) g.FillRectangle(bg, b, b, boxW, boxH);
          for (int i = 0; i < lines.Count; i++) {
            float lw = g.MeasureString(lines[i], font, 10000, StringFormat.GenericTypographic).Width;
            g.DrawString(lines[i], font, ink, b + (boxW - lw) / 2f, b + pad + i * lineH + (lineH - font.Size) / 2f - 1, StringFormat.GenericTypographic);
          }
        }
      }
      lastX = int.MinValue;
    }

    /** 말풍선 아래쪽 가운데를 (cx, bottom) 에 맞춰 보여주기 */
    public void ShowAt(int cx, int bottom) {
      int x = cx - (surf.Width - Native.D(3)) / 2, y = bottom - surf.Height + Native.D(5);
      if (!IsShown) { IsShown = true; Show(); }
      if (x != lastX || y != lastY) { lastX = x; lastY = y; surf.Present(Handle, x, y); }
    }
    /** 점검용: 지금 말풍선 그림을 PNG 로 */
    public void SaveTo(string file) { using (var g = surf.Graphics()) { } using (var bmp = new Bitmap(surf.Width, surf.Height, surf.Width * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, surf.Bits)) bmp.Save(file); }
    public void HideBubble() { if (IsShown) { IsShown = false; Hide(); } }
    protected override void Dispose(bool disposing) { if (disposing) { surf.Dispose(); font.Dispose(); } base.Dispose(disposing); }
  }
}
