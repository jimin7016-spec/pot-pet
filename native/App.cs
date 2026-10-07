// 앱 전체: 트레이 아이콘, 단축키(Ctrl+Alt+P / Ctrl+Alt+T), 우클릭 메뉴, 메모 창 열고 닫기
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PotPet {
  sealed class App : ApplicationContext {
    public readonly Game Game;
    public readonly Icon AppIcon;
    readonly NotifyIcon tray;
    readonly Dictionary<string, NoteForm> notes = new Dictionary<string, NoteForm>();
    readonly Dictionary<string, Store.NoteUi> ui;
    readonly HotkeyWindow hotkeys;
    readonly Timer uiSave = new Timer { Interval = 300 };
    bool allHidden;
    static readonly Dictionary<string, int[]> NOTE_DEFS = new Dictionary<string, int[]> { // 폭, 높이, 오른쪽에서 떨어진 거리
      { "status", new[] { 240, 700, 0 } }, { "dex", new[] { 330, 540, 360 } }, { "closet", new[] { 300, 700, 700 } },
    };

    public App() {
      var iconBmp = Native.ResourceImage("PotPet.Icon");
      AppIcon = iconBmp != null ? Icon.FromHandle(iconBmp.GetHicon()) : SystemIcons.Application;
      ui = Store.LoadUi();
      uiSave.Tick += (s, e) => { uiSave.Stop(); Store.SaveUi(ui); };

      Game = new Game(this);
      Game.Start();

      var trayBmp = Native.ResourceImage("PotPet.Tray");
      tray = new NotifyIcon { Text = "화분 펫", Icon = trayBmp != null ? Icon.FromHandle(new Bitmap(trayBmp, 16, 16).GetHicon()) : AppIcon, Visible = true, ContextMenuStrip = new ContextMenuStrip() };
      tray.ContextMenuStrip.Opening += (s, e) => BuildTrayMenu(tray.ContextMenuStrip);

      foreach (var kind in NOTE_DEFS.Keys) if (ui[kind].Visible) ShowNote(kind);

      hotkeys = new HotkeyWindow();
      hotkeys.Register(1, Native.MOD_CONTROL | Native.MOD_ALT, Keys.P, () => SetAllHidden(!allHidden));
      hotkeys.Register(2, Native.MOD_CONTROL | Native.MOD_ALT, Keys.T, () => { if (!allHidden) Game.DropToy("random"); });
    }

    /* ---------- 메모 창 ---------- */
    Point DefaultNotePos(string kind) {
      var wa = Screen.PrimaryScreen.WorkingArea; var d = NOTE_DEFS[kind];
      return new Point(wa.Right - Native.D(d[0]) - Native.D(24) - Native.D(d[2]), wa.Top + Native.D(40));
    }
    static bool OnScreen(int x, int y) {
      return Screen.AllScreens.Any(s => { var b = s.Bounds; return x + 40 > b.Left && x < b.Right - 40 && y >= b.Top - 4 && y < b.Bottom - 40; });
    }
    NoteForm CreateNote(string kind) {
      var d = NOTE_DEFS[kind]; var u = ui[kind];
      var pos = u.X.HasValue && u.Y.HasValue && OnScreen(u.X.Value, u.Y.Value) ? new Point(u.X.Value, u.Y.Value) : DefaultNotePos(kind);
      var f = new NoteForm(this, kind, new Size(Native.D(d[0]), Native.D(d[1]))) { Location = pos };
      notes[kind] = f;
      return f;
    }
    bool NoteVisible(string kind) { NoteForm f; return notes.TryGetValue(kind, out f) && !f.IsDisposed && f.Visible; }
    public void ShowNote(string kind) {
      ui[kind].Visible = true; SaveUiSoon();
      if (allHidden) return;
      NoteForm f;
      if (!notes.TryGetValue(kind, out f) || f.IsDisposed) f = CreateNote(kind);
      f.Show();
      f.UpdateFromGame();
    }
    public void HideNote(string kind) {
      ui[kind].Visible = false; SaveUiSoon();
      NoteForm f; if (notes.TryGetValue(kind, out f) && !f.IsDisposed) f.Hide();
    }
    void ToggleNote(string kind) { if (NoteVisible(kind)) HideNote(kind); else ShowNote(kind); }
    public void NoteMoved(string kind, Point p) { if (!NoteVisible(kind)) return; ui[kind].X = p.X; ui[kind].Y = p.Y; SaveUiSoon(); }
    void SaveUiSoon() { uiSave.Stop(); uiSave.Start(); }

    /** 화분 상태가 바뀌었으니 메모를 바로 고쳐 주세요 */
    public void NotesDirty() { UpdateNotes(); }
    /** 열려 있는 메모만 고쳐요 (화분이 0.5초마다 불러요) */
    public void UpdateNotes() {
      foreach (var f in notes.Values) if (!f.IsDisposed && f.Visible) f.UpdateFromGame();
    }

    /** 화분과 메모를 한꺼번에 숨기기 / 다시 보이기 (단축키, 트레이) */
    void SetAllHidden(bool h) {
      allHidden = h;
      Game.SetHidden(h);
      foreach (var kind in NOTE_DEFS.Keys) {
        if (h) { NoteForm f; if (notes.TryGetValue(kind, out f) && !f.IsDisposed) f.Hide(); }
        else if (ui[kind].Visible) ShowNote(kind);
      }
    }
    public void SummonFromSecondLaunch() { if (allHidden) SetAllHidden(false); Game.ResetPos(); }

    /* ---------- 메뉴 ---------- */
    static ToolStripMenuItem Item(string text, Action click, bool enabled = true, bool check = false) {
      var it = new ToolStripMenuItem(text) { Enabled = enabled, Checked = check };
      if (click != null) it.Click += (s, e) => click();
      return it;
    }
    static ToolStripMenuItem Sub(string text, params ToolStripItem[] items) { var it = new ToolStripMenuItem(text); it.DropDownItems.AddRange(items); return it; }

    void BuildTrayMenu(ContextMenuStrip m) {
      m.Items.Clear();
      m.Items.Add(Item(allHidden ? "화분 다시 보이기" : "화분 숨기기", () => SetAllHidden(!allHidden)));
      m.Items.Add(Item("화분 불러오기 (화면 아래로)", () => { if (allHidden) SetAllHidden(false); Game.ResetPos(); }));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(Item("상태 메모", () => ToggleNote("status"), true, NoteVisible("status")));
      m.Items.Add(Item("도감 메모", () => ToggleNote("dex"), true, NoteVisible("dex")));
      m.Items.Add(Item("옷장 메모", () => ToggleNote("closet"), true, NoteVisible("closet")));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(Item("종료", Quit));
    }

    string NutriLabel() {
      var s = Game.State; double now = Game.Wall, w = s.Nutri.ReadyAt - now;
      if (s.Nutri.Until > now) return "영양제 효과 중 (" + Logic.FmtLeft((s.Nutri.Until - now) / 1000) + " 남음)";
      return w > 0 ? "영양제 주기 (" + Logic.FmtLeft(w / 1000) + " 뒤에 가능)" : "영양제 주기";
    }
    ContextMenuStrip petMenu;
    public void ShowPetMenu() {
      var s = Game.State; bool done = Logic.IsDone(s.Plant); var cur = s.Settings;
      if (petMenu != null) petMenu.Dispose();
      var m = petMenu = new ContextMenuStrip();
      bool toyOk = !Game.Napping && !Game.ToyActive;
      m.Items.Add(Item("물 주기", Game.DoWater, !done));
      m.Items.Add(Item(done ? "새 씨앗 심기" : "새 씨앗 심기 (다 자라면 가능해요)", Game.NewSeed, done));
      m.Items.Add(Item("화분 갈아엎고 새로 심기", () => Game.ResetGame("plant")));
      m.Items.Add(Item("전부 초기화 (도감 포함)", () => Game.ResetGame("all")));
      m.Items.Add(Sub("장난감 떨어뜨리기 (Ctrl+Alt+T)", new[] { new[] { "random", "아무거나" }, new[] { "ball", "공" }, new[] { "yarn", "털실" }, new[] { "butterfly", "나비" }, new[] { "bubble", "비눗방울" } }
        .Select(t => (ToolStripItem)Item(t[1], () => Game.DropToy(t[0]), toyOk)).ToArray()));
      m.Items.Add(Item(NutriLabel(), Game.DoNutri, !done));
      m.Items.Add(Sub("성장 속도", Logic.SPEEDS.Select(n => (ToolStripItem)Item(n == 1 ? "1배 (기본)" : n + "배", () => Game.SetSpeed(n), true, cur.Speed == n)).ToArray()));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(Item("상태 메모", () => ToggleNote("status"), true, NoteVisible("status")));
      m.Items.Add(Item("도감 메모", () => ToggleNote("dex"), true, NoteVisible("dex")));
      m.Items.Add(Item("옷장 메모", () => ToggleNote("closet"), true, NoteVisible("closet")));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(Sub("화분 색", Logic.SKIN_IDS.Select(id => (ToolStripItem)Item(Sprites.SKINS[id].Name, () => Game.SetSkin(id), true, cur.Skin == id)).ToArray()));
      m.Items.Add(Sub("지내는 방식", new[] { new[] { "free", "화면 전체 돌아다니기" }, new[] { "roam", "작업표시줄 위 걷기" }, new[] { "follow", "커서 따라가기" }, new[] { "stay", "가만히 있기" } }
        .Select(t => (ToolStripItem)Item(t[1], () => Game.SetBehavior(t[0]), true, cur.Mode == t[0])).ToArray()));
      m.Items.Add(Sub("크기", new[] { 2, 3, 4 }.Select(n => (ToolStripItem)Item(n == 2 ? "작게" : n == 3 ? "보통" : "크게", () => Game.SetScale(n), true, Game.PetScale == n)).ToArray()));
      m.Items.Add(Sub("말 걸기 (혼잣말·응원)", new[] { new[] { "often", "자주" }, new[] { "sometimes", "가끔" }, new[] { "quiet", "조용히" } }
        .Select(t => (ToolStripItem)Item(t[1], () => Game.SetTalk(t[0]), true, cur.Talk == t[0])).ToArray()));
      m.Items.Add(Item("테스트 모드 (30배속)", Game.ToggleTest, true, Game.TestSpeed > 1));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(Item("화분 숨기기 (Ctrl+Alt+P)", () => SetAllHidden(true)));
      m.Items.Add(Item("종료", Quit));
      m.Show(Cursor.Position);
    }

    public void Quit() {
      Game.Shutdown();
      uiSave.Stop(); Store.SaveUi(ui);
      hotkeys.Dispose();
      tray.Visible = false; tray.Dispose();
      foreach (var f in notes.Values) if (!f.IsDisposed) f.Dispose();
      Game.Close();
      ExitThread();
    }

    /** 전역 단축키를 받는 보이지 않는 창 */
    sealed class HotkeyWindow : NativeWindow, IDisposable {
      readonly Dictionary<int, Action> actions = new Dictionary<int, Action>();
      public HotkeyWindow() { CreateHandle(new CreateParams()); }
      public void Register(int id, int mods, Keys key, Action a) {
        if (Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, (int)key)) actions[id] = a; // 못 잡아도 트레이·메뉴로 할 수 있어요
      }
      protected override void WndProc(ref Message m) {
        Action a;
        if (m.Msg == Native.WM_HOTKEY && actions.TryGetValue(m.WParam.ToInt32(), out a)) a();
        base.WndProc(ref m);
      }
      public void Dispose() { foreach (var id in actions.Keys) Native.UnregisterHotKey(Handle, id); DestroyHandle(); }
    }
  }
}
