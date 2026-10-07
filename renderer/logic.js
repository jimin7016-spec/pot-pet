/* 게임 규칙 (화면과 무관한 순수 로직) — 브라우저/Node 둘 다에서 동작
 * 여기 숫자만 바꿔도 게임 느낌이 바뀌어요. */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.PotLogic = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  /* ---------- 튜닝 값 ---------- */
  const HOUR = 3600;
  const STAGE_AT = [0, 0.5, 2, 4, 8].map((h) => h * HOUR); // 각 단계에 들어가는 '물 먹고 자란 시간'(초). 8시간 = 하루
  const STAGE_NAMES = ['씨앗', '새싹', '쑥쑥 자라는 중', '모양이 잡혔어요', '수확 완료'];
  const BLOOM_AT = STAGE_AT[4];
  const REVEAL_STAGE = 3; // 이 단계부터 무슨 식물인지 알려줘요
  const WATER_MAX = 100;
  const WATER_DRAIN = WATER_MAX / (3 * HOUR); // 물 한 번이면 3시간
  const WATER_PER_POUR = 100;
  const WATER_FULL_ABOVE = 90; // 이 이상일 때 물을 또 주면 과습(시들어요)
  const SOGGY_RATE = 0.5; // 과습인 동안 성장 속도. 다음 단계에 오르면 회복해요
  const THIRSTY_BELOW = 25;
  const OFFLINE_CAP = 24 * HOUR; // 꺼져 있던 시간은 최대 하루치만 반영
  const LOVE_MAX = 100;
  const LOVE_PER_STROKE = 5;
  const BONUS_SMALL = 80; // 쓰다듬을 때마다 성장하는 초
  const BONUS_BIG = 240; // 애정이 가득 찼을 때
  const BONUS_CAP = 2 * HOUR; // 식물 한 개가 쓰다듬기로 얻을 수 있는 최대 성장 시간
  const PLAY_LOVE = 4; // 장난감을 한 번 받아칠 때 애정
  const PLAY_GROW = 40; // 장난감을 한 번 받아칠 때 성장하는 초
  const PLAY_CAP = HOUR; // 식물 한 개가 놀이로 얻을 수 있는 최대 성장 시간
  const NUTRI_MS = HOUR * 1000; // 영양제 효과 시간
  const NUTRI_COOLDOWN_MS = 2 * HOUR * 1000; // 먹인 뒤 다음 영양제까지
  const NUTRI_RATE = 1.5; // 영양제 효과 동안 성장 속도
  const SPEEDS = [1, 2, 3]; // 성장 배속 (반차·외근 등 자리를 비우는 날용). 물 마르는 속도는 그대로
  const NAME_MAX = 8;
  const TOYS = ['ball', 'yarn', 'butterfly', 'bubble'];

  const RARITY = [
    { id: 'common', label: '일반', weight: 60, stars: 1 },
    { id: 'uncommon', label: '희귀', weight: 30, stars: 2 },
    { id: 'rare', label: '레어', weight: 10, stars: 3 },
  ];

  const SPECIES = [
    { id: 'tomato', name: '방울토마토', rarity: 0 },
    { id: 'basil', name: '바질', rarity: 0 },
    { id: 'cactus', name: '선인장', rarity: 0 },
    { id: 'sunflower', name: '해바라기', rarity: 0 },
    { id: 'lettuce', name: '상추', rarity: 0 },
    { id: 'mint', name: '민트', rarity: 0 },
    { id: 'strawberry', name: '딸기', rarity: 1 },
    { id: 'lavender', name: '라벤더', rarity: 1 },
    { id: 'purplebasil', name: '보라 바질', rarity: 2 },
    { id: 'goldtomato', name: '황금 방울토마토', rarity: 2 },
  ];
  const SKIN_IDS = ['terra', 'cream', 'mint', 'sky', 'rose', 'lilac'];
  const MODES = ['free', 'roam', 'follow', 'stay']; // 화면 전체 / 작업표시줄 위 / 커서 따라가기 / 가만히
  const SCALES = [2, 3, 4];
  const TALKS = ['often', 'sometimes', 'quiet']; // 혼잣말(응원) 빈도: 자주 / 가끔 / 조용히

  /* ---------- 유틸 ---------- */
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const num = (v, d) => (typeof v === 'number' && Number.isFinite(v) ? v : d);
  const speciesById = (id) => SPECIES.find((s) => s.id === id) || SPECIES[0];
  const rarityOf = (id) => RARITY[speciesById(id).rarity];

  /* ---------- 식물 ---------- */
  function rollSpecies(rng) {
    rng = rng || Math.random;
    let x = rng() * RARITY.reduce((a, r) => a + r.weight, 0);
    let tier = 0;
    for (let i = 0; i < RARITY.length; i++) {
      if (x < RARITY[i].weight) { tier = i; break; }
      x -= RARITY[i].weight;
    }
    const list = SPECIES.filter((s) => s.rarity === tier);
    return list[Math.min(list.length - 1, Math.floor(rng() * list.length))];
  }

  function newPlant(rng, now) {
    return { speciesId: rollSpecies(rng).id, growth: 0, water: 70, love: 0, bonus: 0, play: 0, name: '', soggy: false, plantedAt: now || Date.now() };
  }

  function stageOf(growth) {
    let s = 0;
    for (let i = 0; i < STAGE_AT.length; i++) if (growth >= STAGE_AT[i]) s = i;
    return s;
  }
  // 남은 시간(초): 물이 계속 있다는 가정. 과습이면 성장이 절반 속도라 두 배로 걸려요.
  function timeLeft(p, rate) {
    if (p.growth >= BLOOM_AT) return { done: 0, next: 0 };
    const k = (p.soggy ? 1 / SOGGY_RATE : 1) / (rate > 0 ? rate : 1), st = stageOf(p.growth);
    return { done: (BLOOM_AT - p.growth) * k, next: (STAGE_AT[st + 1] - p.growth) * k };
  }
  function fmtLeft(sec) {
    const m = Math.max(1, Math.ceil(sec / 60));
    const h = Math.floor(m / 60), mm = m % 60;
    return h ? (mm ? `${h}시간 ${mm}분` : `${h}시간`) : `${mm}분`;
  }
  const isDone = (p) => p.growth >= BLOOM_AT;
  const isDry = (p) => p.water <= 0 && !isDone(p);
  const isSoggy = (p) => !!p.soggy && !isDone(p);
const isWilted = (p) => (isDry(p) || isSoggy(p)) && stageOf(p.growth) >= 1; // 고개를 숙인 모습 (물 부족 또는 과습)
  const isThirsty = (p) => p.water < THIRSTY_BELOW && !isDone(p);
  const isRevealed = (p) => stageOf(p.growth) >= REVEAL_STAGE;

  const result = (p, prevStage) => {
    const stage = stageOf(p.growth);
    let recovered = false;
    if (p.soggy && stage > prevStage) { p.soggy = false; recovered = true; } // 과습은 다음 단계로 자라면 회복
    return { prevStage, stage, finishedNow: prevStage < 4 && isDone(p), recovered };
  };

  /** sec(가상 초)만큼 시간을 흘려보낸다. 물이 있는 동안만 자라고, 다 자라면 그 모습 그대로 멈춘다. */
  function advance(p, sec, rate) {
    const prev = stageOf(p.growth);
    if (!(sec > 0) || isDone(p)) return result(p, prev);
    const wet = Math.min(sec, p.water / WATER_DRAIN);
    p.growth = Math.min(BLOOM_AT, p.growth + wet * (rate > 0 ? rate : 1) * (p.soggy ? SOGGY_RATE : 1));
    p.water = Math.max(0, p.water - WATER_DRAIN * sec);
    return result(p, prev);
  }

  function waterPlant(p, force) {
    if (isDone(p)) return { result: 'done' };
    if (p.soggy && p.water >= THIRSTY_BELOW) return { result: 'soggy' }; // 과습: 물이 모자라지기 전엔 더 못 줘요
    if (p.water >= WATER_FULL_ABOVE && !force) return { result: 'full' };
    if (p.water >= WATER_FULL_ABOVE) { p.water = WATER_MAX; p.soggy = true; return { result: 'over' }; } // 물을 너무 많이 줌
    const wasDry = p.water <= 0;
    p.water = Math.min(WATER_MAX, p.water + WATER_PER_POUR);
    return { result: 'ok', revived: wasDry };
  }

  /** 쓰다듬기 한 번. 애정이 차고, 물이 있으면 성장이 조금 빨라진다(식물 한 개당 상한 있음). */
  function stroke(p) {
    const prev = stageOf(p.growth);
    if (isDone(p)) return Object.assign(result(p, prev), { gained: 0 });
    p.love = Math.min(LOVE_MAX, p.love + LOVE_PER_STROKE);
    let gained = 0;
    if (p.water > 0 && p.bonus < BONUS_CAP) {
      gained = Math.min(p.love >= LOVE_MAX ? BONUS_BIG : BONUS_SMALL, BONUS_CAP - p.bonus, BLOOM_AT - p.growth);
      p.bonus += gained;
      p.growth += gained;
    }
    return Object.assign(result(p, prev), { gained });
  }

  /** 장난감을 받아쳤을 때. 애정이 조금 오르고, 물이 있으면 성장도 조금(식물 한 개당 상한 있음). */
  function play(p) {
    const prev = stageOf(p.growth);
    if (isDone(p)) return Object.assign(result(p, prev), { gained: 0 });
    p.love = Math.min(LOVE_MAX, p.love + PLAY_LOVE);
    let gained = 0;
    if (p.water > 0 && (p.play || 0) < PLAY_CAP) {
      gained = Math.min(PLAY_GROW, PLAY_CAP - (p.play || 0), BLOOM_AT - p.growth);
      p.play = (p.play || 0) + gained;
      p.growth += gained;
    }
    return Object.assign(result(p, prev), { gained });
  }

  /* ---------- 영양제 ---------- */
  const nutriRate = (state, now) => (state.nutri && now < state.nutri.until ? NUTRI_RATE : 1);
  /** 영양제 먹이기. now = 실제 시각(ms). 과습이거나 쿨타임이면 못 먹여요. */
  function useNutri(state, now) {
    const p = state.plant;
    if (isDone(p)) return { result: 'done' };
    if (p.soggy) return { result: 'soggy' };
    if (state.nutri && now < state.nutri.readyAt) return { result: 'cooldown', waitMs: state.nutri.readyAt - now };
    state.nutri = { until: now + NUTRI_MS, readyAt: now + NUTRI_COOLDOWN_MS };
    return { result: 'ok' };
  }

  /* ---------- 이름 ---------- */
  function cleanName(raw) {
    if (typeof raw !== 'string') return '';
    return Array.from(raw.replace(/[\u0000-\u001f\u007f<>&"]/g, '').replace(/\s+/g, ' ').trim()).slice(0, NAME_MAX).join('').trim();
  }

  /* ---------- 꾸미기 아이템 (화분 색과 상관없이 모든 화분에 어울려요) ---------- */
  const SLOTS = ['head', 'face', 'neck'];
  const ITEMS = [
    { id: 'ribbon', slot: 'head', name: '빨간 리본', how: '첫 씨앗을 수확해요', test: (s) => s.stats.harvests >= 1 },
    { id: 'flowerpin', slot: 'head', name: '꽃핀', how: '도감 2종 채우기', test: (s) => dexCount(s) >= 2 },
    { id: 'crown', slot: 'head', name: '반짝 왕관', how: '도감 5종 채우기', test: (s) => dexCount(s) >= 5 },
    { id: 'glasses', slot: 'face', name: '동그란 안경', how: '처음으로 화분을 갈아엎어요', test: (s) => s.stats.replants >= 1 },
    { id: 'shades', slot: 'face', name: '선글라스', how: '과습에서 처음 회복해요', test: (s) => s.stats.recovers >= 1 },
    { id: 'scarf', slot: 'neck', name: '목도리', how: '식물에게 이름을 지어줘요', test: (s) => s.stats.named >= 1 },
    { id: 'bowtie', slot: 'neck', name: '나비넥타이', how: '장난감으로 10번 놀아줘요', test: (s) => s.stats.plays >= 10 },
    { id: 'bell', slot: 'neck', name: '방울 목걸이', how: '애정을 가득 채워요', test: (s) => s.stats.lovefull >= 1 },
  ];
  const itemById = (id) => ITEMS.find((i) => i.id === id);
  const dexCount = (s) => Object.keys(s.collection).length;
  /** 조건을 채운 아이템을 새로 얻는다. 새로 얻은 아이템 목록을 돌려줌 */
  function checkItems(state, now) {
    const got = [];
    for (const it of ITEMS) {
      if (!state.items.owned[it.id] && it.test(state)) { state.items.owned[it.id] = now || Date.now(); got.push(it); }
    }
    return got;
  }
  /** 같은 아이템을 다시 누르면 벗기, 아니면 그 칸에 착용 */
  function equipItem(state, id) {
    const it = itemById(id);
    if (!it || !state.items.owned[id]) return false;
    state.items.equipped[it.slot] = state.items.equipped[it.slot] === id ? null : id;
    return true;
  }
  const equippedKey = (state) => SLOTS.map((s) => state.items.equipped[s] || '').join(',');

  /* ---------- 저장 데이터 ---------- */
  function defaultState(now, rng) {
    return {
      v: 3,
      plant: newPlant(rng, now),
      collection: {},
      nutri: { until: 0, readyAt: 0 },
      stats: { harvests: 0, replants: 0, recovers: 0, named: 0, plays: 0, lovefull: 0 },
      items: { owned: {}, equipped: { head: null, face: null, neck: null } },
      settings: { scale: 3, skin: 'terra', mode: 'free', talk: 'sometimes', speed: 1, x: null, y: null },
      lastSeen: now,
    };
  }

  function normalizeState(raw, now, rng) {
    const s = defaultState(now, rng);
    if (!raw || typeof raw !== 'object') return s;
    const p = raw.plant;
    if (p && SPECIES.some((x) => x.id === p.speciesId)) {
      s.plant = {
        speciesId: p.speciesId,
        growth: clamp(num(p.growth, 0), 0, BLOOM_AT),
        water: clamp(num(p.water, 70), 0, WATER_MAX),
        love: clamp(num(p.love, 0), 0, LOVE_MAX),
        bonus: clamp(num(p.bonus, 0), 0, BONUS_CAP),
        play: clamp(num(p.play, 0), 0, PLAY_CAP),
        name: cleanName(p.name),
        soggy: p.soggy === true,
        plantedAt: num(p.plantedAt, now),
      };
    }
    if (raw.collection && typeof raw.collection === 'object') {
      for (const sp of SPECIES) {
        const c = raw.collection[sp.id];
        if (c && num(c.count, 0) > 0) {
          s.collection[sp.id] = { count: Math.floor(c.count), skin: SKIN_IDS.includes(c.skin) ? c.skin : 'terra', first: num(c.first, now), name: cleanName(c.name) };
        }
      }
    }
    if (raw.nutri && typeof raw.nutri === 'object') s.nutri = { until: num(raw.nutri.until, 0), readyAt: num(raw.nutri.readyAt, 0) };
    if (raw.stats && typeof raw.stats === 'object') for (const k of Object.keys(s.stats)) s.stats[k] = Math.max(0, Math.floor(num(raw.stats[k], 0)));
    if (raw.items && typeof raw.items === 'object') {
      const o = raw.items.owned || {}, e = raw.items.equipped || {};
      for (const it of ITEMS) if (typeof o[it.id] === 'number') s.items.owned[it.id] = o[it.id];
      for (const sl of SLOTS) { const it = itemById(e[sl]); if (it && it.slot === sl && s.items.owned[it.id]) s.items.equipped[sl] = it.id; }
    }
    const st = raw.settings || {};
    if (SPEEDS.includes(st.speed)) s.settings.speed = st.speed;
    if (SCALES.includes(st.scale)) s.settings.scale = st.scale;
    if (SKIN_IDS.includes(st.skin)) s.settings.skin = st.skin;
    if (MODES.includes(st.mode)) s.settings.mode = st.mode;
    if (TALKS.includes(st.talk)) s.settings.talk = st.talk;
    if (typeof st.x === 'number' && Number.isFinite(st.x)) s.settings.x = st.x;
    if (typeof st.y === 'number' && Number.isFinite(st.y)) s.settings.y = st.y;
    s.lastSeen = num(raw.lastSeen, now);
    return s;
  }

  /** 다 자라면 도감에 등록. 처음 보는 식물이면 true */
  function registerHarvest(state, now) {
    const id = state.plant.speciesId;
    const c = state.collection[id];
    state.collection[id] = { count: (c ? c.count : 0) + 1, skin: state.settings.skin, first: c ? c.first : now, name: cleanName(state.plant.name) || (c ? c.name : '') || '' };
    state.stats.harvests += 1;
    return !c;
  }

  /** 앱이 꺼져 있던 시간만큼 식물을 키운다 */
  function applyOffline(state, now) {
    const elapsed = clamp((now - state.lastSeen) / 1000, 0, OFFLINE_CAP);
    const r = advance(state.plant, elapsed, state.settings.speed);
    r.elapsed = elapsed;
    r.isNewSpecies = r.finishedNow ? registerHarvest(state, now) : false;
    state.lastSeen = now;
    return r;
  }

  return {
    STAGE_AT, STAGE_NAMES, BLOOM_AT, REVEAL_STAGE, WATER_MAX, WATER_DRAIN, WATER_PER_POUR, WATER_FULL_ABOVE, THIRSTY_BELOW,
    OFFLINE_CAP, SOGGY_RATE, LOVE_MAX, LOVE_PER_STROKE, BONUS_SMALL, BONUS_BIG, BONUS_CAP,
    RARITY, SPECIES, SKIN_IDS, MODES, SCALES, TALKS,
    clamp, speciesById, rarityOf, rollSpecies, newPlant, stageOf,
    isDone, isDry, isSoggy, isWilted, isThirsty, isRevealed,
    PLAY_LOVE, PLAY_GROW, PLAY_CAP, NUTRI_MS, NUTRI_COOLDOWN_MS, NUTRI_RATE, SPEEDS, NAME_MAX, TOYS, SLOTS, ITEMS,
    itemById, checkItems, equipItem, equippedKey, dexCount, cleanName, nutriRate, useNutri, play,
    timeLeft, fmtLeft, advance, waterPlant, stroke, defaultState, normalizeState, registerHarvest, applyOffline,
  };
});
