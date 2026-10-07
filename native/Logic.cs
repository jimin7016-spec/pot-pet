// 게임 규칙 (화면과 무관한 순수 로직)
// 여기 숫자만 바꿔도 게임 느낌이 바뀌어요. 저장 파일 모양은 예전 Electron 버전과 같아서 그때 키우던 화분도 그대로 열려요.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PotPet {
  public class Rarity { public string Id, Label; public int Weight, Stars; }
  public class Species { public string Id, Name; public int Rarity; public bool Friend; }
  public class Item { public string Id, Slot, Name, How; public Func<GameState, bool> Test; }

  public class Plant {
    public string SpeciesId = "tomato", Name = "";
    public double Growth, Water = 70, Love, Bonus, Play, PlantedAt;
    public bool Soggy;
  }
  public class DexEntry { public int Count; public string Skin = "terra", Name = ""; public double First; }
  public class Nutri { public double Until, ReadyAt; }
  public class Settings {
    public int Scale = 3, Speed = 1;
    public string Skin = "terra", Mode = "free", Talk = "sometimes";
    public double? X, Y;
  }
  public class GameState {
    public int V = 3;
    public Plant Plant = new Plant();
    public Dictionary<string, DexEntry> Collection = new Dictionary<string, DexEntry>();
    public Nutri Nutri = new Nutri();
    public Dictionary<string, int> Stats = Logic.NewStats();
    public Dictionary<string, double> Owned = new Dictionary<string, double>();
    public Dictionary<string, string> Equipped = new Dictionary<string, string> { { "head", null }, { "face", null }, { "neck", null } };
    public Settings Settings = new Settings();
    public double LastSeen;
  }
  public class StepResult { public int PrevStage, Stage; public bool FinishedNow, Recovered; public double Gained; public double Elapsed; public bool IsNewSpecies; }
  public class ActResult { public string Result; public bool Revived; public double WaitMs; }

  public static class Logic {
    /* ---------- 튜닝 값 ---------- */
    public const double HOUR = 3600;
    public static readonly double[] STAGE_AT = { 0, 0.5 * HOUR, 2 * HOUR, 4 * HOUR, 8 * HOUR }; // 각 단계에 들어가는 '물 먹고 자란 시간'(초). 8시간 = 하루
    public static readonly string[] STAGE_NAMES = { "씨앗", "새싹", "쑥쑥 자라는 중", "모양이 잡혔어요", "수확 완료" };
    public static readonly double BLOOM_AT = STAGE_AT[4];
    public const int REVEAL_STAGE = 3; // 이 단계부터 무슨 식물인지 알려줘요
    public const double WATER_MAX = 100;
    public const double WATER_DRAIN = WATER_MAX / (3 * HOUR); // 물 한 번이면 3시간
    public const double WATER_PER_POUR = 100;
    public const double WATER_FULL_ABOVE = 90; // 이 이상일 때 물을 또 주면 과습(시들어요)
    public const double SOGGY_RATE = 0.5; // 과습인 동안 성장 속도. 다음 단계에 오르면 회복해요
    public const double THIRSTY_BELOW = 25;
    public const double OFFLINE_CAP = 24 * HOUR; // 꺼져 있던 시간은 최대 하루치만 반영
    public const double LOVE_MAX = 100;
    public const double LOVE_PER_STROKE = 5;
    public const double BONUS_SMALL = 80; // 쓰다듬을 때마다 성장하는 초
    public const double BONUS_BIG = 240; // 애정이 가득 찼을 때
    public const double BONUS_CAP = 2 * HOUR; // 식물 한 개가 쓰다듬기로 얻을 수 있는 최대 성장 시간
    public const double PLAY_LOVE = 4; // 장난감을 한 번 받아칠 때 애정
    public const double PLAY_GROW = 40; // 장난감을 한 번 받아칠 때 성장하는 초
    public const double PLAY_CAP = HOUR; // 식물 한 개가 놀이로 얻을 수 있는 최대 성장 시간
    public const double NUTRI_MS = HOUR * 1000; // 영양제 효과 시간
    public const double NUTRI_COOLDOWN_MS = 2 * HOUR * 1000; // 먹인 뒤 다음 영양제까지
    public const double NUTRI_RATE = 1.5; // 영양제 효과 동안 성장 속도
    public static readonly int[] SPEEDS = { 1, 2, 3 }; // 성장 배속 (반차·외근 등 자리를 비우는 날용). 물 마르는 속도는 그대로
    public const int NAME_MAX = 8;
    public static readonly string[] TOYS = { "ball", "yarn", "butterfly", "bubble" };

    public static readonly Rarity[] RARITY = {
      new Rarity { Id = "common", Label = "일반", Weight = 60, Stars = 1 },
      new Rarity { Id = "uncommon", Label = "희귀", Weight = 30, Stars = 2 },
      new Rarity { Id = "rare", Label = "레어", Weight = 10, Stars = 3 },
    };
    static Species S(string id, string name, int rarity, bool friend = false) { return new Species { Id = id, Name = name, Rarity = rarity, Friend = friend }; }
    public static readonly Species[] SPECIES = {
      S("tomato", "방울토마토", 0), S("basil", "바질", 0), S("cactus", "선인장", 0), S("sunflower", "해바라기", 0), S("lettuce", "상추", 0), S("mint", "민트", 0),
      S("strawberry", "딸기", 1), S("lavender", "라벤더", 1),
      S("purplebasil", "보라 바질", 2), S("goldtomato", "황금 방울토마토", 2),
      // 친구: 식물처럼 씨앗에서 자라지만, 정체가 드러나면 흙에서 캐릭터가 나와 점점 커져요
      S("whitepup", "흰 강아지", 0, true), S("hamster", "햄스터", 0, true),
      S("beaver", "분홍 비버", 1, true), S("jjio", "찌오", 1, true), S("jjidung", "찌둥", 1, true),
      S("beagle", "비글", 2, true), S("blob", "보라 친구", 2, true),
    };
    public const int FRIEND_SIZES = 5; // 친구 몸 크기 단계 (정체 공개 ~ 수확 사이에 점점 커짐)
    public static readonly string[] SKIN_IDS = Sprites.SKIN_IDS;
    public static readonly string[] MODES = { "free", "roam", "follow", "stay" }; // 화면 전체 / 작업표시줄 위 / 커서 따라가기 / 가만히
    public static readonly int[] SCALES = { 2, 3, 4 };
    public static readonly string[] TALKS = { "often", "sometimes", "quiet" }; // 혼잣말(응원) 빈도: 자주 / 가끔 / 조용히

    /* ---------- 유틸 ---------- */
    public static double Clamp(double v, double a, double b) { return Math.Min(b, Math.Max(a, v)); }
    public static Species SpeciesById(string id) { return SPECIES.FirstOrDefault(s => s.Id == id) ?? SPECIES[0]; }
    public static Rarity RarityOf(string id) { return RARITY[SpeciesById(id).Rarity]; }
    public static bool IsFriend(string id) { return SpeciesById(id).Friend; }

    /* ---------- 식물 ---------- */
    public static Species RollSpecies(Func<double> rng) {
      double x = rng() * RARITY.Sum(r => r.Weight);
      int tier = 0;
      for (int i = 0; i < RARITY.Length; i++) {
        if (x < RARITY[i].Weight) { tier = i; break; }
        x -= RARITY[i].Weight;
      }
      var list = SPECIES.Where(s => s.Rarity == tier).ToArray();
      return list[Math.Min(list.Length - 1, (int)Math.Floor(rng() * list.Length))];
    }
    public static Plant NewPlant(Func<double> rng, double now) {
      return new Plant { SpeciesId = RollSpecies(rng).Id, Water = 70, PlantedAt = now };
    }
    public static int StageOf(double growth) {
      int s = 0;
      for (int i = 0; i < STAGE_AT.Length; i++) if (growth >= STAGE_AT[i]) s = i;
      return s;
    }
    /** 남은 시간(초): 물이 계속 있다는 가정. 과습이면 성장이 절반 속도라 두 배로 걸려요. */
    public static double[] TimeLeft(Plant p, double rate) {
      if (p.Growth >= BLOOM_AT) return new double[] { 0, 0 };
      double k = (p.Soggy ? 1 / SOGGY_RATE : 1) / (rate > 0 ? rate : 1); int st = StageOf(p.Growth);
      return new[] { (BLOOM_AT - p.Growth) * k, (STAGE_AT[st + 1] - p.Growth) * k };
    }
    public static string FmtLeft(double sec) {
      int m = Math.Max(1, (int)Math.Ceiling(sec / 60));
      int h = m / 60, mm = m % 60;
      return h > 0 ? (mm > 0 ? h + "시간 " + mm + "분" : h + "시간") : mm + "분";
    }
    public static bool IsDone(Plant p) { return p.Growth >= BLOOM_AT; }
    public static bool IsDry(Plant p) { return p.Water <= 0 && !IsDone(p); }
    public static bool IsSoggy(Plant p) { return p.Soggy && !IsDone(p); }
    public static bool IsWilted(Plant p) { return (IsDry(p) || IsSoggy(p)) && StageOf(p.Growth) >= 1; } // 고개를 숙인 모습 (물 부족 또는 과습)
    public static bool IsThirsty(Plant p) { return p.Water < THIRSTY_BELOW && !IsDone(p); }
    public static bool IsRevealed(Plant p) { return StageOf(p.Growth) >= REVEAL_STAGE; }
    /** 친구 몸 크기 0 ~ FRIEND_SIZES-1. 정체 공개 때 0, 이후 1시간마다 한 칸씩, 수확하면 최대 */
    public static int FriendSize(Plant p) {
      if (p.Growth >= BLOOM_AT) return FRIEND_SIZES - 1;
      double t = (p.Growth - STAGE_AT[REVEAL_STAGE]) / (BLOOM_AT - STAGE_AT[REVEAL_STAGE]);
      return t <= 0 ? 0 : Math.Min(FRIEND_SIZES - 2, (int)Math.Floor(t * (FRIEND_SIZES - 1)));
    }

    static StepResult Result(Plant p, int prevStage) {
      int stage = StageOf(p.Growth);
      bool recovered = false;
      if (p.Soggy && stage > prevStage) { p.Soggy = false; recovered = true; } // 과습은 다음 단계로 자라면 회복
      return new StepResult { PrevStage = prevStage, Stage = stage, FinishedNow = prevStage < 4 && IsDone(p), Recovered = recovered };
    }

    /** sec(가상 초)만큼 시간을 흘려보낸다. 물이 있는 동안만 자라고, 다 자라면 그 모습 그대로 멈춘다. */
    public static StepResult Advance(Plant p, double sec, double rate) {
      int prev = StageOf(p.Growth);
      if (!(sec > 0) || IsDone(p)) return Result(p, prev);
      double wet = Math.Min(sec, p.Water / WATER_DRAIN);
      p.Growth = Math.Min(BLOOM_AT, p.Growth + wet * (rate > 0 ? rate : 1) * (p.Soggy ? SOGGY_RATE : 1));
      p.Water = Math.Max(0, p.Water - WATER_DRAIN * sec);
      return Result(p, prev);
    }

    public static ActResult WaterPlant(Plant p, bool force) {
      if (IsDone(p)) return new ActResult { Result = "done" };
      if (p.Soggy && p.Water >= THIRSTY_BELOW) return new ActResult { Result = "soggy" }; // 과습: 물이 모자라지기 전엔 더 못 줘요
      if (p.Water >= WATER_FULL_ABOVE && !force) return new ActResult { Result = "full" };
      if (p.Water >= WATER_FULL_ABOVE) { p.Water = WATER_MAX; p.Soggy = true; return new ActResult { Result = "over" }; } // 물을 너무 많이 줌
      bool wasDry = p.Water <= 0;
      p.Water = Math.Min(WATER_MAX, p.Water + WATER_PER_POUR);
      return new ActResult { Result = "ok", Revived = wasDry };
    }

    /** 쓰다듬기 한 번. 애정이 차고, 물이 있으면 성장이 조금 빨라진다(식물 한 개당 상한 있음). */
    public static StepResult Stroke(Plant p) {
      int prev = StageOf(p.Growth);
      if (IsDone(p)) return Result(p, prev);
      p.Love = Math.Min(LOVE_MAX, p.Love + LOVE_PER_STROKE);
      double gained = 0;
      if (p.Water > 0 && p.Bonus < BONUS_CAP) {
        gained = Math.Min(Math.Min(p.Love >= LOVE_MAX ? BONUS_BIG : BONUS_SMALL, BONUS_CAP - p.Bonus), BLOOM_AT - p.Growth);
        p.Bonus += gained;
        p.Growth += gained;
      }
      var r = Result(p, prev); r.Gained = gained; return r;
    }

    /** 장난감을 받아쳤을 때. 애정이 조금 오르고, 물이 있으면 성장도 조금(식물 한 개당 상한 있음). */
    public static StepResult PlayHit(Plant p) {
      int prev = StageOf(p.Growth);
      if (IsDone(p)) return Result(p, prev);
      p.Love = Math.Min(LOVE_MAX, p.Love + PLAY_LOVE);
      double gained = 0;
      if (p.Water > 0 && p.Play < PLAY_CAP) {
        gained = Math.Min(Math.Min(PLAY_GROW, PLAY_CAP - p.Play), BLOOM_AT - p.Growth);
        p.Play += gained;
        p.Growth += gained;
      }
      var r = Result(p, prev); r.Gained = gained; return r;
    }

    /* ---------- 영양제 ---------- */
    public static double NutriRate(GameState s, double now) { return now < s.Nutri.Until ? NUTRI_RATE : 1; }
    /** 영양제 먹이기. now = 실제 시각(ms). 과습이거나 쿨타임이면 못 먹여요. */
    public static ActResult UseNutri(GameState s, double now) {
      var p = s.Plant;
      if (IsDone(p)) return new ActResult { Result = "done" };
      if (p.Soggy) return new ActResult { Result = "soggy" };
      if (now < s.Nutri.ReadyAt) return new ActResult { Result = "cooldown", WaitMs = s.Nutri.ReadyAt - now };
      s.Nutri = new Nutri { Until = now + NUTRI_MS, ReadyAt = now + NUTRI_COOLDOWN_MS };
      return new ActResult { Result = "ok" };
    }

    /* ---------- 이름 ---------- */
    public static string CleanName(object raw) {
      var s = raw as string;
      if (s == null) return "";
      s = Regex.Replace(s, "[\u0000-\u001f\u007f<>&\"]", "");
      s = Regex.Replace(s, "\\s+", " ").Trim();
      var cps = new List<string>();
      for (int i = 0; i < s.Length && cps.Count < NAME_MAX; i++) {
        if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { cps.Add(s.Substring(i, 2)); i++; } else cps.Add(s[i].ToString());
      }
      return string.Concat(cps).Trim();
    }

    /* ---------- 꾸미기 아이템 (화분 색과 상관없이 모든 화분에 어울려요) ---------- */
    public static readonly string[] SLOTS = { "head", "face", "neck" };
    static Item I(string id, string slot, string name, string how, Func<GameState, bool> test) { return new Item { Id = id, Slot = slot, Name = name, How = how, Test = test }; }
    public static readonly Item[] ITEMS = {
      I("ribbon", "head", "빨간 리본", "첫 씨앗을 수확해요", s => s.Stats["harvests"] >= 1),
      I("flowerpin", "head", "꽃핀", "도감 2종 채우기", s => DexCount(s) >= 2),
      I("crown", "head", "반짝 왕관", "도감 5종 채우기", s => DexCount(s) >= 5),
      I("glasses", "face", "동그란 안경", "처음으로 화분을 갈아엎어요", s => s.Stats["replants"] >= 1),
      I("shades", "face", "선글라스", "과습에서 처음 회복해요", s => s.Stats["recovers"] >= 1),
      I("scarf", "neck", "목도리", "식물에게 이름을 지어줘요", s => s.Stats["named"] >= 1),
      I("bowtie", "neck", "나비넥타이", "장난감으로 10번 놀아줘요", s => s.Stats["plays"] >= 10),
      I("bell", "neck", "방울 목걸이", "애정을 가득 채워요", s => s.Stats["lovefull"] >= 1),
      I("strawhat", "head", "밀짚모자", "물을 30번 줘요", s => s.Stats["waters"] >= 30),
      I("beret", "head", "베레모", "5번 수확해요", s => s.Stats["harvests"] >= 5),
      I("wreath", "head", "꽃 화관", "도감을 전부 채워요", s => DexCount(s) >= SPECIES.Length),
      I("heartglasses", "face", "하트 안경", "애정을 5번 가득 채워요", s => s.Stats["lovefull"] >= 5),
      I("monocle", "face", "외알 안경", "레어 식물을 수확해요", s => DexHasStars(s, 3)),
      I("mustache", "face", "콧수염", "영양제를 5번 먹여요", s => s.Stats["nutris"] >= 5),
      I("pearl", "neck", "진주 목걸이", "희귀 식물을 수확해요", s => DexHasStars(s, 2)),
      I("lei", "neck", "꽃 목걸이", "이름 지어준 식물을 수확해요", s => s.Collection.Values.Any(c => c != null && !string.IsNullOrEmpty(c.Name))),
      I("medal", "neck", "금메달", "장난감으로 30번 놀아줘요", s => s.Stats["plays"] >= 30),
      I("ant_tinky", "head", "보라돌이 안테나", "보라돌이 화분으로 수확해요", s => HarvestedIn(s, "tinky")),
      I("ant_dipsy", "head", "뚜비 안테나", "뚜비 화분으로 수확해요", s => HarvestedIn(s, "dipsy")),
      I("ant_laalaa", "head", "나나 안테나", "나나 화분으로 수확해요", s => HarvestedIn(s, "laalaa")),
      I("ant_po", "head", "뽀 안테나", "뽀 화분으로 수확해요", s => HarvestedIn(s, "po")),
    };
    static bool HarvestedIn(GameState s, string skin) { return s.Collection.Values.Any(c => c != null && c.Skin == skin); }
    public static Item ItemById(string id) { return ITEMS.FirstOrDefault(i => i.Id == id); }
    public static int DexCount(GameState s) { return s.Collection.Count; }
    static bool DexHasStars(GameState s, int stars) { return s.Collection.Keys.Any(id => RarityOf(id).Stars >= stars); }
    /** 조건을 채운 아이템을 새로 얻는다. 새로 얻은 아이템 목록을 돌려줌 */
    public static List<Item> CheckItems(GameState s, double now) {
      var got = new List<Item>();
      foreach (var it in ITEMS) if (!s.Owned.ContainsKey(it.Id) && it.Test(s)) { s.Owned[it.Id] = now; got.Add(it); }
      return got;
    }
    /** 같은 아이템을 다시 누르면 벗기, 아니면 그 칸에 착용 */
    public static bool EquipItem(GameState s, string id) {
      var it = ItemById(id);
      if (it == null || !s.Owned.ContainsKey(id)) return false;
      s.Equipped[it.Slot] = s.Equipped[it.Slot] == id ? null : id;
      return true;
    }

    /* ---------- 저장 데이터 ---------- */
    public static Dictionary<string, int> NewStats() {
      var d = new Dictionary<string, int>();
      foreach (var k in new[] { "harvests", "replants", "recovers", "named", "plays", "lovefull", "waters", "nutris" }) d[k] = 0;
      return d;
    }
    public static GameState DefaultState(double now, Func<double> rng) {
      return new GameState { Plant = NewPlant(rng, now), LastSeen = now };
    }

    static readonly Dictionary<string, string> RENAMED = new Dictionary<string, string> { { "birdie", "jjio" } }; // 없어진 종류 → 이어받는 종류 (저장본 호환)
    static double Num(object v, double d) {
      if (v is int || v is long || v is double || v is decimal || v is float) { double x = Convert.ToDouble(v); return double.IsNaN(x) || double.IsInfinity(x) ? d : x; }
      return d;
    }
    static Dictionary<string, object> Obj(object v) { return v as Dictionary<string, object>; }
    static object Get(Dictionary<string, object> o, string k) { object v; return o != null && o.TryGetValue(k, out v) ? v : null; }

    /** JSON 으로 읽은 저장본(Dictionary)을 안전한 GameState 로. 망가진 값은 기본값으로 */
    public static GameState NormalizeState(object rawObj, double now, Func<double> rng) {
      var s = DefaultState(now, rng);
      var raw = Obj(rawObj);
      if (raw == null) return s;
      var p = Obj(Get(raw, "plant"));
      string pid = Get(p, "speciesId") as string;
      if (pid != null && RENAMED.ContainsKey(pid)) pid = RENAMED[pid];
      if (p != null && SPECIES.Any(x => x.Id == pid)) {
        s.Plant = new Plant {
          SpeciesId = pid,
          Growth = Clamp(Num(Get(p, "growth"), 0), 0, BLOOM_AT),
          Water = Clamp(Num(Get(p, "water"), 70), 0, WATER_MAX),
          Love = Clamp(Num(Get(p, "love"), 0), 0, LOVE_MAX),
          Bonus = Clamp(Num(Get(p, "bonus"), 0), 0, BONUS_CAP),
          Play = Clamp(Num(Get(p, "play"), 0), 0, PLAY_CAP),
          Name = CleanName(Get(p, "name")),
          Soggy = Get(p, "soggy") is bool && (bool)Get(p, "soggy"),
          PlantedAt = Num(Get(p, "plantedAt"), now),
        };
      }
      var col = Obj(Get(raw, "collection"));
      if (col != null) {
        col = new Dictionary<string, object>(col);
        foreach (var kv in RENAMED) if (col.ContainsKey(kv.Key) && !col.ContainsKey(kv.Value)) { col[kv.Value] = col[kv.Key]; col.Remove(kv.Key); }
        foreach (var key in col.Keys) {
          var sp = SPECIES.FirstOrDefault(x => x.Id == key); var c = Obj(col[key]);
          if (sp == null || c == null || !(Num(Get(c, "count"), 0) > 0)) continue;
          string skin = Get(c, "skin") as string;
          s.Collection[sp.Id] = new DexEntry { Count = (int)Math.Floor(Num(Get(c, "count"), 0)), Skin = SKIN_IDS.Contains(skin) ? skin : "terra", First = Num(Get(c, "first"), now), Name = CleanName(Get(c, "name")) };
        }
      }
      var nu = Obj(Get(raw, "nutri"));
      if (nu != null) s.Nutri = new Nutri { Until = Num(Get(nu, "until"), 0), ReadyAt = Num(Get(nu, "readyAt"), 0) };
      var st = Obj(Get(raw, "stats"));
      if (st != null) foreach (var k in s.Stats.Keys.ToList()) s.Stats[k] = (int)Math.Max(0, Math.Floor(Num(Get(st, k), 0)));
      var items = Obj(Get(raw, "items"));
      if (items != null) {
        var o = Obj(Get(items, "owned")); var e = Obj(Get(items, "equipped"));
        foreach (var it in ITEMS) { double v = Num(Get(o, it.Id), double.NaN); if (!double.IsNaN(v)) s.Owned[it.Id] = v; }
        foreach (var sl in SLOTS) { var it = ItemById(Get(e, sl) as string); if (it != null && it.Slot == sl && s.Owned.ContainsKey(it.Id)) s.Equipped[sl] = it.Id; }
      }
      var se = Obj(Get(raw, "settings"));
      if (se != null) {
        var speed = Get(se, "speed"); if (speed is int && SPEEDS.Contains((int)speed)) s.Settings.Speed = (int)speed;
        var scale = Get(se, "scale"); if (scale is int && SCALES.Contains((int)scale)) s.Settings.Scale = (int)scale;
        var skin = Get(se, "skin") as string; if (SKIN_IDS.Contains(skin)) s.Settings.Skin = skin;
        var mode = Get(se, "mode") as string; if (MODES.Contains(mode)) s.Settings.Mode = mode;
        var talk = Get(se, "talk") as string; if (TALKS.Contains(talk)) s.Settings.Talk = talk;
        if (!double.IsNaN(Num(Get(se, "x"), double.NaN))) s.Settings.X = Num(Get(se, "x"), 0);
        if (!double.IsNaN(Num(Get(se, "y"), double.NaN))) s.Settings.Y = Num(Get(se, "y"), 0);
      }
      s.LastSeen = Num(Get(raw, "lastSeen"), now);
      return s;
    }

    /** 저장용 JSON 모양 (예전 버전과 같음) */
    public static Dictionary<string, object> ToJson(GameState s) {
      var p = s.Plant;
      var col = new Dictionary<string, object>();
      foreach (var kv in s.Collection) col[kv.Key] = new Dictionary<string, object> { { "count", kv.Value.Count }, { "skin", kv.Value.Skin }, { "first", kv.Value.First }, { "name", kv.Value.Name } };
      var owned = new Dictionary<string, object>();
      foreach (var kv in s.Owned) owned[kv.Key] = kv.Value;
      var eq = new Dictionary<string, object>();
      foreach (var sl in SLOTS) eq[sl] = s.Equipped[sl];
      var stats = new Dictionary<string, object>();
      foreach (var kv in s.Stats) stats[kv.Key] = kv.Value;
      return new Dictionary<string, object> {
        { "v", 3 },
        { "plant", new Dictionary<string, object> { { "speciesId", p.SpeciesId }, { "growth", p.Growth }, { "water", p.Water }, { "love", p.Love }, { "bonus", p.Bonus }, { "play", p.Play }, { "name", p.Name }, { "soggy", p.Soggy }, { "plantedAt", p.PlantedAt } } },
        { "collection", col },
        { "nutri", new Dictionary<string, object> { { "until", s.Nutri.Until }, { "readyAt", s.Nutri.ReadyAt } } },
        { "stats", stats },
        { "items", new Dictionary<string, object> { { "owned", owned }, { "equipped", eq } } },
        { "settings", new Dictionary<string, object> { { "scale", s.Settings.Scale }, { "skin", s.Settings.Skin }, { "mode", s.Settings.Mode }, { "talk", s.Settings.Talk }, { "speed", s.Settings.Speed }, { "x", s.Settings.X }, { "y", s.Settings.Y } } },
        { "lastSeen", s.LastSeen },
      };
    }

    /** 다 자라면 도감에 등록. 처음 보는 식물이면 true */
    public static bool RegisterHarvest(GameState s, double now) {
      string id = s.Plant.SpeciesId;
      DexEntry c; s.Collection.TryGetValue(id, out c);
      string nm = CleanName(s.Plant.Name);
      s.Collection[id] = new DexEntry { Count = (c != null ? c.Count : 0) + 1, Skin = s.Settings.Skin, First = c != null ? c.First : now, Name = nm.Length > 0 ? nm : (c != null ? c.Name : "") ?? "" };
      s.Stats["harvests"] += 1;
      return c == null;
    }

    /** 앱이 꺼져 있던 시간만큼 식물을 키운다 */
    public static StepResult ApplyOffline(GameState s, double now) {
      double elapsed = Clamp((now - s.LastSeen) / 1000, 0, OFFLINE_CAP);
      var r = Advance(s.Plant, elapsed, s.Settings.Speed);
      r.Elapsed = elapsed;
      r.IsNewSpecies = r.FinishedNow && RegisterHarvest(s, now);
      s.LastSeen = now;
      return r;
    }
  }
}
