// 화분 창의 메인 코드: 움직임, 물주기, 쓰다듬기, 말풍선, 우클릭 메뉴
// 창 자체를 움직여서 화분이 모니터 전체를 돌아다니는 것처럼 보이게 해요.
// 가볍게 돌리는 요령: 1초에 30번만 계산하고, 그린 결과가 이전과 똑같으면 화면에 다시 보내지 않아요.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PotPet {
  sealed class Game : LayeredForm {
    /* ---------- 상수 ---------- */
    const int CW = 40, CH = 46; // 캔버스(도트 단위): 스프라이트 40 + 위쪽 여유 6
    const int SX = 4, SY = 6; // 스프라이트가 그려지는 위치
    const double SAVE_EVERY = 10000;
    const double NAP_AFTER = 300; // 키보드/마우스 입력이 이만큼(초) 없으면 낮잠
    static readonly string[] RAINBOW = { "#ff6b6b", "#ffa94d", "#ffe066", "#69db7c", "#4dabf7", "#9775fa", "#f783ac" };
    static readonly double[] HEAD_Y = { 19, 17, 11, 7, 6.5 }; // 단계별 식물 머리 높이 (반짝임 위치용)
    class Greet { public string Id, Text; public int From, To; }
    static readonly Greet[] GREETINGS = {
      new Greet { Id = "morning", From = 8, To = 11, Text = "좋은 아침이에요!\n오늘도 같이 키워요" },
      new Greet { Id = "lunch", From = 12, To = 14, Text = "점심 먹을 시간이에요~" },
      new Greet { Id = "leave", From = 18, To = 20, Text = "오늘도 수고했어요!" },
    };
    /* 혼잣말: 귀여운 말 + 응원. 문구는 마음대로 고쳐도 돼요. */
    static readonly string[] TALK_CUTE = {
      "나 잘 크고 있죠?", "꼼지락꼼지락~", "햇빛 냄새가 나요", "으쌰으쌰!", "오늘도 같이 있어서 좋아요",
      "나 오늘 좀 귀엽지 않아요?", "뿌듯해요!", "흥얼흥얼~ ♪", "뭐 하고 있어요? 구경해도 돼요?", "쪼르르르~",
      "여기 있으면 따뜻해요", "나 심심하지 않아요! 같이 있으니까요", "쑥쑥 크는 중이에요!",
    };
    static readonly string[] TALK_CHEER = {
      "오늘도 홧팅이에요!", "잘하고 있어요!", "조금만 더 힘내요!", "당신 최고예요!", "천천히 해도 괜찮아요",
      "실수해도 괜찮아요. 다시 하면 되니까요", "오늘도 수고 많아요", "할 수 있어요! 홧팅!", "지금까지 충분히 잘했어요",
      "한 걸음씩, 한 걸음씩!", "제가 응원하고 있어요!", "어려운 일도 하나씩 하면 끝나요!",
    };
    static readonly string[] TALK_CARE = {
      "물 한 잔 마셔요~", "잠깐 스트레칭 어때요?", "밥은 먹었어요?", "눈도 좀 쉬게 해 줘요", "어깨 한 번 돌려봐요~",
      "잠깐 창밖 한번 볼까요?", "허리 펴고! 쭈욱~",
    };
    class TimeTalk { public int From, To; public string[] Lines; }
    static readonly TimeTalk[] TALK_TIME = {
      new TimeTalk { From = 6, To = 11, Lines = new[] { "좋은 아침이에요! 오늘도 홧팅!", "아침부터 부지런하네요!" } },
      new TimeTalk { From = 11, To = 14, Lines = new[] { "점심은 맛있는 거 먹어요~", "점심시간엔 쉬어가요!" } },
      new TimeTalk { From = 14, To = 18, Lines = new[] { "졸리면 기지개 한번~", "오후도 거뜬히 해내요!" } },
      new TimeTalk { From = 18, To = 22, Lines = new[] { "조금만 더 하면 퇴근이에요!", "오늘도 고생 많았어요!" } },
      new TimeTalk { From = 22, To = 30, Lines = new[] { "늦게까지 고생이에요… 얼른 쉬어요", "오늘은 푹 자요. 내일 또 봐요!" } },
    };
    static readonly Dictionary<string, int[]> TALK_EVERY = new Dictionary<string, int[]> { { "often", new[] { 50, 100 } }, { "sometimes", new[] { 120, 240 } } };

    /* ---------- 상태 ---------- */
    public GameState State;
    public int PetScale = 3;
    public int TestSpeed = 1;
    public bool Napping;
    public bool ToyActive { get { return toy != null; } }
    readonly App app;
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Random rnd = new Random();
    readonly Timer timer = new Timer();
    readonly Bubble bubble = new Bubble();
    LayeredSurface surf;
    Toy toy;
    ToyPlay play; // 화분 쪽 놀이 기록
    class ToyPlay { public int Hits; public double Gained; }

    // 화분 움직임. x: 화분 가운데, fy: 발 바닥의 화면 y (실제 픽셀)
    double px_, fy, vy, tx, ty, bob, phase, feetT, lean, jumpT, jumpDur = 0.5, modeStart, modeDur;
    int dir = 1, feet; bool moving, jumping;
    string mode = "idle";
    double happyUntil, pourUntil, nextDrop, blinkUntil, nextBlink, followUntil, nextFollowAt, nextThirstSay, nextWiltTear, nextZ, nextTalk, nextHit, glowUntil, nextGlow, confirmUntil, resetUntil;
    string resetKind;
    int? lastFriendSize;
    readonly Queue<string> itemQueue = new Queue<string>();
    double lastItemCheck, lastSave, lastGreetCheck, lastNotes, lastDisplays, last;
    readonly HashSet<string> greeted = new HashSet<string>();
    bool dragDown, dragActive, petting; string dragKind; int dragSX, dragSY, dragLastSX; double dragOX, dragOY, dragAcc;
    string sayText = ""; double sayUntil;
    class Disp { public Rectangle B, WA; }
    List<Disp> displays = new List<Disp>();
    Point cursor;
    int[] dots = new int[CW * CH], shown = new int[CW * CH];
    int lastLeft = int.MinValue, lastTop = int.MinValue;

    class Particle { public string Kind = "px"; public double X, Y, VX, VY, G, Life, Max; public int Size = 1; public int Color; }
    readonly List<Particle> particles = new List<Particle>();

    double U { get { return Native.Dpi; } } // 화면 배율 (거리·속도에 곱해요)
    int PS { get { return Math.Max(1, (int)Math.Round(PetScale * U)); } } // 도트 한 칸의 실제 픽셀
    double Now { get { return clock.Elapsed.TotalMilliseconds; } }
    public static double Wall { get { return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; } }
    double Rand(double a, double b) { return a + rnd.NextDouble() * (b - a); }
    T Pick<T>(IList<T> a) { return a[rnd.Next(a.Count)]; }
    static double Clamp(double v, double a, double b) { return Logic.Clamp(v, a, b); }
    string Behavior { get { return State.Settings.Mode; } }
    static string Stars(string id) { return new string('★', Logic.RarityOf(id).Stars); }
    /** 받침 있으면 a, 없으면 b (예: 비글이었/햄스터였) */
    static string Josa(string w, string a, string b) { int c = w.Length > 0 ? w[w.Length - 1] - 0xac00 : -1; return c >= 0 && c < 11172 && c % 28 != 0 ? a : b; }

    public Game(App app) : base(false) {
      this.app = app;
      Text = "화분 펫";
      Size = new Size(1, 1);
      timer.Interval = 33;
      timer.Tick += (s, e) => Tick();
    }

    /* ---------- 시작 ---------- */
    public void Start() {
      double wallNow = Wall;
      var saved = Store.Read(Store.GameFile);
      State = Logic.NormalizeState(saved, wallNow, rnd.NextDouble);
      PetScale = State.Settings.Scale;
      RefreshDisplays();
      surf = new LayeredSurface(CW * PS, CH * PS);
      var prim = displays[0];
      bool inside = false;
      if (State.Settings.X.HasValue && State.Settings.Y.HasValue) {
        double sx = State.Settings.X.Value, sy = State.Settings.Y.Value;
        inside = displays.Any(d => sx >= d.B.Left + 18 * U && sx <= d.B.Right - 18 * U && sy > d.B.Top + 60 * U && sy <= d.B.Bottom);
        if (inside) { px_ = sx; fy = sy; }
      }
      if (!inside) { px_ = prim.WA.Left + prim.WA.Width / 2.0; fy = GroundOf(prim); }

      var off = Logic.ApplyOffline(State, wallNow); // 꺼져 있던 동안의 성장
      double t = Now;
      last = t; lastSave = t;
      SetMode("idle", t, 1500);
      nextFollowAt = t + Rand(20000, 50000);
      nextThirstSay = t + 30000;
      ScheduleTalk(t);
      nextBlink = t + 2000;
      int hr = DateTime.Now.Hour; var cg = GREETINGS.FirstOrDefault(x => hr >= x.From && hr < x.To);
      if (cg != null) greeted.Add(DateTime.Now.ToShortDateString() + cg.Id); // 시작하자마자 시간대 인사는 하지 않음
      var sp = Logic.SpeciesById(State.Plant.SpeciesId);
      if (off.FinishedNow) {
        SetMode("celebrate", t, 2500);
        Say("자는 동안 " + sp.Name + "이(가) 다 자랐어요! " + Stars(sp.Id) + (off.IsNewSpecies ? "\n도감에 새로 등록했어요" : ""), 7000);
      } else if (saved == null) Say("안녕하세요!\n저를 잘 키워주세요", 5000);
      else if (off.Elapsed > 600) Say(Logic.IsDry(State.Plant) ? "어서 와요!\n목이 많이 말랐어요…" : "어서 와요! 보고 싶었어요", 4500);
      SaveNow();
      Show();
      Render(t, 0);
      timer.Start();
    }

    /* ---------- 모니터 ---------- */
    void RefreshDisplays() {
      var list = Screen.AllScreens.OrderBy(s => s.Primary ? 0 : 1).Select(s => new Disp { B = s.Bounds, WA = s.WorkingArea }).ToList();
      if (list.Count > 0) displays = list;
      else if (displays.Count == 0) displays.Add(new Disp { B = new Rectangle(0, 0, 1920, 1080), WA = new Rectangle(0, 0, 1920, 1040) });
    }
    Disp DisplayAt(double x, double y) {
      Disp best = displays[0]; double bestD = double.MaxValue;
      foreach (var d in displays) {
        var b = d.B;
        double dx = Math.Max(Math.Max(b.Left - x, 0), x - b.Right), dy = Math.Max(Math.Max(b.Top - y, 0), y - b.Bottom), dist = dx * dx + dy * dy;
        if (dist < bestD) { bestD = dist; best = d; }
        if (dist == 0) break;
      }
      return best;
    }
    static double GroundOf(Disp d) { return d.WA.Bottom; } // 작업표시줄 윗면
    double TopLimit(Disp d) { return d.B.Top + CH * PS + 8 * U; }
    void ClampToDisplay() {
      var d = DisplayAt(px_, fy); var b = d.B;
      px_ = Clamp(px_, b.Left + 18 * U, b.Right - 18 * U);
      fy = Clamp(fy, TopLimit(d), b.Bottom);
    }
    void SetMode(string m, double now, double dur) { mode = m; modeStart = now; modeDur = dur; }

    /* ---------- 말풍선 ---------- */
    public void Say(string text, double ms) { sayText = text; sayUntil = Now + ms; }
    bool SayShown { get { return bubble.IsShown; } }
    void UpdateBubble(double now) {
      bool show = !dragActive && now < sayUntil && Visible;
      if (show) { bubble.SetText(sayText); bubble.ShowAt((int)Math.Round(px_), (int)Math.Round(fy - CH * PS - 6 * U)); }
      else bubble.HideBubble();
    }

    /* ---------- 입자 (물방울, 반짝이, 하트, 색종이, zzz) ---------- */
    static readonly string[] HEART = { ".#.#.", "#####", ".###.", "..#.." };
    static readonly string[] ZZZ = { "###", "..#", ".#.", "#..", "###" };
    void AddP(Particle p) {
      if (particles.Count > 220) return;
      p.Max = p.Life;
      particles.Add(p);
    }
    void Burst(int n, string[] colors, double cx, double cy, double spread) {
      for (int i = 0; i < n; i++)
        AddP(new Particle { Kind = "spark", X = cx + Rand(-spread, spread), Y = cy + Rand(-spread, spread), VX = Rand(-14, 14), VY = Rand(-22, -4), Life = Rand(0.5, 1.0), Color = Sprites.C(Pick(colors)) });
    }
    static PointF HeadPos(int stage) { return new PointF(SX + 15.5f, SY + (float)HEAD_Y[stage]); }
    void Confetti() {
      AddP(new Particle { X = SX + 15.5 + Rand(-7, 7), Y = SY + Rand(4, 9), VX = Rand(-30, 30), VY = Rand(-55, -20), G = 95, Life = Rand(0.8, 1.4), Size = rnd.NextDouble() < 0.4 ? 2 : 1, Color = Sprites.C(Pick(RAINBOW)) });
    }
    void UpdateParticles(double dt) {
      for (int i = particles.Count - 1; i >= 0; i--) {
        var p = particles[i];
        p.VY += p.G * dt; p.X += p.VX * dt; p.Y += p.VY * dt; p.Life -= dt;
        if (p.Kind == "drop" && p.Y >= SY + 22) {
          for (int k = 0; k < 3; k++) AddP(new Particle { X = p.X, Y = SY + 21, VX = Rand(-14, 14), VY = Rand(-22, -8), G = 80, Life = 0.35, Color = Sprites.C("#9bd6ff") });
          p.Life = 0;
        }
        if (p.Life <= 0) particles.RemoveAt(i);
      }
    }

    /** 상태에 따라 가끔 나오는 분위기 효과 */
    void Ambient(double dt, double now) {
      var p = State.Plant; int stage = Logic.StageOf(p.Growth); var h = HeadPos(stage);
      if (Logic.RarityOf(p.SpeciesId).Stars >= 3 && stage >= 3 && rnd.NextDouble() < dt * (stage == 4 ? 2.4 : 1.0))
        Burst(1, p.SpeciesId == "purplebasil" ? new[] { "#e6ccff", "#ffffff" } : new[] { "#fff7b0", "#ffffff", "#ffe066" }, h.X, h.Y + 3, 8);
      if (now < pourUntil && now >= nextDrop) {
        nextDrop = now + 70;
        AddP(new Particle { Kind = "drop", X = SX + 21 + Rand(-0.3, 0.3), Y = SY + 11, VY = 30, G = 70, Life = 1.5, Color = Sprites.C("#58b7ff") });
      }
      if (now < glowUntil && !Logic.IsDone(p) && now >= nextGlow) {
        nextGlow = now + 500;
        Burst(1, new[] { "#b9f6ca", "#fff3b0" }, h.X + Rand(-5, 5), h.Y + Rand(0, 8), 4);
      }
      if (Logic.IsWilted(p) && !Napping && now >= nextWiltTear) {
        nextWiltTear = now + 1600;
        AddP(new Particle { Kind = "drop", X = SX + 12, Y = SY + 34, VY = 10, G = 40, Life = 0.8, Color = Sprites.C("#6ec6ff") });
      }
      if (Napping && now >= nextZ) {
        nextZ = now + 1400;
        AddP(new Particle { Kind = "z", X = SX + 24, Y = SY + 18, VX = 2, VY = -6, Life = 2.2, Color = Sprites.C("#3a2a33") });
      }
    }

    /* ---------- 식물 이벤트 ---------- */
    void OnStage(int stage, double now, bool recovered) {
      if (recovered) State.Stats["recovers"] += 1;
      var h = HeadPos(stage);
      Burst(stage == 3 ? 18 : 8, stage == 3 ? new[] { "#ffffff", "#fff3b0", "#ffc4d6", "#b9f6ca" } : new[] { "#ffffff", "#b9f6ca" }, h.X, h.Y, 7);
      happyUntil = Math.Max(happyUntil, now + 1500);
      if (recovered) { Say("다시 쌩쌩해졌어요!\n" + (stage % 2 == 0 ? "쑥쑥 자라는 중~" : "다 나았어요!"), 3400); Burst(10, new[] { "#b9f6ca", "#ffffff" }, h.X, h.Y + 6, 8); }
      else if (stage == 1) Say("싹이 텄어요!", 3000);
      else if (stage == 2) Say("잎이 늘었어요.\n뭘까요…?", 3200);
      else if (stage == 3 && Logic.IsFriend(State.Plant.SpeciesId)) {
        string n = Logic.SpeciesById(State.Plant.SpeciesId).Name;
        Say("앗! 식물이 아니라\n" + n + Josa(n, "이었", "였") + "어요!", 4500);
        Burst(16, new[] { "#ffc4d6", "#fff3b0", "#ffffff" }, h.X, h.Y + 6, 9);
      } else if (stage == 3) Say("앗, " + Logic.SpeciesById(State.Plant.SpeciesId).Name + "이었어요!", 4000);
      app.NotesDirty();
    }
    void OnFinished(double now, bool isNew) {
      var sp = Logic.SpeciesById(State.Plant.SpeciesId);
      Say((State.Plant.Name.Length > 0 ? State.Plant.Name + " (" + sp.Name + ")" : sp.Name) + " 수확 완료! " + Stars(sp.Id) + (isNew ? "\n도감에 새로 등록했어요" : ""), 6500);
      SetMode("celebrate", now, 3200);
      happyUntil = now + 4000;
      SaveNow();
      app.NotesDirty();
    }
    void Apply(StepResult r, double now) {
      if (r.FinishedNow) OnFinished(now, Logic.RegisterHarvest(State, Wall));
      else if (r.Stage != r.PrevStage) OnStage(r.Stage, now, r.Recovered);
    }

    /* ---------- 행동 (메뉴·메모에서 불러요) ---------- */
    public void DoWater() {
      double now = Now;
      bool confirmed = now < confirmUntil;
      var r = Logic.WaterPlant(State.Plant, confirmed);
      if (r.Result != "full") confirmUntil = 0;
      if (r.Result == "done") { Say("이미 다 컸어요!", 1800); happyUntil = now + 900; return; }
      if (r.Result == "soggy") { Say("아직 푹 젖어 있어요…\n다음 성장 때 괜찮아져요", 3200); return; }
      if (r.Result == "full") { confirmUntil = now + 4000; Say("배불러요! 물이 가득해요\n그래도 주려면 한 번 더 눌러요", 4000); return; }
      pourUntil = now + 1500;
      nextDrop = now;
      State.Stats["waters"] += 1;
      if (r.Result == "over") {
        // 과습: 물을 너무 많이 줘서 시들어요 (다음 성장 단계에 오르면 회복)
        happyUntil = 0;
        Say("으에… 물이 너무 많아요!\n다음 성장 때 괜찮아져요", 5000);
        Burst(10, new[] { "#9bd6ff", "#ffffff" }, SX + 15.5, SY + 22, 8);
        app.NotesDirty();
        return;
      }
      happyUntil = now + 2600;
      Say(r.Revived ? "살았다…! 고마워요!" : Pick(new[] { "고마워요!", "시원해요~", "꿀꺽꿀꺽!" }), 2400);
      if (r.Revived) Burst(12, new[] { "#9bd6ff", "#ffffff" }, SX + 15.5, SY + 22, 7);
      app.NotesDirty();
    }

    void DoStroke() {
      double now = Now, before = State.Plant.Love;
      var r = Logic.Stroke(State.Plant);
      if (before < Logic.LOVE_MAX && State.Plant.Love >= Logic.LOVE_MAX) State.Stats["lovefull"] += 1;
      AddP(new Particle { Kind = "heart", X = SX + 15.5 + Rand(-10, 10), Y = SY + Rand(0, 8), VX = Rand(-4, 4), VY = -16, Life = 1.1, Color = Sprites.C(Pick(new[] { "#ff5f86", "#ff8fab", "#ff3d6e" })) });
      double love = State.Plant.Love;
      if (love == Logic.LOVE_PER_STROKE || love == 50 || love == Logic.LOVE_MAX) Say(Pick(new[] { "으헤헤", "기분 좋아요~", "더 해줘요!" }), 1600);
      Apply(r, now);
    }

    public void NewSeed() { if (Logic.IsDone(State.Plant)) PlantFresh(); }

    // 초기화: "plant" = 이 화분만 갈아엎고 새 씨앗 / "all" = 도감까지 전부. 4초 안에 한 번 더 눌러야 해요.
    public void ResetGame(string kind) {
      double now = Now;
      if (!(resetKind == kind && now < resetUntil)) {
        resetKind = kind; resetUntil = now + 4000;
        Say(kind == "all" ? "도감과 아이템까지 전부 지워져요!\n정말이면 한 번 더 눌러요" : "이 화분을 갈아엎고\n새 씨앗을 심어요. 한 번 더!", 4000);
        return;
      }
      resetKind = null; resetUntil = 0;
      if (kind == "all") {
        var d = Logic.DefaultState(Wall, rnd.NextDouble);
        State.Collection = d.Collection; State.Stats = d.Stats; State.Owned = d.Owned; State.Equipped = d.Equipped; State.Nutri = d.Nutri;
        itemQueue.Clear();
      } else State.Stats["replants"] += 1;
      PlantFresh();
    }
    void PlantFresh() {
      State.Plant = Logic.NewPlant(rnd.NextDouble, Wall);
      double now = Now;
      SetMode("hop", now, 480);
      happyUntil = now + 1500;
      nextThirstSay = now + 40000;
      if (Logic.RarityOf(State.Plant.SpeciesId).Stars >= 3) { Say("씨앗이 반짝반짝\n빛나요…!", 4000); Burst(10, new[] { "#fff3b0", "#ffffff" }, SX + 15.5, SY + 19, 6); }
      else Say(Pick(new[] { "어떤 식물이 자랄까요?", "새 씨앗이다!", "두근두근…" }), 3000);
      SaveNow();
      app.NotesDirty();
    }

    public void SetName(string raw) {
      string n = Logic.CleanName(raw); double now = Now;
      if (n == State.Plant.Name) return;
      State.Plant.Name = n;
      if (n.Length > 0) {
        State.Stats["named"] += 1;
        Say(Pick(new[] { n + "! 마음에 들어요", n + "… 좋은 이름이에요!", "내 이름은 " + n + "!" }), 3600);
        happyUntil = now + 2400;
        Burst(8, new[] { "#ffc4d6", "#ffffff" }, SX + 15.5, SY + 12, 7);
      } else Say("이름을 지웠어요", 1800);
      SaveNow();
      app.NotesDirty();
    }

    public void DoNutri() {
      double now = Now; var r = Logic.UseNutri(State, Wall);
      if (r.Result == "done") { Say("이미 다 컸어요!", 1800); return; }
      if (r.Result == "soggy") { Say("푹 젖었을 땐\n영양제가 안 맞아요", 3000); return; }
      if (r.Result == "cooldown") { Say("영양제는 " + Logic.FmtLeft(r.WaitMs / 1000) + " 뒤에\n또 먹을 수 있어요", 3200); return; }
      State.Stats["nutris"] += 1;
      glowUntil = now + Logic.NUTRI_MS;
      happyUntil = now + 2600;
      pourUntil = 0;
      Say(Pick(new[] { "영양제 꿀꺽!\n1시간 동안 쑥쑥 자라요", "으쌰! 힘이 나요!" }), 3600);
      Burst(14, new[] { "#b9f6ca", "#fff3b0", "#ffffff" }, SX + 15.5, SY + 12, 9);
      SetMode("hop", now, 480);
      SaveNow();
      app.NotesDirty();
    }

    public void SetSpeed(int n) {
      if (!Logic.SPEEDS.Contains(n)) return;
      State.Settings.Speed = n;
      Say(n == 1 ? "원래 속도로 자랄게요" : n + "배속으로 쑥쑥!\n물은 그대로 마르니 챙겨줘요", 3200);
      happyUntil = Now + 1200;
      SaveNow();
      app.NotesDirty();
    }
    public double GrowRate() { return State.Settings.Speed * Logic.NutriRate(State, Wall); }

    public void Equip(string id) {
      if (!Logic.EquipItem(State, id)) return;
      var it = Logic.ItemById(id); bool on = State.Equipped[it.Slot] == id;
      happyUntil = Now + 1400;
      Say(on ? Pick(new[] { it.Name + "!\n잘 어울려요?", "와, 마음에 들어요!" }) : "벗었어요", 2400);
      SaveNow();
      app.NotesDirty();
    }

    /** 조건을 채운 아이템이 있으면 새로 받아요 */
    void CheckItems(double now) {
      var got = Logic.CheckItems(State, Wall);
      if (got.Count == 0) return;
      foreach (var it in got) itemQueue.Enqueue("새 아이템!\n" + it.Name + "을(를) 얻었어요");
      happyUntil = now + 2500;
      Burst(12, new[] { "#ffe066", "#ffffff", "#ffc4d6" }, SX + 15.5, SY + 8, 9);
      SaveNow();
      app.NotesDirty();
    }

    public void SetSkin(string id) {
      if (!Logic.SKIN_IDS.Contains(id)) return;
      State.Settings.Skin = id;
      happyUntil = Now + 900;
      SaveNow();
      app.NotesDirty();
    }
    public void SetTalk(string t) {
      if (!Logic.TALKS.Contains(t)) return;
      State.Settings.Talk = t;
      ScheduleTalk(Now);
      Say(t == "often" ? "자주 말 걸게요!" : t == "sometimes" ? "가끔 말 걸게요~" : "조용히 있을게요…", 1800);
      SaveNow();
    }
    public void SetBehavior(string m) {
      if (!Logic.MODES.Contains(m)) return;
      State.Settings.Mode = m;
      double now = Now;
      SetMode("idle", now, 300);
      nextFollowAt = now + (m == "follow" ? 0 : Rand(8000, 20000));
      Say(m == "free" ? "구경하러 가요~" : m == "roam" ? "어슬렁어슬렁" : m == "follow" ? "같이 가요!" : "여기 있을게요", 1800);
      SaveNow();
      app.NotesDirty();
    }
    public void SetScale(int s) {
      if (!Logic.SCALES.Contains(s)) return;
      PetScale = s; surf.Resize(CW * PS, CH * PS); Array.Clear(shown, 0, shown.Length); lastLeft = int.MinValue;
      SaveNow();
    }
    public void ToggleTest() {
      TestSpeed = TestSpeed > 1 ? 1 : 30;
      Say(TestSpeed > 1 ? "30배속!" : "원래 속도로", 1800);
      app.NotesDirty();
    }
    public void ResetPos() {
      var d = displays[0];
      px_ = d.WA.Left + d.WA.Width / 2.0; fy = GroundOf(d);
      SetMode("hop", Now, 480);
      Say("여기 있어요!", 2000);
    }

    /* ---------- 장난감 놀이 ---------- */
    public void DropToy(string kind) {
      if (Napping || toy != null) { if (toy != null) Say("아직 놀고 있어요!", 1600); return; }
      if (string.IsNullOrEmpty(kind) || kind == "random") kind = Pick(Logic.TOYS);
      if (!Logic.TOYS.Contains(kind) || !Visible) return;
      var d = DisplayAt(px_, fy - 10); var b = d.B;
      bool onThis = cursor.X >= b.Left + 40 * U && cursor.X <= b.Right - 40 * U;
      double sx = onThis ? cursor.X : Math.Min(b.Right - 60 * U, Math.Max(b.Left + 60 * U, px_ + Rand(-150, 150) * U));
      toy = new Toy(kind, sx, fy, b);
      play = new ToyPlay();
      SetMode("play", Now, 0);
      Say(kind == "ball" ? "공이다!" : kind == "yarn" ? "털실이다!" : kind == "butterfly" ? "나비다!" : "비눗방울!", 1800);
      app.NotesDirty();
    }
    void OnToyEnd() {
      var p = play; double now = Now;
      toy.Dispose(); toy = null; play = null;
      if (mode == "play") SetMode("idle", now, 1200);
      if (p != null && p.Hits > 0) {
        State.Stats["plays"] += 1;
        happyUntil = now + 2400;
        int min = (int)Math.Round(p.Gained / 60);
        Say("신나게 놀았어요!" + (min >= 1 ? "\n(성장 +" + min + "분)" : ""), 3400);
        SaveNow();
      } else Say("다음엔 꼭 잡을게요…", 2200);
      app.NotesDirty();
    }
    void ToyHit(double now) {
      nextHit = now + 450;
      play.Hits += 1;
      jumping = true; jumpT = 0; jumpDur = 0.45;
      happyUntil = Math.Max(happyUntil, now + 900);
      double away = (toy.X - px_) * 5 + Rand(-140, 140) * U;
      toy.Kick(Clamp(away, -520 * U, 520 * U), -Rand(640, 860) * U);
      AddP(new Particle { Kind = "heart", X = SX + 15.5 + Rand(-8, 8), Y = SY + Rand(0, 6), VX = Rand(-4, 4), VY = -16, Life = 0.9, Color = Sprites.C(Pick(new[] { "#ff5f86", "#ff8fab" })) });
      double before = State.Plant.Love;
      var r = Logic.PlayHit(State.Plant);
      play.Gained += r.Gained;
      if (before < Logic.LOVE_MAX && State.Plant.Love >= Logic.LOVE_MAX) State.Stats["lovefull"] += 1;
      if (play.Hits % 3 == 1) Say(Pick(new[] { "얍!", "잡았다!", "와~", "이리 와!", "헤헤" }), 1100);
      Apply(r, now);
    }

    /* ---------- 저장 ---------- */
    public void SaveNow() {
      if (State == null) return;
      State.LastSeen = Wall;
      State.Settings.Scale = PetScale;
      State.Settings.X = Math.Round(px_);
      State.Settings.Y = Math.Round(fy);
      Store.Write(Store.GameFile, Logic.ToJson(State));
    }

    /* ---------- 움직임 ---------- */
    bool WantFollow(double now) {
      string b = Behavior;
      if (b == "follow") return true;
      if (b == "stay") return false;
      return now >= nextFollowAt;
    }
    void EnterFollow(double now) {
      SetMode("follow", now, 0);
      followUntil = now + Rand(9000, 16000);
      if (Behavior != "follow" && rnd.NextDouble() < 0.6) Say("같이 가요!", 1800);
    }
    /** 목표를 향해 한 걸음. 도착했으면 false */
    bool StepToward(double tx_, double ty_, double step, double stop) {
      double dx = tx_ - px_, dy = ty_ - fy, d = Math.Sqrt(dx * dx + dy * dy);
      if (d <= stop + 0.5) return false;
      double m = Math.Min(step, d - stop);
      px_ += dx / d * m; fy += dy / d * m;
      if (Math.Abs(dx) > 1) dir = Math.Sign(dx);
      return true;
    }
    void PickWalkTarget() {
      var cur = DisplayAt(px_, fy);
      var d = displays.Count > 1 && rnd.NextDouble() < 0.3 ? Pick(displays) : cur;
      var b = d.B;
      tx = Rand(b.Left + 40 * U, b.Right - 40 * U);
      ty = Behavior == "roam" ? GroundOf(d) : Rand(TopLimit(d) + 30 * U, b.Bottom - 2 * U);
    }
    /** 걷다가 가끔 폴짝 (초당 확률 rate) */
    void MaybeJump(double dt, double now, double rate) {
      if (jumping || rnd.NextDouble() >= dt * rate) return;
      jumping = true; jumpT = 0; jumpDur = Rand(0.42, 0.55);
      happyUntil = Math.Max(happyUntil, now + 700);
      if (rnd.NextDouble() < 0.18) Say(Pick(new[] { "폴짝!", "얍!", "신난다~", "휘리릭~" }), 1300);
    }

    void UpdateBehavior(double dt, double now) {
      moving = false;
      double b = 0;
      if (mode == "drag") { bob = 0; feet = 0; jumping = false; return; }
      if (mode == "fall") {
        vy += 2400 * U * dt;
        fy += vy * dt;
        double g = GroundOf(DisplayAt(px_, fy));
        if (fy >= g) { fy = g; vy = 0; SetMode("hop", now, 450); happyUntil = now + 900; }
        ClampToDisplay();
        return;
      }
      if (Napping) { bob = 0; feet = 0; return; }
      if (toy != null && (mode == "idle" || mode == "walk" || mode == "follow")) SetMode("play", now, 0);

      switch (mode) {
        case "idle": {
          if (Behavior == "roam") {
            double g = GroundOf(DisplayAt(px_, fy));
            if (Math.Abs(fy - g) > 2 * U) { vy = 0; SetMode("fall", now, 0); break; }
          }
          if (WantFollow(now)) { EnterFollow(now); break; }
          if (now >= modeStart + modeDur) {
            double r = rnd.NextDouble();
            if (Behavior == "stay") SetMode(r < 0.25 ? "hop" : "idle", now, r < 0.25 ? 480 : Rand(3000, 7000));
            else if (r < 0.6) { PickWalkTarget(); SetMode("walk", now, 0); }
            else if (r < 0.75) SetMode("hop", now, 480);
            else SetMode("idle", now, Rand(2000, 5000));
          }
          break;
        }
        case "walk": {
          if (WantFollow(now)) { EnterFollow(now); break; }
          if (!StepToward(tx, ty, 16 * U * dt, 0)) { SetMode("idle", now, Rand(2000, 5000)); break; }
          moving = true;
          phase += dt * 8;
          b = Math.Abs(Math.Sin(phase)) * 1.2;
          MaybeJump(dt, now, 0.18);
          break;
        }
        case "follow": {
          string bh = Behavior;
          if (bh == "stay" || (bh != "follow" && now > followUntil)) {
            nextFollowAt = now + Rand(40000, 110000);
            SetMode("idle", now, Rand(800, 1800));
            break;
          }
          var c = cursor; var d = DisplayAt(c.X, c.Y);
          double ty_ = bh == "roam" ? GroundOf(d) : Clamp(c.Y + 30 * U, TopLimit(d), d.B.Bottom);
          double dist = Math.Sqrt((c.X - px_) * (c.X - px_) + (ty_ - fy) * (ty_ - fy));
          if (dist > 56 * U && StepToward(c.X, ty_, (dist > 240 * U ? 170 : 105) * U * dt, 56 * U)) {
            moving = true;
            phase += dt * 10;
            b = Math.Abs(Math.Sin(phase)) * 4;
            MaybeJump(dt, now, 0.1);
          } else if (rnd.NextDouble() < dt * 0.5) happyUntil = now + 900;
          break;
        }
        case "hop": {
          double t = Clamp((now - modeStart) / Math.Max(1, modeDur), 0, 1);
          b = Math.Sin(Math.PI * t) * 5;
          if (t >= 1) SetMode("idle", now, Rand(1200, 3000));
          break;
        }
        case "play": {
          if (toy == null) { SetMode("idle", now, 800); break; }
          double dx = toy.X - px_;
          if (Math.Abs(dx) > 6 * U) {
            px_ += Math.Sign(dx) * Math.Min(Math.Abs(dx), (Math.Abs(dx) > 120 * U ? 260 : 150) * U * dt);
            dir = Math.Sign(dx);
            moving = true;
            phase += dt * 11;
            b = Math.Abs(Math.Sin(phase)) * 3;
          }
          double top = fy - CH * PS - 24 * U, bot = fy - 3 * PS;
          if (now >= nextHit && Math.Abs(dx) < 14 * PS + 14 * U && toy.Y > top && toy.Y < bot && !toy.Dead) ToyHit(now);
          break;
        }
        case "celebrate": {
          b = Math.Abs(Math.Sin((now - modeStart) / 160)) * 5;
          if (rnd.NextDouble() < dt * 14) Confetti();
          if (now >= modeStart + modeDur) SetMode("idle", now, 1500);
          break;
        }
        default: SetMode("idle", now, 1000); break;
      }

      if (jumping) {
        jumpT += dt;
        double k = jumpT / jumpDur;
        if (k >= 1) jumping = false;
        else b = Math.Sin(Math.PI * k) * 6; // 폴짝!
      }
      bob = b;
      ClampToDisplay();
      if (moving) {
        feetT += dt;
        if (feetT > 0.16) { feetT = 0; feet = feet == 1 ? 2 : 1; }
        if (feet == 0) feet = 1;
      } else { feet = 0; feetT = 0; }
    }

    /* ---------- 그리기 ---------- */
    string CurrentFace(double now) {
      var p = State.Plant;
      if (petting) return "love";
      if (Napping) return "sleep";
      if (Logic.IsWilted(p)) return "sad";
      if (mode == "drag" || mode == "celebrate" || now < happyUntil || Logic.IsDone(p)) return "happy";
      if (now < blinkUntil) return "blink";
      if (now >= nextBlink) { blinkUntil = now + 130; nextBlink = now + Rand(2400, 5600); return "blink"; }
      return "idle";
    }
    int CurrentLook() {
      if (moving) return dir;
      double dx = cursor.X - px_;
      return Math.Abs(dx) > 80 * U && Math.Abs(cursor.Y - fy) < 500 * U ? Math.Sign(dx) : 0;
    }

    void Blit(Grid g, int ox, int oy) {
      for (int y = 0; y < g.H; y++) {
        int yy = oy + y; if (yy < 0 || yy >= CH) continue;
        for (int x = 0; x < g.W; x++) { int c = g.D[y * g.W + x]; int xx = ox + x; if (c != 0 && xx >= 0 && xx < CW) dots[yy * CW + xx] = c; }
      }
    }
    void Fill(int x, int y, int w, int h, int c, double alpha) {
      int a = (int)Math.Round(255 * Clamp(alpha, 0, 1)); if (a <= 0) return;
      for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) {
        if (xx < 0 || yy < 0 || xx >= CW || yy >= CH) continue;
        int i = yy * CW + xx, d = dots[i];
        if (a >= 255 || d == 0 && a >= 255) { dots[i] = c; continue; }
        int da = (d >> 24) & 255, oa = a + da * (255 - a) / 255;
        if (oa == 0) continue;
        int r = 0;
        for (int s = 16; s >= 0; s -= 8) { int sc = (c >> s) & 255, dc = (d >> s) & 255; r |= ((sc * a + dc * da * (255 - a) / 255) / oa) << s; }
        dots[i] = (oa << 24) | r;
      }
    }

    void Render(double now, double dt) {
      var p = State.Plant; int stage = Logic.StageOf(p.Growth); bool wilt = Logic.IsWilted(p);
      int bobY = -Sprites.R(bob);
      Array.Clear(dots, 0, dots.Length);
      var eq = State.Equipped;
      string wear = string.Join(",", new[] { eq["face"], eq["neck"] }.Where(x => !string.IsNullOrEmpty(x)));
      Blit(Sprites.PotGrid(State.Settings.Skin, CurrentFace(now), CurrentLook(), feet, wear), SX, SY + bobY);

      // 식물: 줄 단위로 옆으로 밀어서 도트 그대로 살랑살랑 흔들리게 그림
      double want = moving ? -dir * (mode == "follow" ? 2.2 : 1.2) : 0;
      lean += (want - lean) * Math.Min(1, dt * 8);
      double sway = wilt || stage == 0 || Napping ? 0 : Math.Sin(now / 650) * (0.35 + stage * 0.22) + lean + (petting ? Math.Sin(now / 45) * 2 : 0);
      int fsize = Logic.FriendSize(p);
      if (Logic.IsFriend(p.SpeciesId) && stage == 3 && lastFriendSize.HasValue && fsize > lastFriendSize.Value) {
        Burst(10, new[] { "#ffffff", "#fff3b0", "#ffc4d6" }, SX + 15.5, SY + 12, 8);
        Say(Pick(new[] { "쑥! 조금 더 컸어요", "몸이 커졌어요!", "무럭무럭~" }), 2400);
      }
      lastFriendSize = fsize;
      var plant = Sprites.PlantGrid(p.SpeciesId, stage, wilt, fsize);
      for (int y = 0; y < Sprites.H; y++) {
        int dx = Sprites.R(sway * Math.Max(0, 22 - y) / 14), yy = SY + y + bobY;
        if (yy < 0 || yy >= CH) continue;
        for (int x = 0; x < Sprites.W; x++) { int c = plant.D[y * Sprites.W + x], xx = SX + x + dx; if (c != 0 && xx >= 0 && xx < CW) dots[yy * CW + xx] = c; }
      }
      if (!string.IsNullOrEmpty(eq["head"])) Blit(Sprites.HeadGrid(eq["head"]), SX, SY + bobY);

      if (Logic.IsThirsty(p) && !dragActive && !Napping) {
        int c = Sprites.C("#58b7ff"), dy = (int)Math.Floor(now / 300) % 2;
        Fill(CW - 9, 2 + dy, 3, 4, c, 1); Fill(CW - 10, 5 + dy, 5, 2, c, 1);
      }
      if (now < pourUntil) Blit(Sprites.CanGrid(), SX + 19, SY + 1 + Sprites.R(Math.Sin(now / 80) * 0.6));
      foreach (var q in particles) {
        double a = Math.Min(1, q.Life / (q.Max * 0.4));
        int x = Sprites.R(q.X), y = Sprites.R(q.Y);
        if (q.Kind == "heart") { for (int ry = 0; ry < HEART.Length; ry++) for (int rx = 0; rx < HEART[ry].Length; rx++) if (HEART[ry][rx] == '#') Fill(x + rx - 2, y + ry, 1, 1, q.Color, a); }
        else if (q.Kind == "z") { for (int ry = 0; ry < ZZZ.Length; ry++) for (int rx = 0; rx < ZZZ[ry].Length; rx++) if (ZZZ[ry][rx] == '#') Fill(x + rx, y + ry, 1, 1, q.Color, a); }
        else if (q.Kind == "spark" && q.Life > q.Max * 0.35) { Fill(x, y, 1, 1, q.Color, a); Fill(x - 1, y, 1, 1, q.Color, a); Fill(x + 1, y, 1, 1, q.Color, a); Fill(x, y - 1, 1, 1, q.Color, a); Fill(x, y + 1, 1, 1, q.Color, a); }
        else if (q.Kind == "drop") Fill(x, y, 1, 2, q.Color, a);
        else Fill(x, y, q.Size, q.Size, q.Color, a);
      }

      int left = (int)Math.Round(px_ - CW * PS / 2.0), top = (int)Math.Round(fy - CH * PS);
      if (!dots.SequenceEqual(shown) || surf.Width != CW * PS) {
        Array.Copy(dots, shown, dots.Length);
        Upscale();
        surf.Present(Handle, left, top);
        lastLeft = left; lastTop = top;
      } else if (left != lastLeft || top != lastTop) {
        MoveTo(left, top);
        lastLeft = left; lastTop = top;
      }
    }
    int[] big = new int[0];
    /** 도트 → 실제 픽셀 (미리 곱한 알파로) */
    void Upscale() {
      int ps = PS, w = CW * ps, h = CH * ps;
      if (surf.Width != w || surf.Height != h) surf.Resize(w, h);
      if (big.Length != w * h) big = new int[w * h];
      for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++) {
        int c = dots[y * CW + x], a = (c >> 24) & 255, v = 0;
        if (a == 255) v = c;
        else if (a > 0) v = (a << 24) | ((((c >> 16) & 255) * a / 255) << 16) | ((((c >> 8) & 255) * a / 255) << 8) | ((c & 255) * a / 255);
        int b0 = y * ps * w + x * ps;
        for (int yy = 0; yy < ps; yy++) for (int xx = 0; xx < ps; xx++) big[b0 + yy * w + xx] = v;
      }
      surf.CopyFrom(big);
    }

    /* ---------- 마우스 ---------- */
    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle) return;
      var c = Cursor.Position;
      dragDown = true; dragActive = false;
      dragKind = e.Button == MouseButtons.Middle || (ModifierKeys & Keys.Shift) != 0 ? "carry" : "pet"; // 그냥 문지르면 쓰다듬기, Shift(또는 휠 클릭)+드래그면 들어서 옮기기
      dragSX = dragLastSX = c.X; dragSY = c.Y; dragAcc = 0;
      dragOX = px_ - c.X; dragOY = fy - c.Y;
    }
    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      if (!dragDown) return;
      var c = Cursor.Position;
      if (!dragActive && Math.Sqrt((c.X - dragSX) * (c.X - dragSX) + (c.Y - dragSY) * (c.Y - dragSY)) > 5 * U) {
        dragActive = true;
        if (dragKind == "carry") { SetMode("drag", Now, 0); vy = 0; } else petting = true;
      }
      if (dragActive && dragKind == "carry") { px_ = c.X + dragOX; fy = c.Y + dragOY; }
      else if (dragActive) {
        dragAcc += Math.Abs(c.X - dragLastSX);
        dragLastSX = c.X;
        while (dragAcc >= 36 * U) { dragAcc -= 36 * U; DoStroke(); }
      }
    }
    protected override void OnMouseUp(MouseEventArgs e) {
      base.OnMouseUp(e);
      if (e.Button == MouseButtons.Right) { app.ShowPetMenu(); return; }
      EndDrag(true);
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (dragDown && !Capture) EndDrag(false); }
    void EndDrag(bool click) {
      if (!dragDown) return;
      bool was = dragActive; string kind = dragKind;
      dragDown = false; dragActive = false; petting = false;
      double now = Now;
      if (was && kind == "carry") {
        ClampToDisplay();
        if (Behavior == "roam") { SetMode("fall", now, 0); vy = 0; }
        else { SetMode("hop", now, 450); happyUntil = now + 900; }
      } else if (!was && click) {
        happyUntil = now + 900; // 톡 건드림
        Say(Pick(new[] { "응?", "왜요~", "쓰다듬어 주세요! (문지르기)" }), 1600);
      }
    }

    /* ---------- 메인 루프 ---------- */
    void CheckNap(double now) {
      bool want = Native.IdleSeconds() >= NAP_AFTER && !dragDown;
      if (want && !Napping) { Napping = true; nextZ = now; Say("zzz…", 2500); app.NotesDirty(); }
      else if (!want && Napping) { Napping = false; happyUntil = now + 1800; Say("어서 와요!", 2600); app.NotesDirty(); }
    }
    void ScheduleTalk(double now) {
      int[] t;
      nextTalk = TALK_EVERY.TryGetValue(State.Settings.Talk, out t) ? now + Rand(t[0], t[1]) * 1000 : double.PositiveInfinity;
    }
    string PickTalk() {
      int h = DateTime.Now.Hour, hh = h < 6 ? h + 24 : h;
      var tm = TALK_TIME.FirstOrDefault(x => hh >= x.From && hh < x.To);
      double r = rnd.NextDouble(); string n = State.Plant.Name;
      if (n.Length > 0 && r < 0.15) return Pick(new[] { "내 이름은 " + n + "!", n + "~ " + n + "~ 불러봐요!", n + " 오늘도 쑥쑥 크는 중!" });
      if (tm != null && r < 0.25) return Pick(tm.Lines);
      if (r < 0.6) return Pick(TALK_CHEER);
      if (r < 0.85) return Pick(TALK_CUTE);
      return Pick(TALK_CARE);
    }
    void CheckTalk(double now) {
      if (now < nextTalk) return;
      // 말하기 곤란한 때(낮잠, 드래그, 쓰다듬는 중, 다른 말풍선이 떠 있음)는 조금 뒤로 미룸
      if (Napping || dragActive || petting || SayShown || mode == "celebrate") { nextTalk = now + 8000; return; }
      Say(PickTalk(), 3800);
      if (rnd.NextDouble() < 0.5) happyUntil = now + 1500;
      ScheduleTalk(now);
    }
    void CheckGreeting(double now) {
      if (now - lastGreetCheck < 30000) return;
      lastGreetCheck = now;
      var d = DateTime.Now; int h = d.Hour;
      var g = GREETINGS.FirstOrDefault(x => h >= x.From && h < x.To);
      if (g == null) return;
      string key = d.ToShortDateString() + g.Id;
      if (greeted.Contains(key)) return;
      greeted.Add(key);
      if (!Napping && !Logic.IsThirsty(State.Plant)) Say(g.Text, 4000);
    }

    void Tick() {
      double now = Now, dt = Math.Min(0.1, Math.Max(0.001, (now - last) / 1000));
      last = now;
      if (!Visible) { // 숨겨 둔 동안은 키우기만 하고 그리지 않아요
        Apply(Logic.Advance(State.Plant, dt * TestSpeed, GrowRate()), now);
        if (now - lastSave > SAVE_EVERY) { lastSave = now; SaveNow(); }
        timer.Interval = 1000;
        return;
      }
      cursor = Cursor.Position;
      if (now - lastDisplays > 3000) { lastDisplays = now; RefreshDisplays(); }
      var p = State.Plant;
      if (!Napping) Apply(Logic.Advance(p, dt * TestSpeed, GrowRate()), now);
      CheckNap(now);
      UpdateBehavior(dt, now);
      Ambient(dt, now);
      UpdateParticles(dt);
      CheckGreeting(now);
      CheckTalk(now);
      if (now - lastItemCheck > 1000) { lastItemCheck = now; CheckItems(now); }
      if (itemQueue.Count > 0 && !SayShown && now >= sayUntil && !Napping) Say(itemQueue.Dequeue(), 4200);
      if (Logic.IsThirsty(p) && !Napping && now >= nextThirstSay && !SayShown) {
        Say(Pick(Logic.IsDry(p) ? new[] { "바싹 말랐어요…", "물… 물 좀…" } : new[] { "목말라요…", "물 좀 주세요~" }), 3000);
        nextThirstSay = now + 90000;
      }
      if (toy != null) {
        toy.Tick(dt);
        if (toy.Finished) OnToyEnd();
      }
      Render(now, dt);
      UpdateBubble(now);
      if (now - lastSave > SAVE_EVERY) { lastSave = now; SaveNow(); }
      if (now - lastNotes > 500) { lastNotes = now; app.UpdateNotes(); }
      // 낮잠 중이고 아무 일도 없으면 천천히 돌아요
      timer.Interval = Napping && toy == null && particles.Count < 3 ? 120 : 33;
    }

    public void SetHidden(bool hidden) {
      if (hidden) {
        if (toy != null) { toy.Dispose(); toy = null; play = null; if (mode == "play") SetMode("idle", Now, 800); }
        bubble.HideBubble();
        Hide();
      } else {
        Show();
        Array.Clear(shown, 0, shown.Length);
        timer.Interval = 33;
      }
    }
    public void Shutdown() {
      timer.Stop();
      SaveNow();
      if (toy != null) toy.Dispose();
      bubble.Close();
    }
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); if (surf != null) surf.Dispose(); bubble.Dispose(); } base.Dispose(disposing); }
  }
}
