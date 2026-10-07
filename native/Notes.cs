// 스티커 메모 창 (상태 / 도감 / 옷장)
// 화분 상태가 바뀔 때만 글자·게이지를 고치고, 도감·옷장 카드는 내용이 달라졌을 때만 다시 만들어요.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace PotPet {
  static class Theme {
    public static readonly Color Paper = Hex("#fff3a6"), Edge = Hex("#d9c453"), Bar = Hex("#f2e07a"), Ink = Hex("#3d3520"), Sub = Hex("#6b5f33"),
      Btn = Hex("#ffe45c"), BtnHover = Hex("#ffeb85"), BtnEdge = Hex("#b8a42e"), Track = Color.FromArgb(36, 0, 0, 0), Water = Hex("#2f86d6"),
      Grow = Hex("#2f8f55"), Love = Hex("#e8567c"), Low = Hex("#d6402d"), Danger = Hex("#8a3a2a"), DangerEdge = Hex("#c98f7f"), Locked = Hex("#8d8350"),
      Silhouette = Hex("#3a4a41"), White45 = Color.FromArgb(115, 255, 255, 255), White60 = Color.FromArgb(153, 255, 255, 255);
    public static Color Hex(string h) { return ColorTranslator.FromHtml(h); }
    public static readonly Font Small = Native.Font(12), Bold = Native.Font(12, FontStyle.Bold), Big = Native.Font(16), Tiny = Native.Font(11);
    public static int D(double v) { return Native.D(v); }
    public static void Hq(Graphics g) { g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit; }

    /** 띄어쓰기 단위 줄바꿈 (한 단어가 너무 길면 글자 단위로) */
    public static List<string> Wrap(Graphics g, string text, Font f, int maxW) {
      var lines = new List<string>(); var fmt = StringFormat.GenericTypographic;
      foreach (var para in (text ?? "").Split('\n')) {
        string cur = "";
        foreach (var word in para.Split(' ')) {
          string next = cur.Length == 0 ? word : cur + " " + word;
          if (g.MeasureString(next, f, 100000, fmt).Width <= maxW || cur.Length == 0 && word.Length <= 1) { cur = next; continue; }
          if (cur.Length > 0) { lines.Add(cur); cur = ""; }
          foreach (char ch in word) { // 긴 단어는 글자 단위로
            if (cur.Length > 0 && g.MeasureString(cur + ch, f, 100000, fmt).Width > maxW) { lines.Add(cur); cur = ""; }
            cur += ch;
          }
        }
        lines.Add(cur);
      }
      return lines;
    }
    static readonly Bitmap measureBmp = new Bitmap(1, 1);
    public static Graphics Measure() { var g = Graphics.FromImage(measureBmp); Hq(g); return g; }

    /** 도트 그림 여러 겹을 하나의 작은 비트맵으로 (flat 이면 한 색 그림자) */
    public static Bitmap Compose(Color? flat, params Grid[] layers) {
      var b = new Bitmap(layers[0].W, layers[0].H, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
      foreach (var g in layers) if (g != null) for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) if (g[x, y] != 0) b.SetPixel(x, y, flat ?? Color.FromArgb(g[x, y]));
      return b;
    }
    public static void DrawPixels(Graphics g, Image img, Rectangle r) {
      var im = g.InterpolationMode; var po = g.PixelOffsetMode;
      g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
      g.DrawImage(img, r);
      g.InterpolationMode = im; g.PixelOffsetMode = po;
    }
  }

  /** 메모 안에서 위에서 아래로 쌓이는 칸. 주어진 폭에서 필요한 높이를 알려줘요 */
  abstract class Block : Control {
    protected Block() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    public abstract int HeightFor(int w);
    /** 접어 두면 자리도 차지하지 않아요 */
    public bool Collapsed;
  }

  class TextBlock : Block {
    string text = ""; Font font; Color color; Color? bg; int padX, padY; float lineK;
    public TextBlock(Font f, Color c, Color? bg = null, int padX = 0, int padY = 0, float lineK = 1.5f) { font = f; color = c; this.bg = bg; this.padX = padX; this.padY = padY; this.lineK = lineK; }
    public string Value { get { return text; } set { value = value ?? ""; if (value == text) return; text = value; Invalidate(); } }
    public Color Color_ { set { if (value != color) { color = value; Invalidate(); } } }
    int LineH { get { return (int)Math.Ceiling(font.Size * lineK); } }
    public override int HeightFor(int w) {
      if (text.Length == 0 && bg.HasValue) return 0;
      using (var g = Theme.Measure()) return Math.Max(1, Theme.Wrap(g, text, font, w - padX * 2).Count) * LineH + padY * 2;
    }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics; Theme.Hq(g);
      if (bg.HasValue) using (var b = new SolidBrush(bg.Value)) g.FillRectangle(b, ClientRectangle);
      var lines = Theme.Wrap(g, text, font, Width - padX * 2);
      using (var b = new SolidBrush(color)) for (int i = 0; i < lines.Count; i++) g.DrawString(lines[i], font, b, padX, padY + i * LineH + (LineH - font.Size) / 2f, StringFormat.GenericTypographic);
    }
  }

  class Gauge : Block {
    string label; double value; Color fill;
    public Gauge(string label, Color fill) { this.label = label; this.fill = fill; }
    public void Set(double v, Color c) { if (Math.Abs(v - value) < 0.05 && c == fill) return; value = v; fill = c; Invalidate(); }
    public override int HeightFor(int w) { return Theme.D(18); }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics; Theme.Hq(g);
      using (var b = new SolidBrush(Theme.Sub)) g.DrawString(label, Theme.Small, b, 0, (Height - Theme.Small.Size) / 2f, StringFormat.GenericTypographic);
      int x = Theme.D(42), h = Theme.D(10), y = (Height - h) / 2, w = Width - x;
      using (var b = new SolidBrush(Theme.Track)) g.FillRectangle(b, x, y, w, h);
      using (var b = new SolidBrush(fill)) g.FillRectangle(b, x, y, (int)(w * Logic.Clamp(value, 0, 100) / 100), h);
    }
  }

  class NButton : Block {
    string text; public Bitmap Icon; public bool On, Danger; bool hover, down;
    public event Action Clicked;
    public NButton(string text, Action click = null) { this.text = text; Cursor = Cursors.Hand; if (click != null) Clicked += click; }
    public string Label { set { if (value != text) { text = value; Invalidate(); } } }
    public bool Active { set { if (value != Enabled) { Enabled = value; Invalidate(); } } }
    public bool Pressed { set { if (value != On) { On = value; Invalidate(); } } }
    public override int HeightFor(int w) { return Icon != null ? Theme.D(38) : Theme.D(28); }
    public int PreferredWidth { get { using (var g = Theme.Measure()) return (int)Math.Ceiling(g.MeasureString(text ?? "", Theme.Small, 10000, StringFormat.GenericTypographic).Width) + Theme.D(16); } }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) {
      bool fire = down && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Enabled;
      down = false; Invalidate(); base.OnMouseUp(e);
      if (fire && Clicked != null) Clicked();
    }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics; Theme.Hq(g);
      Color bg = Danger ? (hover && Enabled ? Color.FromArgb(31, 214, 64, 45) : Color.Transparent) : On ? Theme.Ink : hover && Enabled ? Theme.BtnHover : Theme.Btn;
      Color edge = Danger ? Theme.DangerEdge : On ? Theme.Ink : Theme.BtnEdge, ink = Danger ? Theme.Danger : On ? Theme.Paper : Theme.Ink;
      if (!Enabled) { bg = Blend(bg, Theme.Paper, 0.55); edge = Blend(edge, Theme.Paper, 0.55); ink = Blend(ink, Theme.Paper, 0.55); }
      var r = new Rectangle(0, 0, Width - 1, Height - 1);
      if (bg.A > 0) using (var b = new SolidBrush(bg)) g.FillRectangle(b, r);
      using (var p = new Pen(edge)) g.DrawRectangle(p, r);
      if (Icon != null) {
        int s = Theme.D(24);
        var dst = new Rectangle((Width - s) / 2, (Height - s) / 2, s, s);
        if (Enabled) Theme.DrawPixels(g, Icon, dst);
        else using (var ia = new System.Drawing.Imaging.ImageAttributes()) {
          ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f });
          g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
          g.DrawImage(Icon, dst, 0, 0, Icon.Width, Icon.Height, GraphicsUnit.Pixel, ia);
        }
      } else using (var b = new SolidBrush(ink)) {
        var sz = g.MeasureString(text, Theme.Small, 10000, StringFormat.GenericTypographic);
        g.DrawString(text, Theme.Small, b, (Width - sz.Width) / 2f, (Height - Theme.Small.Size) / 2f, StringFormat.GenericTypographic);
      }
    }
    static Color Blend(Color a, Color b, double t) {
      if (a.A == 0) return a;
      return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
  }

  /** 가로로 나란히 (weight 0 이면 내용 폭만큼) */
  class Row : Block {
    readonly List<KeyValuePair<Control, float>> items = new List<KeyValuePair<Control, float>>();
    readonly int gap;
    public Row(int gap) { this.gap = gap; }
    public Row Add(Control c, float weight = 1) { items.Add(new KeyValuePair<Control, float>(c, weight)); Controls.Add(c); return this; }
    int Fixed(Control c) { var nb = c as NButton; if (nb != null) return nb.PreferredWidth; var sw = c as Swatch; if (sw != null) return sw.Width; return c.Width; }
    public override int HeightFor(int w) { int h = 0; foreach (var kv in items) { var b = kv.Key as Block; h = Math.Max(h, b != null ? b.HeightFor(w) : kv.Key.Height); } return h; }
    protected override void OnLayout(LayoutEventArgs e) {
      base.OnLayout(e);
      if (items.Count == 0) return;
      float total = items.Sum(i => i.Value); int fixedW = items.Where(i => i.Value == 0).Sum(i => Fixed(i.Key)) + gap * (items.Count - 1);
      int x = 0, free = Math.Max(0, Width - fixedW);
      foreach (var kv in items) {
        int w = kv.Value == 0 ? Fixed(kv.Key) : (int)(free * kv.Value / Math.Max(0.001f, total));
        kv.Key.SetBounds(x, 0, w, Height);
        x += w + gap;
      }
    }
  }

  /** 붙어 있는 버튼 묶음 (1배/2배/3배) */
  class Seg : Block {
    public readonly List<NButton> Buttons = new List<NButton>();
    public Seg Add(NButton b) { Buttons.Add(b); Controls.Add(b); return this; }
    public override int HeightFor(int w) { return Theme.D(28); }
    protected override void OnLayout(LayoutEventArgs e) {
      base.OnLayout(e);
      int n = Buttons.Count; if (n == 0) return;
      for (int i = 0; i < n; i++) { int x0 = Width * i / n, x1 = Width * (i + 1) / n; Buttons[i].SetBounds(x0, 0, x1 - x0 + (i < n - 1 ? 1 : 0), Height); }
    }
  }

  class Swatch : Control {
    readonly Color color; public bool On; public event Action Clicked;
    public Swatch(Color c, string tip) {
      color = c; Size = new Size(Theme.D(26), Theme.D(26)); Cursor = Cursors.Hand; DoubleBuffered = true;
      AccessibleName = tip + " 화분"; new ToolTip().SetToolTip(this, tip);
    }
    public bool Selected { set { if (value != On) { On = value; Invalidate(); } } }
    protected override void OnClick(EventArgs e) { base.OnClick(e); if (Clicked != null) Clicked(); }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics; int b = Theme.D(2);
      g.Clear(Theme.Paper);
      using (var br = new SolidBrush(color)) g.FillRectangle(br, ClientRectangle);
      using (var p = new Pen(On ? Theme.Ink : Color.FromArgb(64, 0, 0, 0), b)) g.DrawRectangle(p, b / 2f, b / 2f, Width - b, Height - b);
    }
  }

  /** 색 버튼처럼 줄바꿈되며 늘어서는 칸 */
  class Wrapper : Block {
    readonly int gap;
    public Wrapper(int gap) { this.gap = gap; }
    public override int HeightFor(int w) { return Place(w, false); }
    int Place(int w, bool apply) {
      int x = 0, y = 0, rowH = 0;
      foreach (Control c in Controls) {
        if (x > 0 && x + c.Width > w) { x = 0; y += rowH + gap; rowH = 0; }
        if (apply) c.Location = new Point(x, y);
        x += c.Width + gap; rowH = Math.Max(rowH, c.Height);
      }
      return y + rowH;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Place(Width, true); }
  }

  /** 도감·옷장 카드 (그림 + 이름 + 설명) */
  class Card : Block {
    Bitmap img; string name, sub, nick; bool locked, worn, button, hover;
    public event Action Clicked;
    public Card(Bitmap img, string name, string sub, string nick, bool locked, bool worn, bool button) {
      this.img = img; this.name = name; this.sub = sub; this.nick = nick; this.locked = locked; this.worn = worn; this.button = button;
      if (button) Cursor = Cursors.Hand;
    }
    int Lines(Graphics g, string s, Font f, int w) { return string.IsNullOrEmpty(s) ? 0 : Theme.Wrap(g, s, f, w).Count; }
    public override int HeightFor(int w) {
      using (var g = Theme.Measure()) {
        int lh = (int)Math.Ceiling(Theme.Small.Size * 1.35);
        return Theme.D(4) + Theme.D(90) + Theme.D(2) + (Lines(g, name, Theme.Bold, w - 4) + Lines(g, sub, Theme.Tiny, w - 4) + Lines(g, nick, Theme.Bold, w - 4)) * lh + Theme.D(4);
      }
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); if (button && Clicked != null) Clicked(); }
    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics; Theme.Hq(g);
      if (worn) using (var b = new SolidBrush(Theme.White60)) g.FillRectangle(b, ClientRectangle);
      else if (button && hover) using (var b = new SolidBrush(Theme.White45)) g.FillRectangle(b, ClientRectangle);
      if (worn) using (var p = new Pen(Theme.Ink, Theme.D(2))) g.DrawRectangle(p, Theme.D(1), Theme.D(1), Width - Theme.D(2), Height - Theme.D(2));
      int iw = Theme.D(72), ih = Theme.D(90), y = Theme.D(4);
      if (img != null) Theme.DrawPixels(g, img, new Rectangle((Width - iw) / 2, y, iw, ih));
      y += ih + Theme.D(2);
      int lh = (int)Math.Ceiling(Theme.Small.Size * 1.35);
      y = DrawCentered(g, name, Theme.Bold, locked ? Theme.Locked : Theme.Ink, y, lh);
      y = DrawCentered(g, sub, Theme.Tiny, Theme.Sub, y, lh);
      DrawCentered(g, nick, Theme.Bold, Theme.Ink, y, lh);
    }
    int DrawCentered(Graphics g, string s, Font f, Color c, int y, int lh) {
      if (string.IsNullOrEmpty(s)) return y;
      using (var b = new SolidBrush(c))
        foreach (var line in Theme.Wrap(g, s, f, Width - 4)) {
          var w = g.MeasureString(line, f, 10000, StringFormat.GenericTypographic).Width;
          g.DrawString(line, f, b, (Width - w) / 2f, y + (lh - f.Size) / 2f, StringFormat.GenericTypographic);
          y += lh;
        }
      return y;
    }
  }

  /** 카드 3줄 격자 */
  class CardGrid : Block {
    public override int HeightFor(int w) { return Place(w, false); }
    int Place(int w, bool apply) {
      int gap = Theme.D(8), cw = (w - gap * 2) / 3, y = 0, i = 0, rowH = 0;
      foreach (Control c in Controls) {
        var b = (Block)c; int h = b.HeightFor(cw);
        if (apply) c.SetBounds((i % 3) * (cw + gap), y, cw, h);
        rowH = Math.Max(rowH, h);
        if (++i % 3 == 0) { y += rowH + gap; rowH = 0; }
      }
      return i % 3 == 0 ? Math.Max(0, y - gap) : y + rowH;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Place(Width, true); }
  }

  class NoteForm : Form {
    public readonly string Kind;
    readonly App app;
    readonly Panel body;
    readonly Panel content;
    readonly List<Control> stack = new List<Control>();
    bool tipBottom;
    static readonly Dictionary<string, string> TITLES = new Dictionary<string, string> { { "status", "화분 상태" }, { "dex", "도감" }, { "closet", "옷장" } };

    public NoteForm(App app, string kind, Size size) {
      this.app = app; Kind = kind;
      FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
      AutoScaleMode = AutoScaleMode.None; BackColor = Theme.Paper; Text = TITLES[kind]; Size = size; DoubleBuffered = true;
      Padding = new Padding(1);
      Icon = app.AppIcon;

      var bar = new Panel { Dock = DockStyle.Top, Height = Theme.D(30), BackColor = Theme.Bar };
      var title = new TextBlock(Theme.Bold, Theme.Ink) { Value = TITLES[kind], Location = new Point(Theme.D(12), Theme.D(8)), Size = new Size(Theme.D(200), Theme.D(20)) };
      var close = new NButton("×", () => app.HideNote(Kind)) { Size = new Size(Theme.D(26), Theme.D(24)) };
      close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
      close.Location = new Point(bar.Width - close.Width - Theme.D(4), Theme.D(3));
      bar.Controls.Add(title); bar.Controls.Add(close);
      bar.Resize += (s, e) => close.Left = bar.Width - close.Width - Theme.D(4);
      MouseEventHandler drag = (s, e) => { if (e.Button == MouseButtons.Left) { Native.ReleaseCapture(); Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero); } };
      bar.MouseDown += drag; title.MouseDown += drag;

      body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Paper };
      content = new Panel { Location = Point.Empty, BackColor = Theme.Paper };
      body.Controls.Add(content);
      body.Resize += (s, e) => Relayout();
      Controls.Add(body); Controls.Add(bar);
      LocationChanged += (s, e) => app.NoteMoved(Kind, Location);
      if (kind == "status") BuildStatus(); else if (kind == "dex") BuildDex(); else BuildCloset();
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= Native.WS_EX_TOOLWINDOW; return cp; } }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using (var p = new Pen(Theme.Edge)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; app.HideNote(Kind); } base.OnFormClosing(e); }

    void Add(Control c) { stack.Add(c); content.Controls.Add(c); }
    void Relayout() {
      int pad = Theme.D(14), top = Theme.D(12), gap = Theme.D(10);
      int w = body.ClientSize.Width - pad * 2;
      int y = top;
      var heights = new List<int>();
      foreach (var c in stack) { var b = c as Block; bool col = b != null && b.Collapsed; c.Visible = !col; int h = col ? 0 : b != null ? b.HeightFor(w) : c.Height; heights.Add(h); y += h + (h > 0 ? gap : 0); }
      int total = y - gap + Theme.D(14);
      if (tipBottom && total < body.ClientSize.Height) { // 상태 메모의 도움말은 맨 아래에
        int extra = body.ClientSize.Height - total;
        total += extra;
        y = top;
        for (int i = 0; i < stack.Count; i++) { if (i == stack.Count - 1) y += extra; stack[i].SetBounds(pad, y, w, heights[i]); y += heights[i] + (heights[i] > 0 ? gap : 0); }
      } else {
        y = top;
        for (int i = 0; i < stack.Count; i++) { stack[i].SetBounds(pad, y, w, heights[i]); y += heights[i] + (heights[i] > 0 ? gap : 0); }
      }
      content.Size = new Size(body.ClientSize.Width, total);
      foreach (var c in stack) c.PerformLayout();
    }
    public override void Refresh() { Relayout(); base.Refresh(); }

    /* ---------- 상태 메모 ---------- */
    string layoutKey = "";
    TextBlock sName, sSub, sTime; TextBox nick; Gauge gWater, gGrow, gLove; NButton bWater, bNutri, bNew, bReplant, bReset;
    readonly Dictionary<string, NButton> toyBtns = new Dictionary<string, NButton>();
    readonly Dictionary<int, NButton> speedBtns = new Dictionary<int, NButton>();
    readonly Dictionary<string, Swatch> swatches = new Dictionary<string, Swatch>();
    static readonly Dictionary<string, string> TOY_NAMES = new Dictionary<string, string> { { "ball", "공" }, { "yarn", "털실" }, { "butterfly", "나비" }, { "bubble", "비눗방울" } };

    void BuildStatus() {
      var g = app.Game;
      sName = new TextBlock(Theme.Big, Theme.Ink, null, 0, 0, 1.4f) { Value = "???" };
      sSub = new TextBlock(Theme.Small, Theme.Sub);
      nick = new TextBox { MaxLength = Logic.NAME_MAX, Font = Theme.Small, BackColor = Color.FromArgb(255, 255, 251, 228), ForeColor = Theme.Ink, BorderStyle = BorderStyle.FixedSingle };
      SetCue(nick, "이름 지어주기");
      nick.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; g.SetName(nick.Text); body.Focus(); } };
      var nickRow = new Row(Theme.D(6)).Add(nick, 1).Add(new NButton("저장", () => g.SetName(nick.Text)), 0);
      gWater = new Gauge("물", Theme.Water); gGrow = new Gauge("성장", Theme.Grow); gLove = new Gauge("애정", Theme.Love);
      sTime = new TextBlock(Theme.Small, Theme.Ink, Theme.White45, Theme.D(8), Theme.D(5), 1.55f);
      bWater = new NButton("물 주기", g.DoWater); bNutri = new NButton("영양제", g.DoNutri);
      var btns = new Row(Theme.D(6)).Add(bWater).Add(bNutri);
      var toyRow = new Row(Theme.D(6));
      foreach (var id in Logic.TOYS) {
        string k = id;
        var b = new NButton("", () => g.DropToy(k)) { Icon = Theme.Compose(null, Sprites.ToyGrid(k, 0)), AccessibleName = TOY_NAMES[k] + " 떨어뜨리기" };
        new ToolTip().SetToolTip(b, TOY_NAMES[k] + " 떨어뜨리기");
        toyBtns[k] = b; toyRow.Add(b);
      }
      var seg = new Seg();
      foreach (var n in Logic.SPEEDS) { int k = n; var b = new NButton(n == 1 ? "1배" : n + "배", () => g.SetSpeed(k)); speedBtns[k] = b; seg.Add(b); }
      bNew = new NButton("새 씨앗 심기", g.NewSeed); bReplant = new NButton("갈아엎기", () => g.ResetGame("plant"));
      new ToolTip().SetToolTip(bReplant, "이 화분을 버리고 새 씨앗을 심어요 (도감·아이템은 그대로)");
      var btns2 = new Row(Theme.D(6)).Add(bNew).Add(bReplant);
      bReset = new NButton("전부 초기화 (도감·아이템 포함)", () => g.ResetGame("all")) { Danger = true };
      var resetRow = new Row(0).Add(bReset, 0);
      var sw = new Wrapper(Theme.D(7));
      foreach (var id in Logic.SKIN_IDS) {
        string k = id; var s = new Swatch(Sprites.SKINS[k].Body.ToColor(), Sprites.SKINS[k].Name); s.Clicked += () => g.SetSkin(k);
        swatches[k] = s; sw.Controls.Add(s);
      }
      var tip = new TextBlock(Theme.Small, Theme.Sub) { Value = "화분을 누른 채 좌우로 문지르면 쓰다듬어요.\nShift를 누른 채 끌면 옮길 수 있어요.\n우클릭하면 메뉴가 나와요." };
      foreach (var c in new Control[] { sName, sSub, nickRow, gWater, gGrow, gLove, sTime, btns, Label("놀아주기 (Ctrl+Alt+T)"), toyRow, Label("성장 속도 (자리를 비우는 날)"), seg, btns2, resetRow, Label("화분 색"), sw, tip }) Add(c);
      tipBottom = true;
    }
    static TextBlock Label(string s) { return new TextBlock(Theme.Small, Theme.Sub) { Value = s }; }
    static void SetCue(TextBox t, string cue) {
      t.HandleCreated += (s, e) => { var p = System.Runtime.InteropServices.Marshal.StringToHGlobalUni(cue); Native.SendMessage(t.Handle, 0x1501, (IntPtr)1, p); System.Runtime.InteropServices.Marshal.FreeHGlobal(p); };
    }

    public void UpdateStatus() {
      var g = app.Game; var s = g.State; var p = s.Plant; int st = Logic.StageOf(p.Growth); var sp = Logic.SpeciesById(p.SpeciesId);
      bool known = Logic.IsRevealed(p), done = Logic.IsDone(p), thirsty = Logic.IsThirsty(p), soggy = Logic.IsSoggy(p), wilted = Logic.IsWilted(p), full = p.Water >= Logic.WATER_FULL_ABOVE && !done;
      string spName = known ? sp.Name + "  " + new string('★', Logic.RarityOf(sp.Id).Stars) : st == 0 ? "씨앗" : "무엇이 자랄까?";
      sName.Value = p.Name.Length > 0 ? (known ? p.Name + " (" + sp.Name + ")" : p.Name) : spName;
      if (!nick.Focused) nick.Text = p.Name;
      sSub.Value = done ? Logic.RarityOf(sp.Id).Label + " · 수확 완료! 새 씨앗을 심어 보세요."
        : soggy && thirsty ? "물이 모자라졌어요. 이제 물을 줘도 돼요!"
        : soggy ? "물을 너무 많이 줬어요! 다음 성장 때 회복해요."
        : wilted ? "목말라서 시들었어요. 물을 주면 다시 자라요."
        : full ? "물이 가득해요. 더 주려면 한 번 더 눌러야 해요 (시들어요!)"
        : g.Napping ? "낮잠 자는 중…"
        : st == 2 ? Logic.STAGE_NAMES[st] + " · 잎 색을 잘 보세요"
        : Logic.STAGE_NAMES[st];
      gWater.Set(p.Water, thirsty ? Theme.Low : Theme.Water);
      gGrow.Set(p.Growth / Logic.BLOOM_AT * 100, Theme.Grow);
      gLove.Set(p.Love, Theme.Love);
      double sp2 = g.TestSpeed; Func<double, string> f = sec => Logic.FmtLeft(sec / sp2);
      double wall = Game.Wall, nutriActive = Math.Max(0, s.Nutri.Until - wall), nutriWait = Math.Max(0, s.Nutri.ReadyAt - wall);
      if (done) sTime.Value = "";
      else {
        var left = Logic.TimeLeft(p, g.GrowRate());
        var lines = new List<string> { "다 클 때까지 약 " + f(left[0]) };
        if (st < 3) lines.Add("다음 단계까지 " + f(left[1]));
        lines.Add(p.Water <= 0 ? "물이 없어서 성장이 멈췄어요!" : "물은 약 " + f(p.Water / Logic.WATER_DRAIN) + " 뒤에 바닥나요");
        if (soggy) lines.Add("과습이라 성장이 절반 속도예요");
        if (s.Settings.Speed > 1) lines.Add("성장 " + s.Settings.Speed + "배속 중");
        if (nutriActive > 0) lines.Add("영양제 효과 " + Logic.FmtLeft(nutriActive / 1000) + " 남음 (1.5배)");
        sTime.Value = string.Join("\n", lines);
      }
      gWater.Collapsed = done;
      bWater.Active = !done;
      bNutri.Active = !done;
      bNutri.Label = nutriWait > 0 && !(nutriActive > 0) ? "영양제 (" + Logic.FmtLeft(nutriWait / 1000) + " 뒤)" : nutriActive > 0 ? "영양제 먹는 중" : "영양제";
      bNew.Active = done;
      foreach (var b in toyBtns.Values) b.Active = !g.ToyActive && !g.Napping;
      foreach (var kv in speedBtns) kv.Value.Pressed = s.Settings.Speed == kv.Key;
      foreach (var kv in swatches) kv.Value.Selected = s.Settings.Skin == kv.Key;
      string lk = sName.Value + "|" + sSub.Value + "|" + sTime.Value + "|" + gWater.Collapsed + "|" + body.ClientSize.Width;
      if (lk != layoutKey) { layoutKey = lk; Relayout(); } // 줄 수가 바뀔 수 있을 때만 다시 배치
    }

    /* ---------- 도감 메모 ---------- */
    TextBlock dCount; NButton dPreview; CardGrid dGrid; bool preview; string dexKey = "";
    void BuildDex() {
      dCount = new TextBlock(Theme.Big, Theme.Ink, null, 0, 0, 1.4f);
      dPreview = new NButton("다 채운 모습 보기", () => { preview = !preview; dexKey = ""; UpdateDex(); });
      var head = new Row(Theme.D(8)).Add(dCount, 1).Add(dPreview, 0);
      dGrid = new CardGrid();
      Add(head); Add(dGrid);
    }
    public void UpdateDex() {
      var s = app.Game.State;
      string key = string.Join(";", s.Collection.Select(kv => kv.Key + ":" + kv.Value.Count + ":" + kv.Value.Skin + ":" + kv.Value.Name)) + "|" + s.Settings.Skin + "|" + preview;
      if (key == dexKey) return;
      dexKey = key;
      dGrid.SuspendLayout();
      foreach (Control c in dGrid.Controls.Cast<Control>().ToList()) { c.Dispose(); }
      int n = 0;
      foreach (var sp in Logic.SPECIES) {
        DexEntry got; s.Collection.TryGetValue(sp.Id, out got);
        bool on = got != null || preview;
        if (got != null) n++;
        Bitmap img = on ? Theme.Compose(null, Sprites.PotGrid(got != null ? got.Skin : s.Settings.Skin, "happy", 0, 0, ""), Sprites.PlantGrid(sp.Id, 4, false, 0))
                        : Theme.Compose(Theme.Silhouette, Sprites.SilhouetteGrid(sp.Id));
        var r = Logic.RARITY[sp.Rarity];
        string sub = on ? new string('★', r.Stars) + " " + r.Label + (got != null ? " ×" + got.Count : "") : r.Label;
        dGrid.Controls.Add(new Card(img, on ? sp.Name : "???", sub, got != null && got.Name.Length > 0 ? "“" + got.Name + "”" : null, !on, false, false));
      }
      dCount.Value = n + " / " + Logic.SPECIES.Length;
      dPreview.Label = preview ? "미리보기 끄기" : "다 채운 모습 보기";
      dGrid.ResumeLayout();
      Relayout();
    }

    /* ---------- 옷장 메모 ---------- */
    TextBlock cCount; readonly Dictionary<string, CardGrid> cGrids = new Dictionary<string, CardGrid>(); string closetKey = "";
    void BuildCloset() {
      cCount = new TextBlock(Theme.Big, Theme.Ink, null, 0, 0, 1.4f);
      Add(cCount); Add(new TextBlock(Theme.Small, Theme.Sub) { Value = "아이템을 누르면 입고, 다시 누르면 벗어요." });
      foreach (var slot in Logic.SLOTS) {
        Add(Label(slot == "head" ? "머리" : slot == "face" ? "얼굴" : "목"));
        var grid = new CardGrid(); cGrids[slot] = grid; Add(grid);
      }
    }
    public void UpdateCloset() {
      var s = app.Game.State;
      string key = string.Join(",", s.Owned.Keys) + "|" + string.Join(",", Logic.SLOTS.Select(x => s.Equipped[x])) + "|" + s.Settings.Skin;
      if (key == closetKey) return;
      closetKey = key;
      foreach (var slot in Logic.SLOTS) {
        var grid = cGrids[slot];
        grid.SuspendLayout();
        foreach (Control c in grid.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (var it in Logic.ITEMS.Where(i => i.Slot == slot)) {
          bool has = s.Owned.ContainsKey(it.Id), on = s.Equipped[slot] == it.Id, head = slot == "head";
          var layers = head ? new[] { Sprites.PotGrid(s.Settings.Skin, "happy", 0, 0, ""), Sprites.HeadGrid(it.Id) } : new[] { Sprites.PotGrid(s.Settings.Skin, "happy", 0, 0, it.Id) };
          var img = Theme.Compose(has ? (Color?)null : Color.FromArgb(89, 0, 0, 0), layers);
          var card = new Card(img, has ? it.Name : "???", has ? (on ? "착용 중" : "입기") : it.How, null, !has, on, has);
          string id = it.Id; card.Clicked += () => app.Game.Equip(id);
          grid.Controls.Add(card);
        }
        grid.ResumeLayout();
      }
      cCount.Value = s.Owned.Count + " / " + Logic.ITEMS.Length;
      Relayout();
    }

    public void UpdateFromGame() {
      if (Kind == "status") UpdateStatus(); else if (Kind == "dex") UpdateDex(); else UpdateCloset();
    }
  }

  static class ColorExt { public static Color ToColor(this int argb) { return Color.FromArgb(argb); } }
}
