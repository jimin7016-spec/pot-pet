// 장난감: 화면 위에서 떨어져 튕기는 공·털실·나비·비눗방울
// 장난감 크기만 한 작은 투명 창이 장난감을 따라다녀요(클릭 통과).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace PotPet {
  sealed class Toy : IDisposable {
    class Part { public double X, Y, VX, VY, Life; public Color C; }
    static readonly Random rnd = new Random();
    static double Rand(double a, double b) { return a + rnd.NextDouble() * (b - a); }

    public readonly string Kind;
    public double X, Y, VX, VY; // 화면 좌표 (가운데)
    public bool Dead, Popped, Finished;
    readonly Rectangle area; // 장난감이 노는 모니터
    readonly double floor, R, sc;
    double t, life, hover, run, deadT, trailT, hitsLeft = 14;
    int hits;
    readonly List<PointF> trail = new List<PointF>();
    readonly List<Part> parts = new List<Part>();
    readonly LayeredForm win = new LayeredForm(true);
    readonly LayeredSurface surf;
    readonly int half;
    static readonly Dictionary<string, Bitmap> frames = new Dictionary<string, Bitmap>();

    public Toy(string kind, double startX, double floorY, Rectangle display) {
      Kind = kind; area = display; floor = floorY;
      sc = 4 * Native.Dpi; R = 6 * sc; // 스프라이트 12x12 를 4배로, 반지름 약 24px
      X = Math.Min(area.Right - R, Math.Max(area.Left + R, startX)); Y = area.Top - R * 2;
      VX = Rand(-50, 50) * Native.Dpi; VY = kind == "butterfly" ? 40 * Native.Dpi : 0;
      life = kind == "bubble" ? 18 : 26; hover = Rand(60, 100) * Native.Dpi;
      half = (int)(R * 5);
      surf = new LayeredSurface(half * 2, half * 2);
      win.Size = new Size(1, 1);
      win.Show();
    }

    static Bitmap Frame(string kind, int f) {
      string k = kind + f; Bitmap b;
      if (frames.TryGetValue(k, out b)) return b;
      var g = Sprites.ToyGrid(kind, f);
      b = new Bitmap(g.W, g.H, PixelFormat.Format32bppArgb);
      for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) if (g[x, y] != 0) b.SetPixel(x, y, Color.FromArgb(g[x, y]));
      frames[k] = b;
      return b;
    }

    /** 화분이 받아쳤을 때 */
    public void Kick(double vx, double vy) {
      if (Dead) return;
      hits++;
      if (Kind == "bubble") { Pop(); return; }
      if (Kind == "butterfly") { VX = vx * 0.5; VY = -Math.Abs(vy) * 0.45; } else { VX = vx; VY = vy; }
      for (int i = 0; i < 5; i++) parts.Add(new Part { X = X, Y = Y, VX = Rand(-90, 90) * Native.Dpi, VY = Rand(-120, -20) * Native.Dpi, Life = 0.4, C = ColorTranslator.FromHtml("#fff3b0") });
      if (hits >= hitsLeft) life = Math.Min(life, t + 0.2);
    }
    void Pop() {
      for (int i = 0; i < 14; i++) parts.Add(new Part { X = X, Y = Y, VX = Rand(-160, 160) * Native.Dpi, VY = Rand(-160, 60) * Native.Dpi, Life = Rand(0.3, 0.6), C = ColorTranslator.FromHtml(i % 2 != 0 ? "#bfe6ff" : "#ffffff") });
      Dead = true; Popped = true;
    }

    void Step(double dt) {
      double u = Native.Dpi;
      t += dt;
      if (Dead) { deadT += dt; return; }
      if (t > life) { if (Kind == "bubble") Pop(); else Dead = true; return; }
      if (Kind == "ball" || Kind == "yarn") {
        double bounce = Kind == "ball" ? 0.62 : 0.3, fric = Kind == "ball" ? 1.1 : 3;
        VY += 1500 * u * dt;
        X += VX * dt; Y += VY * dt;
        if (Y + R > floor) {
          Y = floor - R;
          VY = Math.Abs(VY) > 140 * u ? -VY * bounce : 0;
          VX -= VX * Math.Min(1, fric * dt);
        }
      } else if (Kind == "butterfly") {
        double target = floor - hover + Math.Sin(t * 3) * 14 * u;
        VX += (Math.Sin(t * 1.3) * 70 * u - VX) * Math.Min(1, dt * 1.2);
        VY += ((target - Y) * 2.4 - VY * 1.7) * dt;
        VY = Math.Max(-300 * u, Math.Min(170 * u, VY));
        X += VX * dt; Y += VY * dt;
      } else {
        VX = Math.Sin(t * 2) * 40 * u; VY = 52 * u;
        X += VX * dt; Y += VY * dt;
        if (Y + R >= floor) Pop();
      }
      if (X < area.Left + R) { X = area.Left + R; VX = Math.Abs(VX) * 0.8; }
      if (X > area.Right - R) { X = area.Right - R; VX = -Math.Abs(VX) * 0.8; }
      run += Math.Abs(VX) * dt;
      trailT += dt;
      if (Kind == "yarn" && trailT > 0.03) { trailT = 0; trail.Add(new PointF((float)X, (float)Y)); if (trail.Count > 16) trail.RemoveAt(0); }
    }

    /** 한 프레임. 다 끝났으면 Finished = true */
    public void Tick(double dt) {
      dt = Math.Min(0.05, Math.Max(0.001, dt));
      for (int i = parts.Count - 1; i >= 0; i--) { var p = parts[i]; p.VY += 400 * Native.Dpi * dt; p.X += p.VX * dt; p.Y += p.VY * dt; p.Life -= dt; if (p.Life <= 0) parts.RemoveAt(i); }
      Step(dt);
      if (Dead && deadT > 0.5 && parts.Count == 0) { Finished = true; return; }
      Draw();
    }

    void Draw() {
      int ox = (int)X - half, oy = (int)Y - half, grid = (int)Math.Max(1, Math.Round(sc));
      surf.Clear();
      using (var g = surf.Graphics()) {
        g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
        foreach (var p in parts) {
          int a = (int)(255 * Math.Max(0, Math.Min(1, p.Life * 3)));
          using (var br = new SolidBrush(Color.FromArgb(a, p.C))) g.FillRectangle(br, (int)Math.Round(p.X / grid) * grid - ox, (int)Math.Round(p.Y / grid) * grid - oy, grid, grid);
        }
        if (!(Dead && Popped)) {
          float alpha = Dead ? (float)Math.Max(0, 1 - deadT / 0.4) : 1f;
          if (Kind == "yarn" && trail.Count > 1) {
            using (var pen = new Pen(Color.FromArgb((int)(255 * alpha), ColorTranslator.FromHtml("#ff9bd0")), 3 * Native.Dpi))
              g.DrawLines(pen, trail.Select(q => new PointF(q.X - ox, q.Y - oy + 8 * Native.Dpi)).ToArray());
          }
          int frame = Kind == "butterfly" ? (int)Math.Floor(t * 8) % 2 : (int)Math.Floor(run / (26 * Native.Dpi)) % 2;
          var img = Frame(Kind, frame);
          var dst = new Rectangle((int)Math.Round(X - R) - ox, (int)Math.Round(Y - R) - oy, (int)(12 * sc), (int)(12 * sc));
          if (alpha >= 1) g.DrawImage(img, dst);
          else using (var ia = new ImageAttributes()) {
            ia.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
            g.DrawImage(img, dst, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
          }
        }
      }
      surf.Present(win.Handle, ox, oy);
    }

    public void Dispose() { win.Close(); win.Dispose(); surf.Dispose(); }
  }
}
