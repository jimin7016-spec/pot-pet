'use strict';
// 게임 규칙 검사:  node tools/test-logic.js
const assert = require('assert');
const L = require('../renderer/logic.js');
const S = require('../renderer/sprites.js');

let n = 0;
const t = (name, fn) => { fn(); n++; console.log('  ✓', name); };
const seeded = (seed) => () => ((seed = (seed * 1664525 + 1013904223) % 4294967296) / 4294967296);
const mk = (o) => Object.assign({ speciesId: 'tomato', growth: 0, water: 70, love: 0, bonus: 0, soggy: false, plantedAt: 0 }, o);

t('식물/화분 id 가 sprites 와 logic 에서 일치', () => {
  assert.deepStrictEqual([...S.SPECIES_IDS].sort(), L.SPECIES.map((s) => s.id).sort());
  assert.deepStrictEqual([...S.SKIN_IDS].sort(), [...L.SKIN_IDS].sort());
  assert.strictEqual(L.SPECIES.length, 10);
});

t('성장 단계 경계 (하루 8시간)', () => {
  const h = 3600;
  assert.strictEqual(L.stageOf(0), 0);
  assert.strictEqual(L.stageOf(0.5 * h - 1), 0);
  assert.strictEqual(L.stageOf(0.5 * h), 1);
  assert.strictEqual(L.stageOf(2 * h), 2);
  assert.strictEqual(L.stageOf(4 * h), 3);
  assert.strictEqual(L.stageOf(8 * h), 4);
  assert.strictEqual(L.BLOOM_AT, 8 * h);
});

t('정체는 3단계부터 공개', () => {
  assert.strictEqual(L.isRevealed(mk({ growth: 4 * 3600 - 1 })), false);
  assert.strictEqual(L.isRevealed(mk({ growth: 4 * 3600 })), true);
});

t('물은 한 번에 3시간, 물이 있는 동안만 자란다', () => {
  const p = mk({ water: 100 });
  L.advance(p, 3 * 3600);
  assert.ok(Math.abs(p.growth - 3 * 3600) < 1e-6);
  assert.ok(p.water < 1e-6);
  const before = p.growth;
  L.advance(p, 5 * 3600); // 물 없이 5시간
  assert.strictEqual(p.growth, before);
  assert.ok(L.isDry(p) && L.isWilted(p));
});

t('물 주기: 가득 차고, 다 컸으면 거절, 시든 걸 살리면 revived', () => {
  const p = mk({ water: 0, growth: 3600 });
  const r = L.waterPlant(p);
  assert.deepStrictEqual([r.result, r.revived, p.water, p.soggy], ['ok', true, 100, false]);
  const q = mk({ growth: L.BLOOM_AT, water: 10 });
  assert.strictEqual(L.waterPlant(q).result, 'done');
  assert.strictEqual(q.soggy, false);
});

t('물을 너무 많이 주면(90 이상) 과습: 시들고, 이미 과습이면 더 못 줌', () => {
  const p = mk({ water: 95, growth: 3600 });
  assert.strictEqual(L.waterPlant(p).result, 'full'); // 먼저 경고만, 아무것도 안 바뀜
  assert.ok(!p.soggy && p.water === 95);
  assert.strictEqual(L.waterPlant(p, true).result, 'over');
  assert.ok(p.soggy && L.isSoggy(p) && L.isWilted(p));
  assert.strictEqual(p.water, 100);
  assert.strictEqual(L.waterPlant(p).result, 'soggy');
  p.water = 10; // 과습이어도 물이 모자라면 다시 줄 수 있어요
  assert.strictEqual(L.waterPlant(p).result, 'ok');
  assert.strictEqual(p.water, 100);
  p.water = 100;
  assert.strictEqual(L.isDry(p), false);
  // 89 까지는 괜찮음
  const q = mk({ water: 89, growth: 3600 });
  assert.strictEqual(L.waterPlant(q).result, 'ok');
  assert.strictEqual(q.soggy, false);
});

t('과습 동안은 절반 속도로 자라고, 다음 단계에 오르면 회복', () => {
  const p = mk({ water: 100, growth: 3600 - 100, soggy: true }); // 새싹(1800~) 단계 안
  const before = p.growth;
  L.advance(p, 100);
  assert.ok(Math.abs(p.growth - (before + 50)) < 1e-6, 'half speed ' + p.growth);
  assert.ok(p.soggy && L.isWilted(p));
  // 다음 단계(2시간=7200)까지 올라가면 회복
  const r = L.advance(p, 2 * (7200 - p.growth) + 1);
  assert.strictEqual(r.recovered, true);
  assert.strictEqual(p.soggy, false);
  assert.strictEqual(L.isWilted(p), false);
  assert.strictEqual(r.stage, 2);
  assert.strictEqual(L.advance(p, 10).recovered, false);
});

t('쓰다듬기로 다음 단계가 되면 과습도 회복, 다 자라면 과습 표시 없음', () => {
  const p = mk({ water: 100, growth: 1800 - 10, soggy: true });
  const r = L.stroke(p);
  assert.strictEqual(r.stage, 1);
  assert.strictEqual(r.recovered, true);
  assert.strictEqual(p.soggy, false);
  const d = mk({ growth: L.BLOOM_AT, soggy: true });
  assert.strictEqual(L.isSoggy(d), false);
  assert.strictEqual(L.isWilted(d), false);
});

t('다 자라면 finishedNow 는 딱 한 번, 이후 시간은 멈춤', () => {
  const p = mk({ growth: L.BLOOM_AT - 5, water: 100 });
  assert.strictEqual(L.advance(p, 3).finishedNow, false);
  assert.strictEqual(L.advance(p, 3).finishedNow, true);
  const w = p.water;
  assert.strictEqual(L.advance(p, 100000).finishedNow, false);
  assert.strictEqual(p.water, w);
  assert.ok(!L.isThirsty(p) && !L.isDry(p));
});

t('쓰다듬기: 애정이 차고 성장 보너스, 가득 차면 큰 보너스', () => {
  const p = mk({ water: 100, growth: 1000 });
  let r = L.stroke(p);
  assert.strictEqual(p.love, L.LOVE_PER_STROKE);
  assert.strictEqual(r.gained, L.BONUS_SMALL);
  assert.strictEqual(p.growth, 1000 + L.BONUS_SMALL);
  for (let i = 0; i < 25; i++) L.stroke(p);
  assert.strictEqual(p.love, L.LOVE_MAX);
  r = L.stroke(p);
  assert.strictEqual(r.gained, L.BONUS_BIG);
});

t('쓰다듬기 보너스는 식물당 2시간까지, 물이 없으면 보너스 없음, 다 컸으면 없음', () => {
  const p = mk({ water: 100, growth: 0 });
  let total = 0;
  for (let i = 0; i < 1000; i++) total += L.stroke(p).gained;
  assert.strictEqual(total, L.BONUS_CAP);
  assert.strictEqual(p.bonus, L.BONUS_CAP);
  const dry = mk({ water: 0, growth: 100 });
  assert.strictEqual(L.stroke(dry).gained, 0);
  assert.strictEqual(dry.love, L.LOVE_PER_STROKE); // 애정은 올라감
  const done = mk({ growth: L.BLOOM_AT });
  assert.strictEqual(L.stroke(done).gained, 0);
});

t('쓰다듬기로 단계가 넘어가면 stage 변화가 보고된다', () => {
  const p = mk({ water: 100, growth: 0.5 * 3600 * 0 + 1800 - 10 }); // 새싹 직전
  const r = L.stroke(p);
  assert.strictEqual(r.prevStage, 0);
  assert.strictEqual(r.stage, 1);
});

t('꺼져 있던 시간 반영, 하루 상한, 시계가 거꾸로 가도 안전', () => {
  const now = Date.now();
  const s = L.defaultState(now, seeded(1));
  s.plant.water = 100;
  s.lastSeen = now - 2 * 3600 * 1000;
  const r = L.applyOffline(s, now);
  assert.ok(Math.abs(r.elapsed - 7200) < 1);
  assert.ok(Math.abs(s.plant.growth - 7200) < 1e-6);
  assert.strictEqual(L.stageOf(s.plant.growth), 2);
  s.lastSeen = now - 500 * 3600 * 1000;
  assert.strictEqual(L.applyOffline(s, now).elapsed, L.OFFLINE_CAP);
  s.lastSeen = now + 10000;
  assert.strictEqual(L.applyOffline(s, now).elapsed, 0);
});

t('자는 동안 다 자라면 도감에 등록', () => {
  const now = Date.now();
  const s = L.defaultState(now, seeded(2));
  s.plant.growth = L.BLOOM_AT - 1000;
  s.plant.water = 100;
  s.lastSeen = now - 3600 * 1000;
  const r = L.applyOffline(s, now);
  assert.ok(r.finishedNow && r.isNewSpecies);
  assert.strictEqual(Object.keys(s.collection).length, 1);
});

t('씨앗 확률 분포 (일반 60 / 희귀 30 / 레어 10)', () => {
  const rng = seeded(12345);
  const N = 40000, c = [0, 0, 0];
  for (let i = 0; i < N; i++) c[L.rollSpecies(rng).rarity]++;
  const pct = (i) => (c[i] / N) * 100;
  assert.ok(Math.abs(pct(0) - 60) < 2, 'common ' + pct(0));
  assert.ok(Math.abs(pct(1) - 30) < 2, 'uncommon ' + pct(1));
  assert.ok(Math.abs(pct(2) - 10) < 1.5, 'rare ' + pct(2));
  assert.ok(L.rollSpecies(() => 0) && L.rollSpecies(() => 0.999999999));
  // 모든 식물이 나올 수 있어야 함
  const seen = new Set();
  for (let i = 0; i < 4000; i++) seen.add(L.rollSpecies(rng).id);
  assert.strictEqual(seen.size, 10);
});

t('저장 데이터 복구: 망가진 값은 기본값으로', () => {
  const now = 1000000;
  assert.ok(L.normalizeState(null, now, seeded(3)).plant);
  assert.ok(L.normalizeState('abc', now, seeded(3)).plant);
  const s = L.normalizeState({
    plant: { speciesId: 'goldtomato', growth: 99999999, water: -5, love: 500, bonus: 'x' },
    collection: { goldtomato: { count: 2, skin: 'nope', first: 5 }, fake: { count: 9 } },
    settings: { scale: 7, skin: 'rose', mode: 'weird', talk: 'loud', x: 123, y: 'a' },
    lastSeen: 'x',
  }, now, seeded(3));
  assert.strictEqual(s.plant.speciesId, 'goldtomato');
  assert.strictEqual(s.plant.growth, L.BLOOM_AT);
  assert.strictEqual(s.plant.water, 0);
  assert.strictEqual(s.plant.love, L.LOVE_MAX);
  assert.strictEqual(s.plant.bonus, 0);
  assert.strictEqual(s.plant.soggy, false);
  assert.strictEqual(L.normalizeState({ plant: { speciesId: 'mint', soggy: true } }, now, seeded(3)).plant.soggy, true);
  assert.deepStrictEqual(Object.keys(s.collection), ['goldtomato']);
  assert.strictEqual(s.collection.goldtomato.skin, 'terra');
  assert.strictEqual(s.settings.scale, 3);
  assert.strictEqual(s.settings.skin, 'rose');
  assert.strictEqual(s.settings.mode, 'free');
  assert.strictEqual(s.settings.talk, 'sometimes');
  assert.strictEqual(L.normalizeState({ settings: { talk: 'quiet' } }, now, seeded(3)).settings.talk, 'quiet');
  assert.strictEqual(s.settings.x, 123);
  assert.strictEqual(s.settings.y, null);
  assert.strictEqual(s.lastSeen, now);
});

t('도감 등록: 처음 본 식물만 true, 화분 색 기록', () => {
  const s = L.defaultState(1, seeded(4));
  s.settings.skin = 'sky';
  assert.strictEqual(L.registerHarvest(s, 10), true);
  assert.strictEqual(L.registerHarvest(s, 20), false);
  const c = s.collection[s.plant.speciesId];
  assert.deepStrictEqual([c.count, c.skin, c.first], [2, 'sky', 10]);
});

t('모든 스프라이트 조합이 그려진다', () => {
  for (const id of S.SPECIES_IDS)
    for (let stage = 0; stage <= 4; stage++)
      for (const wilt of [false, true]) {
        const g = S.plantGrid(id, stage, wilt);
        assert.ok(g.d.flat().filter(Boolean).length > 8, `${id} stage${stage} wilt=${wilt}`);
        assert.ok(g.d[0].length === S.W && g.d.length === S.H);
      }
  for (const skin of S.SKIN_IDS)
    for (const face of ['idle', 'blink', 'happy', 'love', 'sad', 'sleep'])
      for (const look of [-1, 0, 1])
        for (const feet of [0, 1, 2]) assert.ok(S.potGrid(skin, face, look, feet).d.flat().filter(Boolean).length > 100);
  assert.ok(S.canGrid() && S.silhouetteGrid('cactus').d.flat().filter(Boolean).length > 50);
});

t('배속: rate 만큼만 성장이 빨라지고 물은 그대로 마른다', () => {
  const a = mk({ water: 100 }), b = mk({ water: 100 });
  L.advance(a, 600, 1); L.advance(b, 600, 3);
  assert.strictEqual(b.growth, a.growth * 3);
  assert.ok(Math.abs(a.water - b.water) < 1e-9);
  assert.strictEqual(L.timeLeft(mk({ growth: 0 }), 2).done, 4 * 3600);
  assert.strictEqual(L.timeLeft(mk({ growth: 0, soggy: true }), 2).done, 8 * 3600);
});

t('영양제: 1시간 1.5배, 2시간 쿨타임, 과습·완료 땐 못 먹음', () => {
  const s = L.defaultState(0, seeded(1)), T = 10_000_000;
  assert.strictEqual(L.nutriRate(s, T), 1);
  assert.strictEqual(L.useNutri(s, T).result, 'ok');
  assert.strictEqual(L.nutriRate(s, T + 1000), 1.5);
  assert.strictEqual(L.nutriRate(s, T + L.NUTRI_MS + 1), 1);
  const c = L.useNutri(s, T + L.NUTRI_MS);
  assert.strictEqual(c.result, 'cooldown');
  assert.strictEqual(c.waitMs, L.NUTRI_COOLDOWN_MS - L.NUTRI_MS);
  assert.strictEqual(L.useNutri(s, T + L.NUTRI_COOLDOWN_MS).result, 'ok');
  const g = L.defaultState(0, seeded(1)); g.plant.soggy = true;
  assert.strictEqual(L.useNutri(g, T).result, 'soggy');
  g.plant.soggy = false; g.plant.growth = L.BLOOM_AT;
  assert.strictEqual(L.useNutri(g, T).result, 'done');
});

t('놀아주기: 애정과 성장 보너스, 물이 없으면 성장 없음, 상한', () => {
  const p = mk({ water: 50, growth: 100 });
  const r = L.play(p);
  assert.strictEqual(p.love, L.PLAY_LOVE);
  assert.strictEqual(r.gained, L.PLAY_GROW);
  assert.strictEqual(p.growth, 100 + L.PLAY_GROW);
  const dry = mk({ water: 0, growth: 100 });
  assert.strictEqual(L.play(dry).gained, 0);
  const cap = mk({ water: 50, growth: 100, play: L.PLAY_CAP });
  assert.strictEqual(L.play(cap).gained, 0);
});

t('이름: 공백 정리, 8글자, 이상한 문자 제거', () => {
  assert.strictEqual(L.cleanName('  콩   이  '), '콩 이');
  assert.strictEqual(L.cleanName('아주아주아주긴이름입니다').length, L.NAME_MAX);
  assert.strictEqual(L.cleanName('<b>"x"&</b>'), 'bx/b');
  assert.strictEqual(L.cleanName(123), '');
});

t('아이템: 조건을 채우면 한 번만 얻고, 같은 칸은 하나만 착용', () => {
  const s = L.defaultState(0, seeded(1));
  assert.strictEqual(L.checkItems(s, 5).length, 0);
  s.stats.harvests = 1; s.stats.named = 1; s.stats.replants = 1;
  assert.deepStrictEqual(L.checkItems(s, 5).map((i) => i.id).sort(), ['glasses', 'ribbon', 'scarf']);
  assert.strictEqual(L.checkItems(s, 6).length, 0);
  s.collection = { tomato: { count: 1 }, basil: { count: 1 } };
  assert.deepStrictEqual(L.checkItems(s, 7).map((i) => i.id), ['flowerpin']);
  assert.ok(L.equipItem(s, 'ribbon'));
  assert.ok(L.equipItem(s, 'flowerpin'));
  assert.strictEqual(s.items.equipped.head, 'flowerpin');
  assert.ok(L.equipItem(s, 'flowerpin'));
  assert.strictEqual(s.items.equipped.head, null);
  assert.strictEqual(L.equipItem(s, 'crown'), false); // 아직 못 얻음
  assert.strictEqual(L.equipItem(s, 'nope'), false);
});

t('저장 데이터: 새 필드 복원, 이전 저장본도 열림', () => {
  const s = L.defaultState(0, seeded(2));
  s.items.owned.ribbon = 5; s.items.equipped.head = 'ribbon'; s.items.equipped.neck = 'crown'; // crown 은 head 칸이라 무시돼야 함
  s.settings.speed = 3; s.plant.name = '두부'; s.stats.plays = 7; s.nutri = { until: 9, readyAt: 99 };
  const r = L.normalizeState(JSON.parse(JSON.stringify(s)), 1, seeded(1));
  assert.strictEqual(r.items.equipped.head, 'ribbon');
  assert.strictEqual(r.items.equipped.neck, null);
  assert.deepStrictEqual([r.settings.speed, r.plant.name, r.stats.plays, r.nutri.readyAt], [3, '두부', 7, 99]);
  const old = L.normalizeState({ v: 2, plant: { speciesId: 'mint', growth: 10, water: 50 }, settings: { speed: 9 } }, 1, seeded(1));
  assert.strictEqual(old.settings.speed, 1);
  assert.deepStrictEqual(old.items.equipped, { head: null, face: null, neck: null });
  assert.strictEqual(old.plant.name, '');
});

t('수확하면 도감에 이름이 남고 수확 횟수가 늘어난다', () => {
  const s = L.defaultState(0, seeded(1));
  s.plant.speciesId = 'mint'; s.plant.name = '두부';
  assert.strictEqual(L.registerHarvest(s, 5), true);
  assert.strictEqual(s.collection.mint.name, '두부');
  assert.strictEqual(s.stats.harvests, 1);
});

t('아이템·장난감 스프라이트가 모두 그려진다', () => {
  for (const it of L.ITEMS) {
    const head = it.slot === 'head';
    assert.ok((head ? S.headGrid(it.id) : S.potGrid('terra', 'happy', 1, 0, it.id)).d.flat().filter(Boolean).length > 20, it.id);
  }
  for (const k of L.TOYS) for (const f of [0, 1]) assert.ok(S.toyGrid(k, f).d.flat().filter(Boolean).length > 20, k);
});

console.log(`\n${n}개 검사 통과`);
