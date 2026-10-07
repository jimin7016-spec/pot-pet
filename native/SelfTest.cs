// 점검용: PotPet.exe --self-test (규칙 검사, build.ps1 -Test 가 불러요)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PotPet {
  public static class SelfTest {
    /** 모든 그림 조합 (화분·아이템·식물·친구·장난감) */
    public static IEnumerable<KeyValuePair<string, Grid>> AllSprites() {
      foreach (var skin in Sprites.SKIN_IDS)
        foreach (var face in new[] { "idle", "blink", "happy", "love", "sad", "sleep" })
          foreach (var look in new[] { -1, 0, 1 })
            foreach (var feet in new[] { 0, 1, 2 })
              yield return Kv("pot|" + skin + "|" + face + "|" + look + "|" + feet + "|", Sprites.PotGrid(skin, face, look, feet, ""));
      var wear = Logic.ITEMS.Where(i => i.Slot != "head").Select(i => i.Id).ToList();
      foreach (var a in wear) foreach (var look in new[] { -1, 0, 1 }) {
        yield return Kv("pot|sky|idle|" + look + "|0|" + a, Sprites.PotGrid("sky", "idle", look, 0, a));
        foreach (var b in wear) if (b != a) yield return Kv("pot|po|happy|" + look + "|1|" + a + "," + b, Sprites.PotGrid("po", "happy", look, 1, a + "," + b));
      }
      foreach (var sp in Logic.SPECIES)
        for (int stage = 0; stage <= 4; stage++)
          foreach (var wilt in new[] { false, true })
            for (int size = 0; size < Logic.FRIEND_SIZES; size++)
              yield return Kv("pl|" + sp.Id + "|" + stage + "|" + (wilt ? 1 : 0) + "|" + size, Sprites.PlantGrid(sp.Id, stage, wilt, size));
      foreach (var sp in Logic.SPECIES) yield return Kv("silu|" + sp.Id, Sprites.SilhouetteGrid(sp.Id));
      foreach (var it in Logic.ITEMS.Where(i => i.Slot == "head")) yield return Kv("head|" + it.Id, Sprites.HeadGrid(it.Id));
      foreach (var t in Logic.TOYS) for (int f = 0; f < 2; f++) yield return Kv("toy|" + t + "|" + f, Sprites.ToyGrid(t, f));
      yield return Kv("can", Sprites.CanGrid());
    }
    static KeyValuePair<string, Grid> Kv(string k, Grid g) { return new KeyValuePair<string, Grid>(k, g); }

    /* ---------- 규칙 검사 ---------- */
    static int n;
    static void T(string name, Action body) {
      try { body(); n++; Console.WriteLine("  ✓ " + name); }
      catch (Exception e) { Console.WriteLine("  ✗ " + name + "\n    " + e.Message); throw; }
    }
    static void Eq(object a, object b, string what = "") { if (!Equals(a, b)) throw new Exception(what + " 기대값 " + b + " / 실제 " + a); }
    static void Ok(bool v, string what = "") { if (!v) throw new Exception("실패: " + what); }
    static Func<double> Seeded(uint seed) { return () => { seed = unchecked(seed * 1664525u + 1013904223u); return seed / 4294967296.0; }; }
    static Plant Mk(double growth, double water) { return new Plant { SpeciesId = "tomato", Growth = growth, Water = water }; }

    public static int Run() {
      Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
      const double H = 3600;
      try {
        T("식물/화분 id 가 그림과 규칙에서 일치", () => {
          Ok(Sprites.SpeciesIds.OrderBy(x => x).SequenceEqual(Logic.SPECIES.Select(s => s.Id).OrderBy(x => x)), "species");
          Eq(Logic.SPECIES.Length, 17);
          Ok(Logic.SPECIES.Where(s => s.Friend).All(s => Sprites.IsFriend(s.Id)), "friend");
        });
        T("성장 단계 경계 (하루 8시간)", () => {
          Eq(Logic.StageOf(0), 0); Eq(Logic.StageOf(0.5 * H - 1), 0); Eq(Logic.StageOf(0.5 * H), 1);
          Eq(Logic.StageOf(2 * H), 2); Eq(Logic.StageOf(4 * H), 3); Eq(Logic.StageOf(8 * H), 4);
        });
        T("물이 있을 때만 자라고, 물은 3시간이면 바닥", () => {
          var p = Mk(0, 100); Logic.Advance(p, 3 * H, 1); Eq(Math.Round(p.Growth), 3 * H); Eq(p.Water, 0.0);
          Logic.Advance(p, H, 1); Eq(Math.Round(p.Growth), 3 * H);
        });
        T("물 주기: 가득일 땐 확인, 한 번 더 주면 과습", () => {
          var p = Mk(100, 95); Eq(Logic.WaterPlant(p, false).Result, "full"); Eq(Logic.WaterPlant(p, true).Result, "over"); Ok(p.Soggy);
          Eq(Logic.WaterPlant(p, false).Result, "soggy");
          p.Water = 0; var r = Logic.WaterPlant(p, false); Eq(r.Result, "ok"); Ok(r.Revived);
        });
        T("과습은 다음 단계로 자라면 회복", () => {
          var p = Mk(1800 - 10, 100); p.Soggy = true; var r = Logic.Advance(p, 100, 1); Ok(r.Recovered); Ok(!p.Soggy);
        });
        T("쓰다듬기 보너스 상한 2시간", () => {
          var p = Mk(0, 100); double total = 0; for (int i = 0; i < 1000; i++) total += Logic.Stroke(p).Gained; Eq(total, Logic.BONUS_CAP);
          Eq(Logic.Stroke(Mk(100, 0)).Gained, 0.0);
        });
        T("꺼져 있던 시간 반영, 하루 상한, 시계가 거꾸로 가도 안전", () => {
          var s = Logic.DefaultState(0, Seeded(1)); s.Plant.Water = 100; s.LastSeen = 0;
          var r = Logic.ApplyOffline(s, 2 * H * 1000); Eq(r.Elapsed, 2 * H);
          s.LastSeen = 10 * H * 1000; Eq(Logic.ApplyOffline(s, 0).Elapsed, 0.0);
        });
        T("씨앗 확률 분포 (일반 60 / 희귀 30 / 레어 10)", () => {
          var rng = Seeded(12345); var c = new int[3]; const int N = 40000;
          for (int i = 0; i < N; i++) c[Logic.RollSpecies(rng).Rarity]++;
          Ok(Math.Abs(c[0] * 100.0 / N - 60) < 2 && Math.Abs(c[1] * 100.0 / N - 30) < 2 && Math.Abs(c[2] * 100.0 / N - 10) < 1.5, string.Join(",", c));
          var seen = new HashSet<string>(); for (int i = 0; i < 4000; i++) seen.Add(Logic.RollSpecies(rng).Id); Eq(seen.Count, Logic.SPECIES.Length);
        });
        T("친구 크기: 공개 뒤 1시간마다 커짐", () => {
          var hs = new[] { 3.5, 4, 4.9, 5, 6, 7, 7.99, 8 }; var want = new[] { 0, 0, 0, 1, 2, 3, 3, 4 };
          for (int i = 0; i < hs.Length; i++) Eq(Logic.FriendSize(new Plant { Growth = hs[i] * H }), want[i], hs[i] + "h");
        });
        T("아이템: 조건, 한 번만, 같은 칸은 하나만", () => {
          var s = Logic.DefaultState(0, Seeded(1));
          Eq(Logic.CheckItems(s, 5).Count, 0);
          s.Stats["harvests"] = 1; s.Stats["named"] = 1; s.Stats["replants"] = 1;
          Eq(string.Join(",", Logic.CheckItems(s, 5).Select(i => i.Id).OrderBy(x => x, StringComparer.Ordinal)), "glasses,ribbon,scarf");
          Eq(Logic.CheckItems(s, 6).Count, 0);
          Ok(Logic.EquipItem(s, "ribbon")); Eq(s.Equipped["head"], "ribbon"); Ok(Logic.EquipItem(s, "ribbon")); Eq(s.Equipped["head"], null);
          Ok(!Logic.EquipItem(s, "crown")); Ok(!Logic.EquipItem(s, "nope"));
          s.Collection["mint"] = new DexEntry { Count = 1, Skin = "po" };
          Ok(Logic.CheckItems(s, 7).Any(i => i.Id == "ant_po"), "ant_po");
        });
        T("이름: 공백 정리, 8글자, 이상한 문자 제거", () => {
          Eq(Logic.CleanName("  콩   이  "), "콩 이"); Eq(Logic.CleanName("아주아주아주긴이름입니다").Length, Logic.NAME_MAX);
          Eq(Logic.CleanName("<b>\"x\"&</b>"), "bx/b"); Eq(Logic.CleanName(123), "");
        });
        T("저장 데이터: 왕복해도 같고, 망가진 값·옛 이름은 고쳐서 열림", () => {
          var s = Logic.DefaultState(0, Seeded(2));
          s.Owned["ribbon"] = 5; s.Equipped["head"] = "ribbon"; s.Settings.Speed = 3; s.Plant.Name = "두부"; s.Stats["plays"] = 7; s.Nutri = new Nutri { Until = 9, ReadyAt = 99 };
          s.Collection["mint"] = new DexEntry { Count = 2, Skin = "po", First = 3, Name = "콩" };
          var json = Store.Serialize(Logic.ToJson(s));
          var r = Logic.NormalizeState(Store.Parse(json), 1, Seeded(1));
          Eq(Store.Serialize(Logic.ToJson(r)), json);
          var bad = Logic.NormalizeState(Store.Parse("{\"plant\":{\"speciesId\":\"birdie\",\"growth\":99999999,\"water\":-5},\"collection\":{\"birdie\":{\"count\":2,\"skin\":\"nope\"},\"fake\":{\"count\":9}},\"settings\":{\"scale\":7,\"skin\":\"rose\",\"x\":12,\"y\":\"a\"},\"items\":{\"equipped\":{\"neck\":\"crown\"}}}"), 1, Seeded(1));
          Eq(bad.Plant.SpeciesId, "jjio"); Eq(bad.Plant.Growth, Logic.BLOOM_AT); Eq(bad.Plant.Water, 0.0);
          Eq(string.Join(",", bad.Collection.Keys), "jjio"); Eq(bad.Collection["jjio"].Skin, "terra");
          Eq(bad.Settings.Scale, 3); Eq(bad.Settings.Skin, "rose"); Eq(bad.Settings.X, (double?)12); Eq(bad.Settings.Y, null); Eq(bad.Equipped["neck"], null);
          Ok(Logic.NormalizeState(null, 1, Seeded(1)).Plant != null);
        });
        T("모든 그림이 그려진다", () => {
          int k = 0; foreach (var kv in AllSprites()) { Ok(kv.Value.D.Count(c => c != 0) > 8, kv.Key); k++; }
          Ok(k > 1000, "count " + k);
        });
      } catch { return 1; }
      Console.WriteLine("\n" + n + "개 검사 통과");
      return 0;
    }
  }
}
