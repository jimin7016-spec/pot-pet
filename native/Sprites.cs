// 도트 스프라이트를 코드로 그리는 파일
// 그림은 전부 픽셀 좌표로 찍어서 만들고, 화면에서는 보간 없이 확대해서 써요.
// 색은 0xAARRGGBB 정수, 0 은 빈칸. 반올림은 .5 를 위로 올려요(R 함수).
using System;
using System.Collections.Generic;
using System.Linq;

namespace PotPet {
  public class Grid {
    public readonly int W, H;
    public readonly int[] D;
    public Grid(int w, int h) { W = w; H = h; D = new int[w * h]; }
    public int this[int x, int y] { get { return D[y * W + x]; } set { D[y * W + x] = value; } }
    public Grid Clone() { var g = new Grid(W, H); Array.Copy(D, g.D, D.Length); return g; }
  }

  public class Skin { public string Name; public int Body, Shade, Light, Rim, RimS; }

  public static class Sprites {
    public const int W = 32, H = 40;
    static readonly int OUT = C("#3a2a33"), SOIL = C("#5b3a29"), SOIL_L = C("#7a5038"), EYE = C("#2b1d2a");

    /* ---------- 픽셀 그리기 도구 ---------- */
    public static int C(string hex) { return unchecked((int)0xff000000) | Convert.ToInt32(hex.Substring(1), 16); }
    public static string Hex(int c) { return "#" + (c & 0xffffff).ToString("x6"); }
    /** JS Math.round: .5 는 위로 */
    public static int R(double v) { return (int)Math.Floor(v + 0.5); }
    static Grid grid() { return new Grid(W, H); }
    static void px(Grid g, double x, double y, int c) { int xi = R(x), yi = R(y); if (xi < 0 || yi < 0 || xi >= g.W || yi >= g.H) return; g[xi, yi] = c; }
    static int get(Grid g, int x, int y) { return x < 0 || y < 0 || x >= g.W || y >= g.H ? 0 : g[x, y]; }
    static void rect(Grid g, int x0, int y0, int x1, int y1, int c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) px(g, x, y, c); }
    static void disc(Grid g, double cx, double cy, double r, int c) {
      for (int y = (int)Math.Floor(cy - r); y <= (int)Math.Ceiling(cy + r); y++)
        for (int x = (int)Math.Floor(cx - r); x <= (int)Math.Ceiling(cx + r); x++)
          if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) px(g, x, y, c);
    }
    static void ell(Grid g, double cx, double cy, double rx, double ry, int c) {
      for (int y = (int)Math.Floor(cy - ry); y <= (int)Math.Ceiling(cy + ry); y++)
        for (int x = (int)Math.Floor(cx - rx); x <= (int)Math.Ceiling(cx + rx); x++) { double a = (x - cx) / rx, b = (y - cy) / ry; if (a * a + b * b <= 1) px(g, x, y, c); }
    }
    static void outline(Grid g, int c) {
      var add = new List<int>();
      for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) {
        if (g[x, y] != 0) continue;
        if (get(g, x - 1, y) != 0 || get(g, x + 1, y) != 0 || get(g, x, y - 1) != 0 || get(g, x, y + 1) != 0) add.Add(y * g.W + x);
      }
      foreach (var i in add) g.D[i] = c;
    }
    public static int Mix(int c1, int c2, double t) {
      int r = 0;
      for (int s = 16; s >= 0; s -= 8) { int a = (c1 >> s) & 255, b = (c2 >> s) & 255; r |= R(a + (b - a) * t) << s; }
      return unchecked((int)0xff000000) | r;
    }
    static void ring(Grid g, double cx, double cy, int n, double dist, double r, params int[] col) {
      for (int i = 0; i < n; i++) { double a = -Math.PI / 2 + (i * 2 * Math.PI) / n; disc(g, cx + Math.Cos(a) * dist, cy + Math.Sin(a) * dist, r, col[i % col.Length]); }
    }

    /* ---------- 화분 (색은 사용자가 고름) ---------- */
    static Skin SK(string name, string body, string shade, string light, string rim, string rimS) {
      return new Skin { Name = name, Body = C(body), Shade = C(shade), Light = C(light), Rim = C(rim), RimS = C(rimS) };
    }
    public static readonly string[] SKIN_IDS = { "terra", "cream", "mint", "sky", "rose", "lilac", "tinky", "dipsy", "laalaa", "po", "sun", "noonoo" };
    public static readonly Dictionary<string, Skin> SKINS = new Dictionary<string, Skin> {
      { "terra", SK("테라코타", "#e58f5b", "#c46b3c", "#f6b88a", "#f0a06a", "#d27e4a") },
      { "cream", SK("크림", "#f3e3c3", "#d6bf95", "#fff6e0", "#fbeed3", "#e3cfa6") },
      { "mint", SK("민트", "#8fd9c2", "#5fb59c", "#c6f3e4", "#a5e6d1", "#79c6ad") },
      { "sky", SK("하늘", "#8fc3f0", "#659fd6", "#c9e4fb", "#a7d0f5", "#7fb0e2") },
      { "rose", SK("분홍", "#f5a3b9", "#d97b97", "#fdd0dc", "#f8b8c9", "#e695ac") },
      { "lilac", SK("라일락", "#b9a3ec", "#8f78cc", "#dcd0fa", "#c8b6f2", "#a48ddc") },
      // 텔레토비 동산 색
      { "tinky", SK("보라돌이", "#9a6ad6", "#7448b3", "#c4a2f2", "#ab7ee2", "#8a5cc8") },
      { "dipsy", SK("뚜비", "#9ccc3c", "#76a522", "#cdec84", "#b0dc56", "#89b82e") },
      { "laalaa", SK("나나", "#ffe14d", "#e0b81f", "#fff3a6", "#ffea7a", "#edc838") },
      { "po", SK("뽀", "#f2545b", "#c93339", "#ff9a9e", "#f6747a", "#dc4248") },
      { "sun", SK("햇님", "#ffb25c", "#e88a2e", "#ffdcae", "#ffc47e", "#f29d44") },
      { "noonoo", SK("누누", "#4a8fe0", "#2f6cbc", "#9cc6f6", "#64a2ea", "#3e7fd0") },
    };

    /* ---------- 꾸미기 아이템 (화분 위에 얹어요. 화분 색과 상관없이 같은 자리) ---------- */
    static readonly Dictionary<string, Action<Grid, int>> ITEM_DRAW = new Dictionary<string, Action<Grid, int>> {
      { "glasses", (g, look) => {
        int F = C("#7a4a2a"), lx = 12 + look, rx = 18 + look;
        foreach (var a in new[] { new[] { lx - 1, lx + 2, lx - 1, lx + 1 }, new[] { rx - 1, rx + 2, rx, rx + 2 } }) {
          rect(g, a[0], 29, a[1], 29, F); rect(g, a[2], 33, a[3], 33, F);
          rect(g, a[0], 30, a[0], 32, F); rect(g, a[1], 30, a[1], 32, F);
        }
        px(g, lx + 3, 31, F); px(g, lx + 4, 31, F);
      } },
      { "shades", (g, look) => {
        int K = C("#2b2b3a"), lx = 12 + look, rx = 18 + look;
        rect(g, lx - 1, 29, lx + 2, 32, K); rect(g, rx - 1, 29, rx + 2, 32, K);
        rect(g, lx + 3, 30, lx + 4, 30, K);
        px(g, lx, 30, C("#7a7a9c")); px(g, rx, 30, C("#7a7a9c")); px(g, lx - 1, 29, C("#3a3a52")); px(g, rx - 1, 29, C("#3a3a52"));
      } },
      { "scarf", (g, look) => {
        int A = C("#ff6b6b"), B = C("#fff3f0");
        for (int x = 8; x <= 23; x++) { px(g, x, 27, (x / 2) % 2 != 0 ? A : B); px(g, x, 28, (x / 2) % 2 != 0 ? B : A); }
        rect(g, 20, 29, 22, 32, A); rect(g, 20, 31, 22, 31, B); px(g, 20, 29, C("#ff9b9b"));
      } },
      { "bowtie", (g, look) => {
        int Wc = C("#5b6ee1"), D = C("#3a46a8");
        rect(g, 12, 27, 14, 29, Wc); rect(g, 17, 27, 19, 29, Wc); rect(g, 15, 27, 16, 29, D);
        px(g, 12, 27, C("#8a9bff")); px(g, 17, 27, C("#8a9bff")); px(g, 12, 29, D); px(g, 19, 29, D);
      } },
      { "bell", (g, look) => {
        rect(g, 8, 27, 23, 27, C("#e8567c"));
        rect(g, 15, 28, 16, 30, C("#ffd23f")); px(g, 15, 28, C("#fff3b0")); px(g, 16, 30, C("#e0a800")); px(g, 15, 29, C("#e0a800"));
      } },
      { "heartglasses", (g, look) => {
        int Rr = C("#ff4d6d"), Lt = C("#ffa3b5");
        foreach (var x0 in new[] { 11 + look, 16 + look }) {
          px(g, x0 + 1, 29, Rr); px(g, x0 + 3, 29, Rr);
          rect(g, x0, 30, x0 + 4, 31, Rr); rect(g, x0 + 1, 32, x0 + 3, 32, Rr); px(g, x0 + 2, 33, Rr);
          px(g, x0 + 1, 30, Lt);
        }
      } },
      { "monocle", (g, look) => {
        int A = C("#e0a800"), rx = 18 + look;
        rect(g, rx - 1, 29, rx + 2, 29, A); rect(g, rx - 1, 33, rx + 2, 33, A);
        rect(g, rx - 2, 30, rx - 2, 32, A); rect(g, rx + 3, 30, rx + 3, 32, A);
        px(g, rx + 1, 30, C("#ffffff"));
        px(g, rx + 3, 34, C("#ffd23f")); px(g, rx + 4, 35, C("#ffd23f")); px(g, rx + 4, 36, C("#ffd23f"));
      } },
      { "mustache", (g, look) => {
        int M = C("#5b3a29"), m = look;
        rect(g, 13 + m, 32, 18 + m, 32, M); rect(g, 12 + m, 33, 14 + m, 33, M); rect(g, 17 + m, 33, 19 + m, 33, M);
        px(g, 11 + m, 32, M); px(g, 20 + m, 32, M); px(g, 15 + m, 32, C("#7a5038"));
      } },
      { "pearl", (g, look) => {
        for (int x = 8; x <= 23; x++) px(g, x, x <= 10 || x >= 21 ? 27 : 28, x % 2 != 0 ? C("#fdf6ec") : C("#e4d6c8"));
        disc(g, 15.5, 29.6, 1.2, C("#fdf6ec")); px(g, 15, 29, C("#ffffff")); px(g, 16, 30, C("#d9c7e8"));
      } },
      { "lei", (g, look) => {
        var Cs = new[] { C("#ff7aa8"), C("#ffd43b"), C("#ffffff"), C("#b98cf0") };
        for (int x = 8; x <= 23; x++) { px(g, x, 27, C("#58b84e")); if (x % 2 == 0) { int c = Cs[(x / 2) % Cs.Length]; px(g, x, 27, c); px(g, x + 1, 28, c); px(g, x, 28, C("#fff3b0")); } }
      } },
      { "medal", (g, look) => {
        int Rr = C("#5b6ee1"), A = C("#ffd23f");
        rect(g, 8, 27, 23, 27, Rr); px(g, 14, 28, Rr); px(g, 17, 28, Rr);
        disc(g, 15.5, 30, 1.7, A); px(g, 15, 29, C("#fff3b0")); px(g, 16, 31, C("#e0a800")); px(g, 16, 30, C("#e0a800"));
      } },
    };
    /* 머리 아이템은 식물 위에 한 번 더 얹어요(잎에 가려지지 않게). 화분 왼쪽 모서리 */
    static readonly Dictionary<string, Action<Grid>> HEAD_DRAW = new Dictionary<string, Action<Grid>> {
      { "ribbon", g => {
        int Rr = C("#ff5a7a"), D = C("#d63a5c"), Lt = C("#ff9bb0");
        rect(g, 4, 19, 6, 22, Rr); rect(g, 9, 19, 11, 22, Rr); rect(g, 7, 20, 8, 21, D);
        rect(g, 4, 19, 6, 19, Lt); rect(g, 9, 19, 11, 19, Lt);
        px(g, 5, 23, Rr); px(g, 5, 24, D); px(g, 10, 23, Rr); px(g, 10, 24, D);
      } },
      { "flowerpin", g => {
        ring(g, 8, 20, 5, 2.6, 1.5, C("#ff9bd0")); disc(g, 8, 20, 1.4, C("#ffe066")); px(g, 8, 19, C("#fff3b0"));
      } },
      { "crown", g => {
        int A = C("#ffd23f"), B = C("#e0a800");
        foreach (var x in new[] { 8, 4, 12 }) { px(g, x, 18, A); px(g, x, 19, A); }
        px(g, 8, 17, C("#fff3b0"));
        rect(g, 4, 20, 12, 22, A); rect(g, 5, 19, 5, 19, A); rect(g, 7, 19, 7, 19, A); rect(g, 9, 19, 9, 19, A); rect(g, 11, 19, 11, 19, A);
        rect(g, 4, 22, 12, 22, B);
        px(g, 8, 21, C("#ff5a7a")); px(g, 6, 21, C("#ffffff")); px(g, 10, 21, C("#7ad7ff"));
      } },
      { "strawhat", g => {
        int S = C("#f2cf72"), D = C("#c9a24a"), Lt = C("#fbe6a6");
        rect(g, 1, 22, 14, 23, S); rect(g, 1, 23, 14, 23, D);
        rect(g, 4, 18, 11, 21, S); rect(g, 5, 17, 10, 17, S);
        rect(g, 4, 20, 11, 21, C("#ff6b6b")); rect(g, 5, 17, 7, 17, Lt); px(g, 4, 18, Lt);
        for (int x = 2; x <= 13; x += 3) px(g, x, 22, D);
      } },
      { "beret", g => {
        ell(g, 8, 20.5, 5, 2.2, C("#d6404f")); rect(g, 4, 22, 12, 22, C("#a82838"));
        px(g, 8, 17, C("#a82838")); px(g, 8, 18, C("#d6404f")); px(g, 5, 19, C("#ff7a86")); px(g, 6, 19, C("#ff7a86"));
      } },
      // 텔레토비 안테나: 화분 왼쪽 모서리에 꽂아요
      { "ant_tinky", g => {
        int Cc = C("#8a5ac8");
        rect(g, 7, 17, 7, 22, Cc);
        rect(g, 4, 12, 10, 12, Cc); px(g, 5, 13, Cc); px(g, 9, 13, Cc); px(g, 6, 14, Cc); px(g, 8, 14, Cc); px(g, 7, 15, Cc); px(g, 7, 16, Cc);
      } },
      { "ant_dipsy", g => {
        int Cc = C("#8cc034");
        rect(g, 7, 13, 8, 22, Cc); rect(g, 7, 12, 8, 12, C("#b0dc56")); px(g, 7, 15, C("#b0dc56"));
      } },
      { "ant_laalaa", g => {
        int Cc = C("#f2cc1f");
        rect(g, 7, 18, 7, 22, Cc);
        var pts = new[] { 7, 17, 6, 16, 6, 15, 7, 14, 8, 14, 9, 15, 9, 16, 8, 17, 8, 16, 5, 14, 5, 13, 6, 12, 7, 12, 8, 12, 9, 12, 10, 13, 10, 14 };
        for (int i = 0; i < pts.Length; i += 2) px(g, pts[i], pts[i + 1], Cc);
      } },
      { "ant_po", g => {
        int Cc = C("#e8404a");
        rect(g, 7, 19, 7, 22, Cc);
        for (int a = 0; a < 20; a++) { double t = (a / 20.0) * Math.PI * 2; px(g, 7 + Math.Cos(t) * 2.4, 16 + Math.Sin(t) * 2.4, Cc); }
      } },
      { "wreath", g => {
        var Cs = new[] { C("#ff7aa8"), C("#ffd43b"), C("#ffffff"), C("#b98cf0"), C("#ff9b5a") };
        for (int x = 7; x <= 24; x++) { px(g, x, 22, C("#4fa84a")); px(g, x, 21, x % 3 != 0 ? C("#6fcf72") : C("#4fa84a")); }
        for (int i = 0; i < 6; i++) { int x = 8 + i * 3; ring(g, x, 21, 4, 1, 0.6, Cs[i % Cs.Length]); px(g, x, 21, C("#fff3b0")); }
      } },
    };
    static void drawItems(Grid g, string items, int look) {
      foreach (var id in (items ?? "").Split(',')) { Action<Grid, int> f; if (id.Length > 0 && ITEM_DRAW.TryGetValue(id, out f)) f(g, look); }
    }

    /* ---------- 장난감 ---------- */
    static Grid buildToy(string kind, int frame) {
      var g = new Grid(12, 12);
      if (kind == "ball") {
        disc(g, 5.5, 5.5, 5, C("#ff6b6b"));
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) if (get(g, x, y) != 0 && Math.Abs((frame != 0 ? x + y : x - y + 11) - 11) <= 1) px(g, x, y, C("#fff6f0"));
        px(g, 3, 3, C("#ffb3b3")); px(g, 4, 2, C("#ffb3b3"));
      } else if (kind == "yarn") {
        disc(g, 5.5, 5.5, 5, C("#ff9bd0"));
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) if (get(g, x, y) != 0 && (frame != 0 ? x + y : x - y + 12) % 4 == 0) px(g, x, y, C("#ff6fb8"));
        px(g, 3, 3, C("#ffd0ea")); px(g, 4, 2, C("#ffd0ea")); px(g, 10, 10, C("#ff6fb8")); px(g, 11, 11, C("#ff6fb8"));
      } else if (kind == "butterfly") {
        bool open = frame == 0; double rx = open ? 3.2 : 1.6, ry = open ? 3.4 : 3.8;
        ell(g, 6 - rx - 0.5, 4.5, rx, ry, C("#ffa94d")); ell(g, 6 + rx + 0.5, 4.5, rx, ry, C("#ffa94d"));
        ell(g, 6 - rx, 8, rx * 0.7, 2, C("#ffd166")); ell(g, 6 + rx + 1, 8, rx * 0.7, 2, C("#ffd166"));
        if (open) { px(g, 2, 4, C("#ffffff")); px(g, 9, 4, C("#ffffff")); }
        rect(g, 5, 3, 6, 9, C("#6b4a3a")); px(g, 4, 2, C("#6b4a3a")); px(g, 7, 2, C("#6b4a3a"));
      } else {
        for (int a = 0; a < 40; a++) { double t = (a / 40.0) * Math.PI * 2; px(g, 5.5 + Math.Cos(t) * 5, 5.5 + Math.Sin(t) * 5, C("#8fd0ff")); }
        px(g, 3, 3, C("#ffffff")); px(g, 3, 4, C("#ffffff")); px(g, 4, 2, C("#ffffff")); px(g, 7, 8, C("#c9ecff"));
        return g;
      }
      outline(g, OUT);
      return g;
    }

    /** face: idle | blink | happy | love | sad | sleep,  look: -1/0/1,  feet: 0 서있음 / 1 왼발 / 2 오른발 */
    static Grid buildPot(string skin, string face, int look, int feet, string items) {
      Skin s; if (!SKINS.TryGetValue(skin ?? "", out s)) s = SKINS["terra"];
      var g = grid();
      rect(g, 11, 37, 13, feet == 1 ? 37 : 38, s.Shade);
      rect(g, 18, 37, 20, feet == 2 ? 37 : 38, s.Shade);
      for (int y = 27; y <= 36; y++) {
        int l = y <= 29 ? 8 : y <= 32 ? 9 : 10, r = 31 - l;
        rect(g, l, y, r, y, s.Body); px(g, r, y, s.Shade); px(g, r - 1, y, s.Shade);
        if (y >= 28 && y <= 31) px(g, l + 1, y, s.Light);
        if (y == 27 || y == 36) rect(g, l, y, r, y, s.Shade);
      }
      rect(g, 7, 24, 24, 25, s.Rim); rect(g, 7, 26, 24, 26, s.RimS);
      px(g, 7, 23, s.Rim); px(g, 24, 23, s.Rim); rect(g, 9, 24, 11, 24, s.Light);
      rect(g, 8, 23, 23, 23, SOIL); px(g, 10, 23, SOIL_L); px(g, 14, 23, SOIL_L); px(g, 19, 23, SOIL_L);
      int lx = 12 + look, rx = 18 + look, m = look, W_ = C("#ffffff");
      if (face == "blink" || face == "sleep") { rect(g, lx, 31, lx + 1, 31, EYE); rect(g, rx, 31, rx + 1, 31, EYE); }
      else if (face == "happy" || face == "love") { foreach (var bx in new[] { 12 + look, 17 + look }) { px(g, bx, 31, EYE); px(g, bx + 1, 30, EYE); px(g, bx + 2, 31, EYE); } }
      else if (face == "sad") { px(g, lx, 31, EYE); px(g, lx + 1, 32, EYE); px(g, rx, 32, EYE); px(g, rx + 1, 31, EYE); }
      else { rect(g, lx, 30, lx + 1, 32, EYE); rect(g, rx, 30, rx + 1, 32, EYE); px(g, lx, 30, W_); px(g, rx, 30, W_); }
      if (face == "happy" || face == "love") { rect(g, 14 + m, 33, 17 + m, 33, EYE); rect(g, 15 + m, 34, 16 + m, 34, C("#e85a72")); }
      else if (face == "sad") { px(g, 14 + m, 34, EYE); px(g, 15 + m, 33, EYE); px(g, 16 + m, 33, EYE); px(g, 17 + m, 34, EYE); px(g, lx, 33, C("#6ec6ff")); px(g, lx, 34, C("#6ec6ff")); }
      else if (face == "sleep") { rect(g, 15 + m, 34, 16 + m, 34, EYE); }
      else { px(g, 14 + m, 33, EYE); px(g, 15 + m, 34, EYE); px(g, 16 + m, 34, EYE); px(g, 17 + m, 33, EYE); }
      int b = face == "love" ? C("#ff5f86") : face == "happy" ? C("#ff7f9c") : C("#ff9bb0");
      foreach (var x in new[] { 10, 11, 20, 21 }) px(g, x, 33, b);
      if (face == "love") { px(g, 9, 33, b); px(g, 22, 33, b); }
      drawItems(g, items, look);
      outline(g, OUT);
      return g;
    }

    /* ---------- 식물 ---------- */
    static readonly int G_lf = C("#58b84e"), G_lfL = C("#8fe07a"), G_s0 = C("#79d36a"), G_s1 = C("#4da84a");
    static void stem(Grid g, int top) { stem(g, top, G_s0, G_s1); }
    static void stem(Grid g, int top, int c0, int c1) { for (int y = top; y <= 22; y++) { px(g, 15, y, c0); px(g, 16, y, c1); } }
    static void leaf(Grid g, double x, double y, double rx, double ry) { leaf(g, x, y, rx, ry, G_lf, G_lfL); }
    static void leaf(Grid g, double x, double y, double rx, double ry, int c, int l) { ell(g, x, y, rx, ry, c); px(g, x - rx * 0.5, y - ry * 0.6, l); px(g, x - rx * 0.1, y - ry * 0.6, l); }
    static void leaves(Grid g, double y, double rx, double ry, double dx) { leaves(g, y, rx, ry, dx, G_lf, G_lfL); }
    static void leaves(Grid g, double y, double rx, double ry, double dx, int c, int l) { leaf(g, 15.5 - dx, y, rx, ry, c, l); leaf(g, 15.5 + dx, y, rx, ry, c, l); }

    static void seed(Grid g) {
      rect(g, 13, 22, 18, 22, SOIL); rect(g, 14, 21, 17, 21, SOIL); px(g, 15, 21, SOIL_L); px(g, 17, 22, SOIL_L);
      px(g, 15, 19, C("#d9b77e")); px(g, 16, 19, C("#c39a5e")); px(g, 15, 20, C("#c39a5e")); px(g, 16, 20, C("#d9b77e"));
    }
    // 1~2단계는 모든 식물이 비슷해서 정체를 몰라요. 잎 색만 살짝 달라서 눈치 빠르면 맞힐 수 있어요.
    static void generic(Grid g, int stage, int tint) {
      int c = Mix(G_lf, tint, 0.45), l = Mix(G_lfL, tint, 0.45);
      if (stage == 1) { stem(g, 19); leaves(g, 18, 2.2, 1.2, 2.6, c, l); }
      else { stem(g, 13); leaves(g, 19, 3.2, 1.5, 3.6, c, l); leaves(g, 15, 2.6, 1.3, 3.0, c, l); leaves(g, 12, 1.8, 1.0, 2.0, c, l); }
    }

    static void tomato(Grid g, int stage, bool gold) {
      stem(g, 9);
      leaf(g, 11, 19, 3.2, 1.6); leaf(g, 20.5, 17, 3.2, 1.6); leaf(g, 11.5, 12.5, 2.8, 1.4); leaf(g, 20, 10.5, 2.6, 1.3); leaf(g, 15.5, 8, 2, 2.2);
      bool ripe = stage >= 4;
      int c = ripe ? (gold ? C("#ffc82e") : C("#ef4a3d")) : C("#97d96c"), hl = ripe ? (gold ? C("#fff2a8") : C("#ffa79e")) : C("#cdf2b0");
      foreach (var p in new[] { new[] { 9.5, 16 }, new[] { 21.5, 14 }, new[] { 14, 20.5 } }) {
        double x = p[0], y = p[1];
        disc(g, x, y, 2.3, c); px(g, x - 1, y - 1, hl); px(g, x - 1, y - 3, C("#3f9b44")); px(g, x + 1, y - 3, C("#3f9b44")); px(g, x, y - 3, C("#3f9b44"));
      }
      if (stage == 3) { px(g, 18, 7, C("#ffe45c")); px(g, 19, 7, C("#ffe45c")); px(g, 13, 6, C("#ffe45c")); }
    }
    static void basil(Grid g, int stage, bool purple) {
      int A = purple ? C("#7b4aa8") : C("#3f9f4a"), B = purple ? C("#a374d0") : C("#6fcf72");
      stem(g, 10, purple ? C("#8b66b0") : C("#6bbf5e"), purple ? C("#6a4890") : C("#3f8f45"));
      var tiers = new[] { new[] { 19, 3.6, 1.9, 4.2 }, new[] { 15, 3.3, 1.8, 3.7 }, new[] { 11.5, 2.7, 1.6, 3.0 } };
      foreach (var t in tiers.Take(stage >= 4 ? 3 : 2)) leaves(g, t[0], t[1], t[2], t[3], A, B);
      leaf(g, 15.5, stage >= 4 ? 8 : 12, 2.2, 2.6, A, B);
      if (stage >= 4) { foreach (var p in new[] { new[] { 15, 4 }, new[] { 16, 3 }, new[] { 15, 2 }, new[] { 16, 5 }, new[] { 14, 5 }, new[] { 17, 4 } }) px(g, p[0], p[1], C("#ffffff")); px(g, 15, 3, C("#ffe9a8")); }
    }
    static void cactusBody(Grid g, int x0, int x1, int top, int bot) {
      int B = C("#6cc36a"), BD = C("#4a9f52"), BL = C("#93dc8a");
      rect(g, x0, top + 1, x1, bot, B); rect(g, x0 + 1, top, x1 - 1, top, B); rect(g, x1 - 1, top + 1, x1, bot, BD); px(g, x1 - 1, top, BD);
      for (int y = top + 2; y <= bot - 1; y += 3) px(g, x0 + 1, y, BL);
    }
    static void cactus(Grid g, int stage) {
      cactusBody(g, 13, 18, 8, 22); rect(g, 11, 16, 13, 17, C("#6cc36a")); cactusBody(g, 10, 11, 12, 17); rect(g, 18, 14, 20, 15, C("#6cc36a")); cactusBody(g, 20, 21, 10, 15);
      foreach (var p in new[] { new[] { 14, 11 }, new[] { 16, 14 }, new[] { 15, 18 }, new[] { 16, 20 }, new[] { 10, 14 }, new[] { 21, 12 } }) px(g, p[0], p[1], C("#e6f8de"));
      if (stage == 3) ell(g, 15.5, 6.3, 1.3, 1.7, C("#ff8fb1")); else { ring(g, 15.5, 5.5, 6, 2.6, 1.4, C("#ff7aa8")); disc(g, 15.5, 5.5, 1.3, C("#ffe066")); }
    }
    static void sunflower(Grid g, int stage) {
      stem(g, 10); leaves(g, 19, 3.4, 1.6, 3.7); leaves(g, 15, 3.0, 1.4, 3.3);
      if (stage == 3) { ell(g, 15.5, 7, 2.4, 3.2, G_lf); ell(g, 15.5, 5.7, 1.9, 2.0, C("#ffd43b")); px(g, 15, 4, C("#fff2a0")); }
      else {
        ring(g, 15.5, 6.5, 12, 4.4, 1.9, C("#ffd43b")); disc(g, 15.5, 6.5, 3.4, C("#8a5a2f")); disc(g, 15.5, 6.5, 2.2, C("#6b4423"));
        foreach (var d in new[] { new[] { -1, -1 }, new[] { 1, 0 }, new[] { 0, 1 } }) px(g, 15.5 + d[0] + 0.5, 6.5 + d[1], C("#4a2d17"));
      }
    }
    static void lettuce(Grid g, int stage) {
      double Rd = stage >= 4 ? 7.4 : 5.6;
      for (int i = 0; i < 10; i++) { double a = (i / 10.0) * Math.PI * 2; disc(g, 15.5 + Math.Cos(a) * Rd, 18.5 + Math.Sin(a) * Rd * 0.42, Rd * 0.42, C("#4fb04a")); }
      for (int i = 0; i < 7; i++) { double a = (i / 7.0) * Math.PI * 2 + 0.3; disc(g, 15.5 + Math.Cos(a) * Rd * 0.55, 18.2 + Math.Sin(a) * Rd * 0.3, Rd * 0.34, C("#7fd36a")); }
      ell(g, 15.5, 18, Rd * 0.38, Rd * 0.3, C("#b6ec8a")); px(g, 14, 17, C("#e3f9c8")); px(g, 17, 18, C("#e3f9c8"));
    }
    static void mint(Grid g, int stage) {
      stem(g, 9, C("#7fe0a8"), C("#4fb883"));
      foreach (var p in new[] { new[] { 20, 3.2 }, new[] { 17, 3.0 }, new[] { 14, 2.8 }, new[] { 11, 2.4 } }) leaves(g, p[0], 2.4, 1.3, p[1], C("#4fcf88"), C("#b6f5cf"));
      leaf(g, 15.5, 7.5, 1.6, 2.0, C("#4fcf88"), C("#b6f5cf"));
      if (stage >= 4) { disc(g, 15.5, 4.5, 1.3, C("#d9b8ff")); disc(g, 13.5, 5.5, 1.1, C("#e6ccff")); disc(g, 17.5, 5.5, 1.1, C("#e6ccff")); }
    }
    static void strawberry(Grid g, int stage) {
      Action<double, double> tri = (x, y) => { disc(g, x - 2.2, y, 1.9, C("#49a655")); disc(g, x + 2.2, y, 1.9, C("#49a655")); disc(g, x, y - 2, 2.1, C("#5cc268")); px(g, x - 0.5, y - 3, C("#9be58a")); };
      stem(g, 16, C("#7fcf70"), C("#4da84a")); tri(9.5, 18); tri(22, 17); tri(15.5, 13.5);
      foreach (var p in new[] { new double[] { 10, 21 }, new[] { 21.5, 20.5 }, new[] { 15.5, 21 } }) {
        double x = p[0], y = p[1];
        if (stage >= 4) { ell(g, x, y, 2.1, 2.5, C("#ef4a5b")); px(g, x - 1, y - 1, C("#ff9aa5")); px(g, x + 1, y + 1, C("#ffe27a")); px(g, x - 1, y + 1, C("#ffe27a")); px(g, x - 1, y - 3, C("#3f9b44")); px(g, x, y - 3, C("#3f9b44")); px(g, x + 1, y - 3, C("#3f9b44")); }
        else { disc(g, x, y - 1, 1.3, C("#ffffff")); px(g, x, y - 1, C("#ffd84a")); }
      }
    }
    static void lavender(Grid g, int stage) {
      leaves(g, 20, 2.4, 1.0, 3.2, C("#8fb58a"), C("#b6d4ad"));
      foreach (var p in new[] { new[] { 12, 11 }, new[] { 15, 6 }, new[] { 19, 12 } }) {
        int x = p[0], top = p[1];
        for (int y = top + 4; y <= 22; y++) px(g, x, y, C("#8fb58a"));
        int len = stage >= 4 ? 8 : 5;
        for (int k = 0; k < len; k++) { int y = top + k, c = stage >= 4 ? (k % 2 != 0 ? C("#9b6bd8") : C("#c19ef0")) : (k % 2 != 0 ? C("#a99bd0") : C("#b9d1a8")); px(g, x, y, c); if (k > 0) { px(g, x - 1, y, c); px(g, x + 1, y, c); } }
      }
    }

    /* ---------- 친구 (식물 대신 흙에서 나오는 캐릭터) ----------
     * 좌표는 '다 컸을 때' 기준: x 는 가운데에서 좌우(-10~10), y 는 흙에서 위로(0~20).
     * k(0.6~1.0) 만큼 줄여서 그리고, 눈·코 같은 1칸짜리는 크기와 상관없이 그대로 찍어서 작아도 얼굴이 보여요. */
    static readonly int K = C("#2b1d2a"), WH = C("#ffffff");
    class Pen {
      readonly Grid g; readonly double k;
      public Pen(Grid g, double k) { this.g = g; this.k = k; }
      double X(double x) { return 15.5 + x * k; }
      double Y(double y) { return 22 - y * k; }
      public void E(double x, double y, double rx, double ry, int c) { ell(g, X(x), Y(y), Math.Max(0.6, rx * k), Math.Max(0.6, ry * k), c); }
      public void P(double x, double y, int c) { px(g, X(x), Y(y), c); }
    }
    static readonly Dictionary<string, Func<Pen, double>> FRIENDS = new Dictionary<string, Func<Pen, double>> {
      { "whitepup", f => {
        f.E(0, 3.5, 5.5, 4, C("#f2f2f8")); f.E(-3, 0.8, 1.6, 1, WH); f.E(3, 0.8, 1.6, 1, WH);
        f.E(0, 11.6, 6.6, 5.4, C("#dcdce8")); f.E(0, 12, 6.5, 5.4, WH);
        f.E(-6, 13, 1.8, 2.8, C("#e4e4ee")); f.E(6, 13, 1.8, 2.8, C("#e4e4ee"));
        f.P(-2.5, 12.5, K); f.P(2.5, 12.5, K); f.P(0, 10.5, K); f.P(-0.8, 9.4, C("#9a8a9a")); f.P(0.8, 9.4, C("#9a8a9a"));
        return 17.5;
      } },
      { "hamster", f => {
        f.E(-5, 16.5, 2.2, 2.2, C("#b07440")); f.E(5, 16.5, 2.2, 2.2, C("#b07440")); f.E(-5, 16.5, 1, 1, C("#ffb3c1")); f.E(5, 16.5, 1, 1, C("#ffb3c1"));
        f.E(0, 8.5, 7.5, 8.5, C("#f3c27f")); f.E(0, 6.5, 5, 5, C("#fff3e0"));
        f.E(-4.6, 6.8, 1.6, 1.4, C("#ffd9b0")); f.E(4.6, 6.8, 1.6, 1.4, C("#ffd9b0"));
        f.P(-2.5, 11, K); f.P(2.5, 11, K); f.P(0, 9, C("#e8607a")); f.P(-4, 8.5, C("#ff9bb0")); f.P(4, 8.5, C("#ff9bb0"));
        f.P(-0.5, 7.5, C("#c08a5a")); f.P(0.5, 7.5, C("#c08a5a"));
        return 17;
      } },
      { "beaver", f => {
        f.E(-5.2, 16.8, 2, 2, C("#f27f9c")); f.E(5.2, 16.8, 2, 2, C("#f27f9c")); f.E(-5.2, 16.8, 0.9, 0.9, C("#d95c7c")); f.E(5.2, 16.8, 0.9, 0.9, C("#d95c7c"));
        f.E(0, 3.5, 5.2, 4, C("#f58fa8")); f.E(0, 3.2, 3.2, 3, C("#ffd6e0"));
        f.E(0, 12, 6.8, 5.8, C("#f58fa8")); f.E(0, 10, 3.2, 2.2, C("#ffd6e0"));
        f.E(-5.4, 4.5, 1.4, 1.4, C("#ff4d6d")); f.E(5.4, 4.5, 1.4, 1.4, C("#ff4d6d"));
        f.P(-3, 13.5, K); f.P(3, 13.5, K); f.P(-3, 14.2, WH); f.P(3, 14.2, WH);
        f.P(0, 11.5, C("#7a2a3a")); f.P(-0.6, 8.6, WH); f.P(0.6, 8.6, WH); f.P(-4.6, 11, C("#ff6f8f")); f.P(4.6, 11, C("#ff6f8f"));
        return 18;
      } },
      // 오리 둘: 커다란 동그란 머리, 작은 몸, 주황 부리와 발
      { "jjidung", f => {
        int O = C("#ffb03a");
        f.E(-2.6, 0.6, 1.9, 0.9, O); f.E(2.6, 0.6, 1.9, 0.9, O);
        f.E(0, 4.2, 5, 4, C("#8a5a44")); f.E(-5.2, 4.6, 1.1, 2.2, C("#7a4a36")); f.E(5.2, 4.6, 1.1, 2.2, C("#7a4a36"));
        f.E(0, 12.4, 7.4, 5.6, C("#2f7a4a")); f.E(-3.5, 15, 1.6, 1, C("#4a9a64"));
        f.E(0, 7.4, 5, 0.6, WH);
        f.P(-2.6, 13, K); f.P(2.6, 13, K); f.E(0, 11.2, 1.9, 0.9, O); f.P(1, 11.4, C("#e08a20"));
        return 18;
      } },
      { "jjio", f => {
        int O = C("#ffb03a");
        f.E(-2.6, 0.6, 1.9, 0.9, O); f.E(2.6, 0.6, 1.9, 0.9, O);
        f.E(0, 4.2, 5, 4, C("#f4f4f8")); f.E(-5.2, 4.6, 1.1, 2.2, C("#e6e6ee")); f.E(5.2, 4.6, 1.1, 2.2, C("#e6e6ee"));
        f.E(0, 12, 7.5, 5.8, C("#dcdce6")); f.E(0, 12.4, 7.4, 5.6, WH);
        f.E(0.6, 18.4, 0.8, 1.4, WH); f.P(1.4, 19.4, WH);
        f.P(-2.6, 13, K); f.P(2.6, 13, K); f.E(0, 11.2, 1.9, 0.9, O); f.P(1, 11.4, C("#e08a20"));
        return 19.5;
      } },
      { "beagle", f => {
        f.E(0, 3.5, 5, 4, WH); f.E(-3, 0.8, 1.6, 1, WH); f.E(3, 0.8, 1.6, 1, WH);
        f.E(0, 6.8, 4.4, 0.7, C("#e8303c"));
        f.E(0, 12.5, 5.6, 5.4, WH); f.E(-5.8, 11.5, 1.8, 4.2, K); f.E(5.8, 11.5, 1.8, 4.2, K);
        f.E(0, 9.6, 3.4, 2.2, WH); f.E(0, 10.6, 1.4, 1, K);
        f.P(-2.2, 13.6, K); f.P(2.2, 13.6, K); f.P(0, 8.4, C("#9a8a9a"));
        return 18;
      } },
      { "blob", f => {
        f.E(0, 5, 9, 5.5, C("#b39ae0")); f.E(-4, 10, 3.6, 2.8, C("#b39ae0")); f.E(3.5, 10.5, 4, 3, C("#b39ae0"));
        f.E(0, 1.5, 8.4, 2, C("#9a7cc8")); f.E(-5, 8, 1.6, 1.2, C("#d6c6f2")); f.E(-4.5, 11, 1.4, 0.9, C("#d6c6f2"));
        f.P(-2, 7.5, K); f.P(2, 7.5, K);
        f.P(-1.5, 5.5, K); f.P(-0.5, 5, K); f.P(0.5, 5, K); f.P(1.5, 5.5, K);
        return 13;
      } },
    };
    /** 친구 머리 위 새싹 (식물에서 나왔다는 표시). 다 크면 꽃이 펴요 */
    static void friendSprout(Grid g, double topY, bool bloom) {
      int y = R(topY);
      px(g, 15, y, G_s0); px(g, 15, y - 1, G_s0); px(g, 14, y - 2, G_lf); px(g, 13, y - 2, G_lfL); px(g, 16, y - 2, G_lf); px(g, 17, y - 3, G_lfL);
      if (bloom) { px(g, 15, y - 3, C("#ffe066")); px(g, 15, y - 4, C("#ff9bd0")); px(g, 14, y - 3, C("#ff9bd0")); }
    }
    static void friend(Grid g, string id, int stage, int size) {
      double k = 0.6 + 0.4 * (stage >= 4 ? 1 : size / 4.0);
      double top = FRIENDS[id](new Pen(g, k));
      friendSprout(g, 22 - top * k - 1, stage >= 4);
      for (int y = 23; y < g.H; y++) for (int x = 0; x < g.W; x++) g[x, y] = 0; // 흙 아래는 잘라서 화분에 쏙 들어가 앉은 모습
    }
    /** 쑥쑥 단계의 친구 씨앗: 맨 위 잎 대신 동그란 봉오리가 맺혀요 (무엇인지는 아직 비밀) */
    static void friendBud(Grid g, int tint) {
      generic(g, 2, tint);
      disc(g, 15.5, 10.5, 2.2, C("#ffe0b0")); px(g, 14, 9, C("#fff6e0")); px(g, 17, 12, C("#f2c48a"));
    }
    /** 친구가 시들면: 축 처져서 흙 속으로 가라앉고 색이 바래요 */
    static Grid friendWilt(Grid src) {
      var g = new Grid(src.W, src.H); int to = C("#a59a5c");
      for (int y = 0; y < src.H; y++) for (int x = 0; x < src.W; x++) { int c = src[x, y]; if (c != 0 && y + 2 <= 22) px(g, x, y + 2, Mix(c, to, 0.35)); }
      return g;
    }

    class Sp { public int Tint; public bool Friend; public Action<Grid, int> Draw; }
    static Sp Plt(string tint, Action<Grid, int> draw) { return new Sp { Tint = C(tint), Draw = draw }; }
    static Sp Frd(string tint) { return new Sp { Tint = C(tint), Friend = true }; }
    static readonly Dictionary<string, Sp> SPECIES = new Dictionary<string, Sp> {
      { "tomato", Plt("#7fd05a", (g, s) => tomato(g, s, false)) },
      { "basil", Plt("#2f8f4a", (g, s) => basil(g, s, false)) },
      { "cactus", Plt("#4aa89a", cactus) },
      { "sunflower", Plt("#a6c94a", sunflower) },
      { "lettuce", Plt("#b4e36a", lettuce) },
      { "mint", Plt("#5fdca0", mint) },
      { "strawberry", Plt("#4fa860", strawberry) },
      { "lavender", Plt("#8fb58a", lavender) },
      { "purplebasil", Plt("#8a5ab0", (g, s) => basil(g, s, true)) },
      { "goldtomato", Plt("#a8d04a", (g, s) => tomato(g, s, true)) },
      { "whitepup", Frd("#9fd08a") },
      { "hamster", Frd("#c4c46a") },
      { "beaver", Frd("#d0a08a") },
      { "jjidung", Frd("#5a9a6a") },
      { "jjio", Frd("#a8d4b8") },
      { "beagle", Frd("#8ac08a") },
      { "blob", Frd("#a89ad0") },
    };
    public static IEnumerable<string> SpeciesIds { get { return SPECIES.Keys; } }
    public static bool IsFriend(string id) { Sp s; return SPECIES.TryGetValue(id ?? "", out s) && s.Friend; }

    /** 시든 모습: 줄기가 오른쪽으로 휘어 처지고 색이 누렇게 바램 */
    static Grid wiltify(Grid src) {
      var g = new Grid(src.W, src.H); int to = C("#a59a5c");
      for (int y = 0; y < src.H; y++) for (int x = 0; x < src.W; x++) {
        int c = src[x, y]; if (c == 0) continue;
        int h = 22 - y;
        if (h <= 0) px(g, x, y, Mix(c, to, 0.5)); else px(g, x + R((h * h) / 26.0), y + R((h * h) / 80.0), Mix(c, to, 0.5));
      }
      return g;
    }
    static Grid buildPlant(string id, int stage, bool wilt, int size) {
      Sp sp; if (!SPECIES.TryGetValue(id ?? "", out sp)) { sp = SPECIES["tomato"]; id = "tomato"; }
      var g = grid();
      if (stage <= 0) seed(g);
      else if (sp.Friend && stage == 2) friendBud(g, sp.Tint);
      else if (stage <= 2) generic(g, stage, sp.Tint);
      else if (sp.Friend) friend(g, id, stage, size);
      else sp.Draw(g, stage);
      if (wilt && stage >= 1) g = sp.Friend && stage >= 3 ? friendWilt(g) : wiltify(g);
      outline(g, OUT);
      return g;
    }
    static Grid buildCan() {
      var g = new Grid(17, 12); int B = C("#5aa9f0"), D = C("#3d84c6");
      rect(g, 7, 2, 14, 8, B); rect(g, 7, 8, 14, 8, D); rect(g, 7, 2, 14, 2, C("#a3d4ff")); rect(g, 13, 3, 14, 7, D);
      foreach (var p in new[] { new[] { 6, 5 }, new[] { 5, 6 }, new[] { 4, 7 }, new[] { 3, 8 }, new[] { 6, 4 }, new[] { 5, 5 }, new[] { 4, 6 }, new[] { 3, 7 } }) px(g, p[0], p[1], B);
      rect(g, 15, 3, 16, 3, D); rect(g, 16, 4, 16, 7, D); px(g, 15, 8, D);
      outline(g, OUT);
      return g;
    }

    /* ---------- 캐시 ---------- */
    static readonly Dictionary<string, Grid> cache = new Dictionary<string, Grid>();
    static Grid memo(string k, Func<Grid> fn) { Grid g; if (!cache.TryGetValue(k, out g)) { g = fn(); cache[k] = g; } return g; }
    public static Grid PotGrid(string skin, string face, int look, int feet, string items) {
      return memo("pot|" + skin + "|" + face + "|" + look + "|" + feet + "|" + (items ?? ""), () => buildPot(skin, face, look, feet, items));
    }
    /** size: 친구 몸 크기(0~4). 식물은 무시해요 */
    public static Grid PlantGrid(string id, int stage, bool wilt, int size) {
      int sz = IsFriend(id) && stage == 3 ? size : 0;
      return memo("pl|" + id + "|" + stage + "|" + (wilt ? 1 : 0) + "|" + sz, () => buildPlant(id, stage, wilt, sz));
    }
    public static Grid CanGrid() { return memo("can", buildCan); }
    /** 도감에서 아직 못 만난 식물의 검은 그림자 (모양만, 색은 그릴 때 한 색으로) */
    public static Grid SilhouetteGrid(string id) {
      return memo("silu|" + id, () => {
        var a = buildPlant(id, 4, false, 0); var p = buildPot("terra", "idle", 0, 0, "");
        for (int i = 0; i < p.D.Length; i++) if (p.D[i] != 0 && a.D[i] == 0) a.D[i] = p.D[i];
        return a;
      });
    }
    public static Grid HeadGrid(string id) {
      return memo("head|" + id, () => { var g = grid(); Action<Grid> f; if (HEAD_DRAW.TryGetValue(id ?? "", out f)) f(g); outline(g, OUT); return g; });
    }
    public static Grid ToyGrid(string kind, int frame) { return memo("toy|" + kind + "|" + frame, () => buildToy(kind, frame)); }
    public static IEnumerable<string> ItemIds { get { return ITEM_DRAW.Keys.Concat(HEAD_DRAW.Keys); } }
  }
}
