/* 화분 창의 메인 코드: 움직임, 물주기, 쓰다듬기, 말풍선, 우클릭 메뉴
 * 창(BrowserWindow) 자체를 움직여서 화분이 모니터 전체를 돌아다니는 것처럼 보이게 해요.
 * 상태/도감은 별도의 스티커 메모 창(note.html)에 정보를 보내서 보여줘요. */
(() => {
  'use strict';

  const L = window.PotLogic;
  const S = window.Sprites;
  const api = window.api;
  if (!api) {
    document.body.textContent = 'preload 를 불러오지 못했어요.';
    return;
  }

  /* ---------- 상수 ---------- */
  const CW = 40; // 캔버스(도트 단위) 가로
  const CH = 46; // 캔버스 세로 (스프라이트 40 + 위쪽 여유 6)
  const SX = 4; // 스프라이트가 그려지는 위치
  const SY = 6;
  const WIN_W = 220;
  const BUBBLE_H = 92; // 창 위쪽 말풍선 공간
  const SAVE_EVERY = 5000;
  const NAP_AFTER = 300; // 키보드/마우스 입력이 이만큼(초) 없으면 낮잠
  const STROKE_PX = 36; // 문지르는 거리(화면 px)마다 쓰다듬기 1회
  const RAINBOW = ['#ff6b6b', '#ffa94d', '#ffe066', '#69db7c', '#4dabf7', '#9775fa', '#f783ac'];
  const HEAD_Y = [19, 17, 11, 7, 6.5]; // 단계별 식물 머리 높이 (반짝임 위치용)
  const GREETINGS = [
    { id: 'morning', from: 8, to: 11, text: '좋은 아침이에요!\n오늘도 같이 키워요' },
    { id: 'lunch', from: 12, to: 14, text: '점심 먹을 시간이에요~' },
    { id: 'leave', from: 18, to: 20, text: '오늘도 수고했어요!' },
  ];

  /* 혼잣말: 귀여운 말 + 응원. 문구는 마음대로 고쳐도 돼요. */
  const TALK_CUTE = [
    '나 잘 크고 있죠?', '꼼지락꼼지락~', '햇빛 냄새가 나요', '으쌰으쌰!', '오늘도 같이 있어서 좋아요',
    '나 오늘 좀 귀엽지 않아요?', '뿌듯해요!', '흥얼흥얼~ 🎵', '뭐 하고 있어요? 구경해도 돼요?', '쪼르르르~',
    '여기 있으면 따뜻해요', '나 심심하지 않아요! 같이 있으니까요', '쑥쑥 크는 중이에요!',
  ];
  const TALK_CHEER = [
    '오늘도 홧팅이에요!', '잘하고 있어요!', '조금만 더 힘내요!', '당신 최고예요!', '천천히 해도 괜찮아요',
    '실수해도 괜찮아요. 다시 하면 되니까요', '오늘도 수고 많아요', '할 수 있어요! 홧팅!', '지금까지 충분히 잘했어요',
    '한 걸음씩, 한 걸음씩!', '제가 응원하고 있어요 📣', '어려운 일도 하나씩 하면 끝나요!',
  ];
  const TALK_CARE = [
    '물 한 잔 마셔요~', '잠깐 스트레칭 어때요?', '밥은 먹었어요?', '눈도 좀 쉬게 해 줘요', '어깨 한 번 돌려봐요~',
    '잠깐 창밖 한번 볼까요?', '허리 펴고! 쭈욱~',
  ];
  const TALK_TIME = [
    { from: 6, to: 11, lines: ['좋은 아침이에요! 오늘도 홧팅!', '아침부터 부지런하네요!'] },
    { from: 11, to: 14, lines: ['점심은 맛있는 거 먹어요~', '점심시간엔 쉬어가요!'] },
    { from: 14, to: 18, lines: ['졸리면 기지개 한번~', '오후도 거뜬히 해내요!'] },
    { from: 18, to: 22, lines: ['조금만 더 하면 퇴근이에요!', '오늘도 고생 많았어요!'] },
    { from: 22, to: 30, lines: ['늦게까지 고생이에요… 얼른 쉬어요', '오늘은 푹 자요. 내일 또 봐요!'] },
  ];
  const TALK_EVERY = { often: [50, 100], sometimes: [120, 240] }; // 초

  /* ---------- 요소 ---------- */
  const $ = (id) => document.getElementById(id);
  const canvas = $('pet');
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  canvas.width = CW;
  canvas.height = CH;
  ctx.imageSmoothingEnabled = false;
  const elSay = $('say');

  /* ---------- 상태 ---------- */
  let state = null;
  let scale = 3;
  let testSpeed = 1;
  let env = {
    cursor: { x: 0, y: 0 },
    displays: [{ bounds: { x: 0, y: 0, width: 1920, height: 1080 }, workArea: { x: 0, y: 0, width: 1920, height: 1040 } }],
    bounds: { x: 0, y: 0, width: WIN_W, height: 300 },
    idle: 0,
  };
  const pet = {
    x: 0, fy: 0, dir: 1, vy: 0, // x: 화분 가운데, fy: 발 바닥의 화면 y
    mode: 'idle', modeStart: 0, modeDur: 0, tx: 0, ty: 0,
    bob: 0, phase: 0, feet: 0, feetT: 0, lean: 0, moving: false,
    happyUntil: 0, pourUntil: 0, nextDrop: 0, blinkUntil: 0, nextBlink: 0,
    followUntil: 0, nextFollowAt: 0, nextThirstSay: 0, nextWiltTear: 0, nextZ: 0, nextTalk: 0,
    jumping: false, jumpT: 0, jumpDur: 0.5,
    toy: null, nextHit: 0, glowUntil: 0, nextGlow: 0,
  };
  const itemQueue = []; // 새로 얻은 아이템 알림 대기열
  let lastItemCheck = 0;
  const drag = { down: false, kind: null, active: false, sx: 0, sy: 0, ox: 0, oy: 0, lastSX: 0, acc: 0 };
  const hitMask = new Uint8Array(CW * CH);
  const particles = [];
  let ignoring = true;
  let sayShown = false;
  let sayUntil = 0;
  let petting = false;
  let napping = false;
  let lastSent = { x: NaN, y: NaN };
  let lastSave = 0;
  let lastEnvAt = 0;
  let envBusy = false;
  let lastSnap = '';
  let lastSnapAt = 0;
  let lastGreetCheck = 0;
  const greeted = new Set();

  /* ---------- 유틸 ---------- */
  const rand = (a, b) => a + Math.random() * (b - a);
  const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];
  const clamp = L.clamp;
  const winH = () => CH * scale + BUBBLE_H;
  const spriteH = () => CH * scale;
  const behavior = () => state.settings.mode;
  const stars = (id) => '★'.repeat(L.rarityOf(id).stars);

  /* ---------- 모니터 ---------- */
  function displayAt(x, y) {
    let best = env.displays[0], bestD = Infinity;
    for (const d of env.displays) {
      const b = d.bounds;
      const dx = Math.max(b.x - x, 0, x - (b.x + b.width));
      const dy = Math.max(b.y - y, 0, y - (b.y + b.height));
      const dist = dx * dx + dy * dy;
      if (dist < bestD) { bestD = dist; best = d; }
      if (dist === 0) break;
    }
    return best;
  }
  const groundOf = (d) => d.workArea.y + d.workArea.height; // 작업표시줄 윗면
  const topLimit = (d) => d.bounds.y + spriteH() + 8;
  function clampToDisplay() {
    const d = displayAt(pet.x, pet.fy), b = d.bounds;
    pet.x = clamp(pet.x, b.x + 18, b.x + b.width - 18);
    pet.fy = clamp(pet.fy, topLimit(d), b.y + b.height);
  }

  function setMode(m, now, dur) {
    pet.mode = m;
    pet.modeStart = now;
    pet.modeDur = dur || 0;
  }

  function applyScale() {
    canvas.style.width = CW * scale + 'px';
    canvas.style.height = CH * scale + 'px';
    canvas.style.left = Math.round((WIN_W - CW * scale) / 2) + 'px';
    document.documentElement.style.setProperty('--ch', CH * scale + 'px');
    api.setSize(WIN_W, winH());
    lastSent = { x: NaN, y: NaN };
  }

  /* ---------- 말풍선 ---------- */
  function say(text, ms) {
    elSay.textContent = text;
    sayUntil = performance.now() + (ms || 2500);
  }
  function updateBubble(now) {
    const show = !drag.active && now < sayUntil;
    if (show !== sayShown) {
      sayShown = show;
      elSay.style.display = show ? 'block' : 'none';
    }
  }

  /* ---------- 입자 (물방울, 반짝이, 하트, 색종이, zzz) ---------- */
  const HEART = ['.#.#.', '#####', '.###.', '..#..'];
  const ZZZ = ['###', '..#', '.#.', '#..', '###'];
  function addP(o) {
    if (particles.length > 220) return;
    const p = Object.assign({ vx: 0, vy: 0, g: 0, size: 1, color: '#fff', kind: 'px', life: 1 }, o);
    p.max = p.life;
    p.ph = Math.random() * 6;
    particles.push(p);
  }
  function burst(n, colors, cx, cy, spread) {
    for (let i = 0; i < n; i++) {
      addP({ kind: 'spark', x: cx + rand(-spread, spread), y: cy + rand(-spread, spread), vx: rand(-14, 14), vy: rand(-22, -4), life: rand(0.5, 1.0), color: pick(colors) });
    }
  }
  const headPos = (stage) => ({ x: SX + 15.5, y: SY + HEAD_Y[stage] });
  function confetti() {
    addP({ x: SX + 15.5 + rand(-7, 7), y: SY + rand(4, 9), vx: rand(-30, 30), vy: rand(-55, -20), g: 95, life: rand(0.8, 1.4), size: Math.random() < 0.4 ? 2 : 1, color: pick(RAINBOW) });
  }
  function updateParticles(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const p = particles[i];
      p.vy += p.g * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.life -= dt;
      if (p.kind === 'drop' && p.y >= SY + 22) {
        for (let k = 0; k < 3; k++) addP({ x: p.x, y: SY + 21, vx: rand(-14, 14), vy: rand(-22, -8), g: 80, life: 0.35, color: '#9bd6ff' });
        p.life = 0;
      }
      if (p.life <= 0) particles.splice(i, 1);
    }
  }
  function drawParticles() {
    for (const p of particles) {
      ctx.globalAlpha = Math.min(1, p.life / (p.max * 0.4));
      ctx.fillStyle = p.color;
      const x = Math.round(p.x), y = Math.round(p.y);
      if (p.kind === 'heart') HEART.forEach((row, ry) => [...row].forEach((ch, rx) => ch === '#' && ctx.fillRect(x + rx - 2, y + ry, 1, 1)));
      else if (p.kind === 'z') ZZZ.forEach((row, ry) => [...row].forEach((ch, rx) => ch === '#' && ctx.fillRect(x + rx, y + ry, 1, 1)));
      else if (p.kind === 'spark' && p.life > p.max * 0.35) {
        ctx.fillRect(x, y, 1, 1); ctx.fillRect(x - 1, y, 1, 1); ctx.fillRect(x + 1, y, 1, 1); ctx.fillRect(x, y - 1, 1, 1); ctx.fillRect(x, y + 1, 1, 1);
      } else if (p.kind === 'drop') ctx.fillRect(x, y, 1, 2);
      else ctx.fillRect(x, y, p.size, p.size);
    }
    ctx.globalAlpha = 1;
  }

  /** 상태에 따라 가끔 나오는 분위기 효과 */
  function ambient(dt, now) {
    const p = state.plant, stage = L.stageOf(p.growth), h = headPos(stage);
    if (L.rarityOf(p.speciesId).stars >= 3 && stage >= 3 && Math.random() < dt * (stage === 4 ? 2.4 : 1.0)) {
      burst(1, p.speciesId === 'purplebasil' ? ['#e6ccff', '#ffffff'] : ['#fff7b0', '#ffffff', '#ffe066'], h.x, h.y + 3, 8);
    }
    if (now < pet.pourUntil && now >= pet.nextDrop) {
      pet.nextDrop = now + 70;
      addP({ kind: 'drop', x: SX + 21 + rand(-0.3, 0.3), y: SY + 11, vy: 30, g: 70, life: 1.5, color: '#58b7ff' });
    }
    if (now < pet.glowUntil && !L.isDone(p) && now >= pet.nextGlow) {
      pet.nextGlow = now + 500;
      burst(1, ['#b9f6ca', '#fff3b0'], h.x + rand(-5, 5), h.y + rand(0, 8), 4);
    }
    if (L.isWilted(p) && !napping && now >= pet.nextWiltTear) {
      pet.nextWiltTear = now + 1600;
      addP({ kind: 'drop', x: SX + 12, y: SY + 34, vy: 10, g: 40, life: 0.8, color: '#6ec6ff' });
    }
    if (napping && now >= pet.nextZ) {
      pet.nextZ = now + 1400;
      addP({ kind: 'z', x: SX + 24, y: SY + 18, vx: 2, vy: -6, life: 2.2, color: '#3a2a33' });
    }
  }

  /* ---------- 식물 이벤트 ---------- */
  function onStage(stage, now, recovered) {
    if (recovered) state.stats.recovers += 1;
    const h = headPos(stage);
    burst(stage === 3 ? 18 : 8, stage === 3 ? ['#ffffff', '#fff3b0', '#ffc4d6', '#b9f6ca'] : ['#ffffff', '#b9f6ca'], h.x, h.y, 7);
    pet.happyUntil = Math.max(pet.happyUntil, now + 1500);
    if (recovered) { say('다시 쌩쌩해졌어요!\n' + ['쑥쑥 자라는 중~', '다 나았어요!'][stage % 2], 3400); burst(10, ['#b9f6ca', '#ffffff'], h.x, h.y + 6, 8); }
    else if (stage === 1) say('싹이 텄어요!', 3000);
    else if (stage === 2) say('잎이 늘었어요.\n뭘까요…?', 3200);
    else if (stage === 3) say(`앗, ${L.speciesById(state.plant.speciesId).name}이었어요!`, 4000);
    pushSnapshot(true);
  }

  function onFinished(now, isNew) {
    const sp = L.speciesById(state.plant.speciesId);
    say(`${state.plant.name ? state.plant.name + ' (' + sp.name + ')' : sp.name} 수확 완료! ${stars(sp.id)}${isNew ? '\n도감에 새로 등록했어요' : ''}`, 6500);
    setMode('celebrate', now, 3200);
    pet.happyUntil = now + 4000;
    saveNow();
    pushSnapshot(true);
  }

  function doWater() {
    const now = performance.now();
    const confirmed = now < (pet.confirmUntil || 0);
    const r = L.waterPlant(state.plant, confirmed);
    if (r.result !== "full") pet.confirmUntil = 0;
    if (r.result === 'done') { say('이미 다 컸어요!', 1800); pet.happyUntil = now + 900; return; }
    if (r.result === 'soggy') { say('아직 푹 젖어 있어요…\n다음 성장 때 괜찮아져요', 3200); return; }
    if (r.result === 'full') {
      pet.confirmUntil = now + 4000;
      say('배불러요! 물이 가득해요\n그래도 주려면 한 번 더 눌러요', 4000);
      return;
    }
    pet.pourUntil = now + 1500;
    pet.nextDrop = now;
    if (r.result === 'over') {
      // 과습: 물을 너무 많이 줘서 시들어요 (다음 성장 단계에 오르면 회복)
      pet.happyUntil = 0;
      say('으에… 물이 너무 많아요!\n다음 성장 때 괜찮아져요', 5000);
      burst(10, ['#9bd6ff', '#ffffff'], SX + 15.5, SY + 22, 8);
      pushSnapshot(true);
      return;
    }
    pet.happyUntil = now + 2600;
    say(r.revived ? '살았다…! 고마워요!' : pick(['고마워요!', '시원해요~', '꿀꺽꿀꺽!']), 2400);
    if (r.revived) burst(12, ['#9bd6ff', '#ffffff'], SX + 15.5, SY + 22, 7);
    pushSnapshot(true);
  }

  function doStroke() {
    const now = performance.now();
    const before = state.plant.love;
    const r = L.stroke(state.plant);
    if (before < L.LOVE_MAX && state.plant.love >= L.LOVE_MAX) state.stats.lovefull += 1;
    addP({ kind: 'heart', x: SX + 15.5 + rand(-10, 10), y: SY + rand(0, 8), vx: rand(-4, 4), vy: -16, life: 1.1, color: pick(['#ff5f86', '#ff8fab', '#ff3d6e']) });
    const love = state.plant.love;
    if (love === L.LOVE_PER_STROKE || love === 50 || love === L.LOVE_MAX) say(pick(['으헤헤', '기분 좋아요~', '더 해줘요!']), 1600);
    if (r.finishedNow) {
      onFinished(now, L.registerHarvest(state, Date.now()));
    } else if (r.stage !== r.prevStage) onStage(r.stage, now, r.recovered);
  }

  function newSeed() {
    if (!L.isDone(state.plant)) return;
    plantFresh();
  }

  // 초기화: 'plant' = 이 화분만 갈아엎고 새 씨앗 / 'all' = 도감까지 전부. 4초 안에 한 번 더 눌러야 해요.
  function resetGame(kind) {
    const now = performance.now();
    if (!(pet.resetKind === kind && now < (pet.resetUntil || 0))) {
      pet.resetKind = kind; pet.resetUntil = now + 4000;
      say(kind === 'all' ? '도감과 아이템까지 전부 지워져요!\n정말이면 한 번 더 눌러요' : '이 화분을 갈아엎고\n새 씨앗을 심어요. 한 번 더!', 4000);
      return;
    }
    pet.resetKind = null; pet.resetUntil = 0;
    if (kind === 'all') {
      const d = L.defaultState(Date.now());
      state.collection = {}; state.stats = d.stats; state.items = d.items; state.nutri = d.nutri;
      itemQueue.length = 0;
    } else state.stats.replants += 1;
    plantFresh();
  }

  function plantFresh() {
    state.plant = L.newPlant(Math.random, Date.now());
    const now = performance.now();
    setMode('hop', now, 480);
    pet.happyUntil = now + 1500;
    pet.nextThirstSay = now + 40000;
    if (L.rarityOf(state.plant.speciesId).stars >= 3) {
      say('씨앗이 반짝반짝\n빛나요…!', 4000);
      burst(10, ['#fff3b0', '#ffffff'], SX + 15.5, SY + 19, 6);
    } else {
      say(pick(['어떤 식물이 자랄까요?', '새 씨앗이다!', '두근두근…']), 3000);
    }
    saveNow();
    pushSnapshot(true);
  }

  /* ---------- 이름 / 영양제 / 배속 / 아이템 ---------- */
  const nm = () => state.plant.name;
  function setName(raw) {
    const n = L.cleanName(raw), now = performance.now();
    if (n === state.plant.name) return;
    state.plant.name = n;
    if (n) {
      state.stats.named += 1;
      say(pick([`${n}! 마음에 들어요`, `${n}… 좋은 이름이에요!`, `내 이름은 ${n}!`]), 3600);
      pet.happyUntil = now + 2400;
      burst(8, ['#ffc4d6', '#ffffff'], SX + 15.5, SY + 12, 7);
    } else say('이름을 지웠어요', 1800);
    saveNow();
    pushSnapshot(true);
  }

  function doNutri() {
    const now = performance.now(), r = L.useNutri(state, Date.now());
    if (r.result === 'done') { say('이미 다 컸어요!', 1800); return; }
    if (r.result === 'soggy') { say('푹 젖었을 땐\n영양제가 안 맞아요', 3000); return; }
    if (r.result === 'cooldown') { say(`영양제는 ${L.fmtLeft(r.waitMs / 1000)} 뒤에\n또 먹을 수 있어요`, 3200); return; }
    pet.glowUntil = now + L.NUTRI_MS;
    pet.happyUntil = now + 2600;
    pet.pourUntil = 0;
    say(pick(['영양제 꿀꺽!\n1시간 동안 쑥쑥 자라요', '으쌰! 힘이 나요!']), 3600);
    burst(14, ['#b9f6ca', '#fff3b0', '#ffffff'], SX + 15.5, SY + 12, 9);
    setMode('hop', now, 480);
    saveNow();
    pushSnapshot(true);
  }

  function setSpeed(n) {
    if (!L.SPEEDS.includes(n)) return;
    state.settings.speed = n;
    say(n === 1 ? '원래 속도로 자랄게요' : `${n}배속으로 쑥쑥!\n물은 그대로 마르니 챙겨줘요`, 3200);
    pet.happyUntil = performance.now() + 1200;
    saveNow();
    pushSnapshot(true);
  }

  function growRate() { return state.settings.speed * L.nutriRate(state, Date.now()); }

  function equip(id) {
    if (!L.equipItem(state, id)) return;
    const it = L.itemById(id), on = state.items.equipped[it.slot] === id;
    pet.happyUntil = performance.now() + 1400;
    say(on ? pick([`${it.name}!\n잘 어울려요?`, '와, 마음에 들어요!']) : '벗었어요', 2400);
    saveNow();
    pushSnapshot(true);
  }

  /** 조건을 채운 아이템이 있으면 새로 받아요 */
  function checkItems(now) {
    const got = L.checkItems(state, Date.now());
    if (!got.length) return;
    got.forEach((it) => itemQueue.push(`새 아이템!\n${it.name}을(를) 얻었어요`));
    pet.happyUntil = now + 2500;
    burst(12, ['#ffe066', '#ffffff', '#ffc4d6'], SX + 15.5, SY + 8, 9);
    saveNow();
    pushSnapshot(true);
  }

  /* ---------- 장난감 놀이 ---------- */
  const TOY_START = { ball: '공이다!', yarn: '털실이다!', butterfly: '나비다!', bubble: '비눗방울!' };
  function dropToy(kind) {
    if (napping || pet.toy) { if (pet.toy) say('아직 놀고 있어요!', 1600); return; }
    if (!kind || kind === 'random') kind = pick(L.TOYS);
    if (!L.TOYS.includes(kind)) return;
    pet.toy = { kind, x: pet.x, y: -200, vx: 0, vy: 0, hits: 0, gained: 0, started: false, startAt: performance.now() };
    api.dropToy({ kind, px: pet.x, py: pet.fy });
  }
  function onToyStart(o) {
    const now = performance.now();
    if (!pet.toy) pet.toy = { kind: o.kind, hits: 0, gained: 0 };
    Object.assign(pet.toy, { kind: o.kind, x: o.x, y: -200, started: true });
    setMode('play', now, 0);
    say(TOY_START[o.kind] || '와!', 1800);
  }
  function onToyEnd() {
    const t = pet.toy, now = performance.now();
    pet.toy = null;
    if (!t) return;
    if (pet.mode === 'play') setMode('idle', now, 1200);
    if (t.hits > 0) {
      state.stats.plays += 1;
      pet.happyUntil = now + 2400;
      const min = Math.round(t.gained / 60);
      say(`신나게 놀았어요!${min >= 1 ? `\n(성장 +${min}분)` : ''}`, 3400);
      saveNow();
      pushSnapshot(true);
    } else say('다음엔 꼭 잡을게요…', 2200);
  }
  function toyHit(t, now) {
    pet.nextHit = now + 450;
    t.hits += 1;
    pet.jumping = true; pet.jumpT = 0; pet.jumpDur = 0.45;
    pet.happyUntil = Math.max(pet.happyUntil, now + 900);
    const away = (t.x - pet.x) * 5 + rand(-140, 140);
    api.toyKick({ vx: clamp(away, -520, 520), vy: -rand(640, 860) });
    addP({ kind: 'heart', x: SX + 15.5 + rand(-8, 8), y: SY + rand(0, 6), vx: rand(-4, 4), vy: -16, life: 0.9, color: pick(['#ff5f86', '#ff8fab']) });
    const before = state.plant.love;
    const r = L.play(state.plant);
    t.gained += r.gained;
    if (before < L.LOVE_MAX && state.plant.love >= L.LOVE_MAX) state.stats.lovefull += 1;
    if (t.hits % 3 === 1) say(pick(['얍!', '잡았다!', '와~', '이리 와!', '헤헤']), 1100);
    if (r.finishedNow) onFinished(now, L.registerHarvest(state, Date.now()));
    else if (r.stage !== r.prevStage) onStage(r.stage, now, r.recovered);
    else pushSnapshot(false);
  }

  function setSkin(id) {
    if (!L.SKIN_IDS.includes(id)) return;
    state.settings.skin = id;
    pet.happyUntil = performance.now() + 900;
    saveNow();
    pushSnapshot(true);
  }

  function setTalk(t) {
    if (!L.TALKS.includes(t)) return;
    state.settings.talk = t;
    scheduleTalk(performance.now());
    say({ often: '자주 말 걸게요!', sometimes: '가끔 말 걸게요~', quiet: '조용히 있을게요…' }[t], 1800);
    saveNow();
  }

  function setBehavior(m) {
    if (!L.MODES.includes(m)) return;
    state.settings.mode = m;
    const now = performance.now();
    setMode('idle', now, 300);
    pet.nextFollowAt = now + (m === 'follow' ? 0 : rand(8000, 20000));
    say({ free: '구경하러 가요~', roam: '어슬렁어슬렁', follow: '같이 가요!', stay: '여기 있을게요' }[m], 1800);
    saveNow();
    pushSnapshot(true);
  }

  /* ---------- 저장 / 메모 창으로 상태 보내기 ---------- */
  function serialize() {
    state.lastSeen = Date.now();
    state.settings.scale = scale;
    state.settings.x = Math.round(pet.x);
    state.settings.y = Math.round(pet.fy);
    return state;
  }
  async function saveNow() {
    try { await api.saveState(serialize()); } catch (e) { /* 저장 실패는 조용히 넘어감 */ }
  }

  function snapshot() {
    const p = state.plant, st = L.stageOf(p.growth), sp = L.speciesById(p.speciesId), known = L.isRevealed(p);
    return {
      name: known ? sp.name : null,
      speciesId: known ? sp.id : null,
      rarity: known ? L.RARITY[sp.rarity].label : null,
      stars: known ? L.rarityOf(sp.id).stars : 0,
      stage: st,
      stageName: L.STAGE_NAMES[st],
      water: Math.round(p.water * 10) / 10,
      growth: Math.round((p.growth / L.BLOOM_AT) * 1000) / 10,
      left: L.timeLeft(p, growRate()),
      waterLeft: p.water / L.WATER_DRAIN,
      hours: Math.round((p.growth / 3600) * 10) / 10,
      love: p.love,
      done: L.isDone(p),
      thirsty: L.isThirsty(p),
      wilted: L.isWilted(p),
      soggy: L.isSoggy(p),
      full: p.water >= L.WATER_FULL_ABOVE && !L.isDone(p),
      napping,
      skin: state.settings.skin,
      mode: state.settings.mode,
      fast: testSpeed > 1,
      testSpeed,
      growSpeed: state.settings.speed,
      nick: p.name || '',
      nutri: { activeMs: Math.max(0, state.nutri.until - Date.now()), waitMs: Math.max(0, state.nutri.readyAt - Date.now()) },
      items: { owned: Object.keys(state.items.owned), equipped: state.items.equipped },
      stats: state.stats,
      toy: !!pet.toy,
      collection: state.collection,
    };
  }
  function pushSnapshot(force) {
    const now = performance.now();
    if (!force && now - lastSnapAt < 400) return;
    lastSnapAt = now;
    const s = JSON.stringify(snapshot());
    if (s !== lastSnap) {
      lastSnap = s;
      api.sendNoteState(JSON.parse(s));
    }
  }

  /* ---------- 움직임 ---------- */
  function wantFollow(now) {
    const b = behavior();
    if (b === 'follow') return true;
    if (b === 'stay') return false;
    return now >= pet.nextFollowAt;
  }
  function enterFollow(now) {
    setMode('follow', now, 0);
    pet.followUntil = now + rand(9000, 16000);
    if (behavior() !== 'follow' && Math.random() < 0.6) say('같이 가요!', 1800);
  }

  /** 목표를 향해 한 걸음. 도착했으면 false */
  function stepToward(tx, ty, step, stop) {
    const dx = tx - pet.x, dy = ty - pet.fy, d = Math.hypot(dx, dy);
    if (d <= stop + 0.5) return false;
    const m = Math.min(step, d - stop);
    pet.x += (dx / d) * m;
    pet.fy += (dy / d) * m;
    if (Math.abs(dx) > 1) pet.dir = Math.sign(dx);
    return true;
  }

  function pickWalkTarget() {
    const cur = displayAt(pet.x, pet.fy);
    const d = env.displays.length > 1 && Math.random() < 0.3 ? pick(env.displays) : cur;
    const b = d.bounds;
    pet.tx = rand(b.x + 40, b.x + b.width - 40);
    pet.ty = behavior() === 'roam' ? groundOf(d) : rand(topLimit(d) + 30, b.y + b.height - 2);
  }

  /** 걷다가 가끔 폴짝 (초당 확률 rate) */
  function maybeJump(dt, now, rate) {
    if (pet.jumping || Math.random() >= dt * rate) return;
    pet.jumping = true;
    pet.jumpT = 0;
    pet.jumpDur = rand(0.42, 0.55);
    pet.happyUntil = Math.max(pet.happyUntil, now + 700);
    if (Math.random() < 0.18) say(pick(['폴짝!', '얍!', '신난다~', '휘리릭~']), 1300);
  }

  function updateBehavior(dt, now) {
    pet.moving = false;
    let bob = 0;

    if (pet.mode === 'drag') { pet.bob = 0; pet.feet = 0; pet.jumping = false; return; }
    if (pet.mode === 'fall') {
      pet.vy += 2400 * dt;
      pet.fy += pet.vy * dt;
      const g = groundOf(displayAt(pet.x, pet.fy));
      if (pet.fy >= g) { pet.fy = g; pet.vy = 0; setMode('hop', now, 450); pet.happyUntil = now + 900; }
      clampToDisplay();
      return;
    }
    if (napping) { pet.bob = 0; pet.feet = 0; return; }

    if (pet.toy && pet.toy.started && (pet.mode === 'idle' || pet.mode === 'walk' || pet.mode === 'follow')) setMode('play', now, 0);

    switch (pet.mode) {
      case 'idle': {
        if (behavior() === 'roam') {
          const g = groundOf(displayAt(pet.x, pet.fy));
          if (Math.abs(pet.fy - g) > 2) { pet.vy = 0; setMode('fall', now, 0); break; }
        }
        if (wantFollow(now)) { enterFollow(now); break; }
        if (now >= pet.modeStart + pet.modeDur) {
          const r = Math.random();
          if (behavior() === 'stay') setMode(r < 0.25 ? 'hop' : 'idle', now, r < 0.25 ? 480 : rand(3000, 7000));
          else if (r < 0.6) { pickWalkTarget(); setMode('walk', now, 0); }
          else if (r < 0.75) setMode('hop', now, 480);
          else setMode('idle', now, rand(2000, 5000));
        }
        break;
      }
      case 'walk': {
        if (wantFollow(now)) { enterFollow(now); break; }
        if (!stepToward(pet.tx, pet.ty, 16 * dt, 0)) { setMode('idle', now, rand(2000, 5000)); break; }
        pet.moving = true;
        pet.phase += dt * 8;
        bob = Math.abs(Math.sin(pet.phase)) * 1.2;
        maybeJump(dt, now, 0.18);
        break;
      }
      case 'follow': {
        const b = behavior();
        if (b === 'stay' || (b !== 'follow' && now > pet.followUntil)) {
          pet.nextFollowAt = now + rand(40000, 110000);
          setMode('idle', now, rand(800, 1800));
          break;
        }
        const c = env.cursor, d = displayAt(c.x, c.y);
        const ty = b === 'roam' ? groundOf(d) : clamp(c.y + 30, topLimit(d), d.bounds.y + d.bounds.height);
        const dist = Math.hypot(c.x - pet.x, ty - pet.fy);
        if (dist > 56 && stepToward(c.x, ty, (dist > 240 ? 170 : 105) * dt, 56)) {
          pet.moving = true;
          pet.phase += dt * 10;
          bob = Math.abs(Math.sin(pet.phase)) * 4;
          maybeJump(dt, now, 0.1);
        } else if (Math.random() < dt * 0.5) pet.happyUntil = now + 900;
        break;
      }
      case 'hop': {
        const t = clamp((now - pet.modeStart) / Math.max(1, pet.modeDur), 0, 1);
        bob = Math.sin(Math.PI * t) * 5;
        if (t >= 1) setMode('idle', now, rand(1200, 3000));
        break;
      }
      case 'play': {
        const t = pet.toy;
        if (!t || !t.started) { if (!t || now > t.startAt + 4000) { pet.toy = null; setMode('idle', now, 800); } break; }
        const dx = t.x - pet.x;
        if (Math.abs(dx) > 6) {
          pet.x += Math.sign(dx) * Math.min(Math.abs(dx), (Math.abs(dx) > 120 ? 260 : 150) * dt);
          pet.dir = Math.sign(dx);
          pet.moving = true;
          pet.phase += dt * 11;
          bob = Math.abs(Math.sin(pet.phase)) * 3;
        }
        const top = pet.fy - spriteH() - 24, bot = pet.fy - 3 * scale;
        if (now >= pet.nextHit && Math.abs(dx) < 14 * scale + 14 && t.y > top && t.y < bot) toyHit(t, now);
        break;
      }
      case 'celebrate': {
        bob = Math.abs(Math.sin((now - pet.modeStart) / 160)) * 5;
        if (Math.random() < dt * 14) confetti();
        if (now >= pet.modeStart + pet.modeDur) setMode('idle', now, 1500);
        break;
      }
      default:
        setMode('idle', now, 1000);
    }

    if (pet.jumping) {
      pet.jumpT += dt;
      const k = pet.jumpT / pet.jumpDur;
      if (k >= 1) pet.jumping = false;
      else bob = Math.sin(Math.PI * k) * 6; // 폴짝!
    }
    pet.bob = bob;
    clampToDisplay();
    if (pet.moving) {
      pet.feetT += dt;
      if (pet.feetT > 0.16) { pet.feetT = 0; pet.feet = pet.feet === 1 ? 2 : 1; }
      if (pet.feet === 0) pet.feet = 1;
    } else { pet.feet = 0; pet.feetT = 0; }
  }

  /* ---------- 그리기 ---------- */
  function currentFace(now) {
    const p = state.plant;
    if (petting) return 'love';
    if (napping) return 'sleep';
    if (L.isWilted(p)) return 'sad';
    if (pet.mode === 'drag' || pet.mode === 'celebrate' || now < pet.happyUntil || L.isDone(p)) return 'happy';
    if (now < pet.blinkUntil) return 'blink';
    if (now >= pet.nextBlink) { pet.blinkUntil = now + 130; pet.nextBlink = now + rand(2400, 5600); return 'blink'; }
    return 'idle';
  }
  function currentLook() {
    if (pet.moving) return pet.dir;
    const dx = env.cursor.x - pet.x;
    return Math.abs(dx) > 80 && Math.abs(env.cursor.y - pet.fy) < 500 ? Math.sign(dx) : 0;
  }

  function render(now, dt) {
    const p = state.plant, stage = L.stageOf(p.growth), wilt = L.isWilted(p);
    const bobY = -Math.round(pet.bob);

    ctx.clearRect(0, 0, CW, CH);
    const eq = state.items.equipped, wear = [eq.face, eq.neck].filter(Boolean).join(',');
    ctx.drawImage(S.potCanvas(state.settings.skin, currentFace(now), currentLook(), pet.feet, wear), SX, SY + bobY);

    // 식물: 줄 단위로 옆으로 밀어서 도트 그대로 살랑살랑 흔들리게 그림
    const lean = pet.moving ? -pet.dir * (pet.mode === 'follow' ? 2.2 : 1.2) : 0;
    pet.lean += (lean - pet.lean) * Math.min(1, dt * 8);
    const sway = wilt || stage === 0 || napping ? 0 : Math.sin(now / 650) * (0.35 + stage * 0.22) + pet.lean + (petting ? Math.sin(now / 45) * 2 : 0);
    const img = S.plantCanvas(p.speciesId, stage, wilt);
    for (let y = 0; y < S.H; y++) {
      const dx = Math.round((sway * Math.max(0, 22 - y)) / 14);
      ctx.drawImage(img, 0, y, S.W, 1, SX + dx, SY + y + bobY, S.W, 1);
    }
    if (eq.head) ctx.drawImage(S.headCanvas(eq.head), SX, SY + bobY);

    // 클릭 판정용: 화분+식물이 그려진 픽셀만 "내 몸"으로 취급
    const d = ctx.getImageData(0, 0, CW, CH).data;
    for (let i = 0; i < hitMask.length; i++) hitMask[i] = d[i * 4 + 3] > 40 ? 1 : 0;

    if (L.isThirsty(p) && !drag.active && !napping) {
      ctx.fillStyle = '#58b7ff';
      const dy = Math.floor(now / 300) % 2;
      ctx.fillRect(CW - 9, 2 + dy, 3, 4); ctx.fillRect(CW - 10, 5 + dy, 5, 2);
    }
    if (now < pet.pourUntil) ctx.drawImage(S.canCanvas(), SX + 19, SY + 1 + Math.round(Math.sin(now / 80) * 0.6));
    drawParticles();
  }

  /* ---------- 마우스 ---------- */
  function setIgnore(v) {
    if (v !== ignoring) { ignoring = v; api.setIgnore(v); }
  }
  function petHit(cx, cy) {
    const r = canvas.getBoundingClientRect();
    const lx = Math.floor((cx - r.left) / scale), ly = Math.floor((cy - r.top) / scale);
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      const x = lx + dx, y = ly + dy;
      if (x >= 0 && y >= 0 && x < CW && y < CH && hitMask[y * CW + x]) return true;
    }
    return false;
  }
  /** 마우스가 화분 위에 있을 때만 창이 클릭을 받고, 나머지는 뒤쪽 창으로 통과 */
  function updateHover(cx, cy) {
    if (!drag.down) setIgnore(!petHit(cx, cy));
  }

  canvas.addEventListener('mousedown', (e) => {
    if (!petHit(e.clientX, e.clientY)) return;
    if (e.button !== 0 && e.button !== 1) return;
    drag.down = true;
    drag.active = false;
    drag.kind = e.button === 1 || e.shiftKey ? 'carry' : 'pet'; // 그냥 문지르면 쓰다듬기, Shift(또는 휠 클릭)+드래그면 들어서 옮기기
    drag.sx = drag.lastSX = e.screenX;
    drag.sy = e.screenY;
    drag.acc = 0;
    drag.ox = pet.x - e.screenX;
    drag.oy = pet.fy - e.screenY;
    e.preventDefault();
  });

  window.addEventListener('mousemove', (e) => {
    if (drag.down) {
      if (!drag.active && Math.hypot(e.screenX - drag.sx, e.screenY - drag.sy) > 5) {
        drag.active = true;
        if (drag.kind === 'carry') { setMode('drag', performance.now(), 0); pet.vy = 0; }
        else petting = true;
      }
      if (drag.active && drag.kind === 'carry') {
        pet.x = e.screenX + drag.ox;
        pet.fy = e.screenY + drag.oy;
      } else if (drag.active) {
        drag.acc += Math.abs(e.screenX - drag.lastSX);
        drag.lastSX = e.screenX;
        while (drag.acc >= STROKE_PX) { drag.acc -= STROKE_PX; doStroke(); }
      }
      return;
    }
    updateHover(e.clientX, e.clientY);
  });

  function endDrag(e) {
    if (!drag.down) return;
    const was = drag.active, kind = drag.kind;
    drag.down = false;
    drag.active = false;
    petting = false;
    if (e && e.button !== 0 && e.button !== 1) return;
    const now = performance.now();
    if (was && kind === 'carry') {
      clampToDisplay();
      if (behavior() === 'roam') { setMode('fall', now, 0); pet.vy = 0; }
      else { setMode('hop', now, 450); pet.happyUntil = now + 900; }
    } else if (!was && e) {
      pet.happyUntil = now + 900; // 톡 건드림
      say(pick(['응?', '왜요~', '쓰다듬어 주세요! (문지르기)']), 1600);
    }
  }
  window.addEventListener('mouseup', endDrag);
  window.addEventListener('blur', () => endDrag(null));
  document.addEventListener('mouseleave', () => { if (!drag.down) setIgnore(true); });

  window.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    if (petHit(e.clientX, e.clientY)) api.showMenu(buildMenu());
  });

  /* ---------- 우클릭 메뉴 ---------- */
  function nutriLabel() {
    const w = state.nutri.readyAt - Date.now();
    if (state.nutri.until > Date.now()) return `영양제 효과 중 (${L.fmtLeft((state.nutri.until - Date.now()) / 1000)} 남음)`;
    return w > 0 ? `영양제 주기 (${L.fmtLeft(w / 1000)} 뒤에 가능)` : '영양제 주기';
  }
  function buildMenu() {
    const done = L.isDone(state.plant), cur = state.settings;
    const skinItems = L.SKIN_IDS.map((id) => ({ label: S.SKINS[id].name, type: 'radio', checked: cur.skin === id, id: 'skin:' + id }));
    const modeItems = [['free', '화면 전체 돌아다니기'], ['roam', '작업표시줄 위 걷기'], ['follow', '커서 따라가기'], ['stay', '가만히 있기']]
      .map(([id, label]) => ({ label, type: 'radio', checked: cur.mode === id, id: 'mode:' + id }));
    return [
      { label: '물 주기', id: 'water', enabled: !done },
      { label: done ? '새 씨앗 심기' : '새 씨앗 심기 (다 자라면 가능해요)', id: 'newseed', enabled: done },
      { label: '화분 갈아엎고 새로 심기', id: 'replant' },
      { label: '전부 초기화 (도감 포함)', id: 'resetall' },
      { label: '장난감 떨어뜨리기 (Ctrl+Alt+T)', submenu: [['random', '아무거나'], ['ball', '공'], ['yarn', '털실'], ['butterfly', '나비'], ['bubble', '비눗방울']].map(([id, label]) => ({ label, id: 'toy:' + id, enabled: !napping && !pet.toy })) },
      { label: nutriLabel(), id: 'nutri', enabled: !done },
      { label: '성장 속도', submenu: L.SPEEDS.map((n) => ({ label: n === 1 ? '1배 (기본)' : `${n}배`, type: 'radio', checked: cur.speed === n, id: 'speed:' + n })) },
      { type: 'separator' },
      { label: '상태 메모', type: 'checkbox', id: 'ui:status' },
      { label: '도감 메모', type: 'checkbox', id: 'ui:dex' },
      { label: '옷장 메모', type: 'checkbox', id: 'ui:closet' },
      { type: 'separator' },
      { label: '화분 색', submenu: skinItems },
      { label: '지내는 방식', submenu: modeItems },
      {
        label: '크기',
        submenu: [
          { label: '작게', type: 'radio', checked: scale === 2, id: 'scale:2' },
          { label: '보통', type: 'radio', checked: scale === 3, id: 'scale:3' },
          { label: '크게', type: 'radio', checked: scale === 4, id: 'scale:4' },
        ],
      },
      {
        label: '말 걸기 (혼잣말·응원)',
        submenu: [['often', '자주'], ['sometimes', '가끔'], ['quiet', '조용히']]
          .map(([id, label]) => ({ label, type: 'radio', checked: cur.talk === id, id: 'talk:' + id })),
      },
      { label: '테스트 모드 (30배속)', type: 'checkbox', checked: testSpeed > 1, id: 'fast' },
      { type: 'separator' },
      { label: '화분 숨기기 (Ctrl+Alt+P)', id: 'ui:hide' },
      { label: '종료', id: 'quit' },
    ];
  }

  function onMenu(id) {
    if (id === 'water') doWater();
    else if (id === 'newseed') newSeed();
    else if (id === 'replant') resetGame('plant');
    else if (id === 'resetall') resetGame('all');
    else if (id === 'nutri') doNutri();
    else if (id.startsWith('toy:')) dropToy(id.slice(4));
    else if (id.startsWith('speed:')) setSpeed(parseInt(id.slice(6), 10));
    else if (id.startsWith('skin:')) setSkin(id.slice(5));
    else if (id.startsWith('mode:')) setBehavior(id.slice(5));
    else if (id.startsWith('talk:')) setTalk(id.slice(5));
    else if (id.startsWith('scale:')) {
      const s = parseInt(id.slice(6), 10);
      if (L.SCALES.includes(s)) { scale = s; applyScale(); saveNow(); }
    } else if (id === 'fast') {
      testSpeed = testSpeed > 1 ? 1 : 30;
      say(testSpeed > 1 ? '⚡ 30배속!' : '원래 속도로', 1800);
      pushSnapshot(true);
    } else if (id === 'quit') saveNow().then(() => api.quit());
  }
  api.onMenuAction(onMenu);
  api.onNoteAction((a) => {
    if (!a || typeof a.type !== 'string') return;
    if (a.type === 'water') doWater();
    else if (a.type === 'newseed') newSeed();
    else if (a.type === 'replant') resetGame('plant');
    else if (a.type === 'resetall') resetGame('all');
    else if (a.type === 'skin') setSkin(a.value);
    else if (a.type === 'name') setName(a.value);
    else if (a.type === 'nutri') doNutri();
    else if (a.type === 'speed') setSpeed(parseInt(a.value, 10));
    else if (a.type === 'equip') equip(a.value);
    else if (a.type === 'toy') dropToy(a.value);
  });
  api.onResetPos(() => {
    const d = env.displays[0], w = d.workArea;
    pet.x = w.x + w.width / 2;
    pet.fy = groundOf(d);
    setMode('hop', performance.now(), 480);
    say('여기 있어요!', 2000);
  });
  api.onToyStart(onToyStart);
  api.onToyPos((o) => { if (pet.toy) Object.assign(pet.toy, { x: o.x, y: o.y, vx: o.vx, vy: o.vy }); });
  api.onToyEnd(onToyEnd);
  api.onToyHotkey(() => dropToy('random'));
  api.onSaveAndQuit(() => { saveNow().then(() => api.quit()); });

  /* ---------- 메인 루프 ---------- */
  function pollEnv(now) {
    if (envBusy || now - lastEnvAt < 100) return;
    envBusy = true;
    lastEnvAt = now;
    api.getEnv()
      .then((e) => {
        env = e;
        const cx = e.cursor.x - e.bounds.x, cy = e.cursor.y - e.bounds.y;
        if (cx >= 0 && cy >= 0 && cx <= e.bounds.width && cy <= e.bounds.height) updateHover(cx, cy);
        else if (!drag.down) setIgnore(true);
      })
      .catch(() => {})
      .finally(() => { envBusy = false; });
  }

  function checkNap(now) {
    const want = env.idle >= NAP_AFTER && !drag.down;
    if (want && !napping) {
      napping = true;
      pet.nextZ = now;
      say('zzz…', 2500);
      pushSnapshot(true);
    } else if (!want && napping) {
      napping = false;
      pet.happyUntil = now + 1800;
      say('어서 와요!', 2600);
      pushSnapshot(true);
    }
  }

  function scheduleTalk(now) {
    const t = TALK_EVERY[state.settings.talk];
    pet.nextTalk = t ? now + rand(t[0], t[1]) * 1000 : Infinity;
  }
  function pickTalk() {
    const h = new Date().getHours(), hh = h < 6 ? h + 24 : h;
    const tm = TALK_TIME.find((x) => hh >= x.from && hh < x.to);
    const r = Math.random();
    const n = nm();
    if (n && r < 0.15) return pick([`내 이름은 ${n}!`, `${n}~ ${n}~ 불러봐요!`, `${n} 오늘도 쑥쑥 크는 중!`]);
    if (tm && r < 0.25) return pick(tm.lines);
    if (r < 0.6) return pick(TALK_CHEER);
    if (r < 0.85) return pick(TALK_CUTE);
    return pick(TALK_CARE);
  }
  function checkTalk(now) {
    if (now < pet.nextTalk) return;
    // 말하기 곤란한 때(낮잠, 드래그, 쓰다듬는 중, 다른 말풍선이 떠 있음)는 조금 뒤로 미룸
    if (napping || drag.active || petting || sayShown || pet.mode === 'celebrate') { pet.nextTalk = now + 8000; return; }
    say(pickTalk(), 3800);
    if (Math.random() < 0.5) pet.happyUntil = now + 1500;
    scheduleTalk(now);
  }

  function checkGreeting(now) {
    if (now - lastGreetCheck < 30000) return;
    lastGreetCheck = now;
    const d = new Date(), h = d.getHours();
    const g = GREETINGS.find((x) => h >= x.from && h < x.to);
    if (!g) return;
    const key = d.toDateString() + g.id;
    if (greeted.has(key)) return;
    greeted.add(key);
    if (!napping && !L.isThirsty(state.plant)) say(g.text, 4000);
  }

  function update(dt, now) {
    const p = state.plant;
    if (!napping) {
      const r = L.advance(p, dt * testSpeed, growRate());
      if (r.finishedNow) onFinished(now, L.registerHarvest(state, Date.now()));
      else if (r.stage !== r.prevStage) onStage(r.stage, now, r.recovered);
    }

    pollEnv(now);
    checkNap(now);
    updateBehavior(dt, now);
    ambient(dt, now);
    updateParticles(dt);
    checkGreeting(now);
    checkTalk(now);
    if (now - lastItemCheck > 1000) { lastItemCheck = now; checkItems(now); }
    if (itemQueue.length && !sayShown && now >= sayUntil && !napping) say(itemQueue.shift(), 4200);

    if (L.isThirsty(p) && !napping && now >= pet.nextThirstSay && !sayShown) {
      say(pick(L.isDry(p) ? ['바싹 말랐어요…', '물… 물 좀…'] : ['목말라요…', '물 좀 주세요~']), 3000);
      pet.nextThirstSay = now + 90000;
    }

    const bx = Math.round(pet.x - WIN_W / 2), by = Math.round(pet.fy - winH());
    if (bx !== lastSent.x || by !== lastSent.y) {
      lastSent = { x: bx, y: by };
      api.setBounds(bx, by);
    }
    if (now - lastSave > SAVE_EVERY) { lastSave = now; saveNow(); }
    pushSnapshot(false);
    updateBubble(now);
  }

  let last = performance.now();
  function loop(ts) {
    const dt = Math.min(0.1, Math.max(0.001, (ts - last) / 1000));
    last = ts;
    update(dt, ts);
    render(ts, dt);
    requestAnimationFrame(loop);
  }

  /* ---------- 시작 ---------- */
  async function init() {
    const wallNow = Date.now();
    const saved = await api.loadState();
    state = L.normalizeState(saved, wallNow);
    scale = state.settings.scale;
    try { env = await api.getEnv(); } catch (e) { /* 기본값 사용 */ }
    applyScale();

    const prim = env.displays[0];
    const sx = state.settings.x, sy = state.settings.y;
    const inside = typeof sx === 'number' && typeof sy === 'number' && env.displays.some((d) => sx >= d.bounds.x + 18 && sx <= d.bounds.x + d.bounds.width - 18 && sy > d.bounds.y + 60 && sy <= d.bounds.y + d.bounds.height);
    pet.x = inside ? sx : prim.workArea.x + prim.workArea.width / 2;
    pet.fy = inside ? sy : groundOf(prim);

    const off = L.applyOffline(state, wallNow); // 꺼져 있던 동안의 성장
    const t = performance.now();
    last = t; lastSave = t;
    setMode('idle', t, 1500);
    pet.nextFollowAt = t + rand(20000, 50000);
    pet.nextThirstSay = t + 30000;
    scheduleTalk(t);
    pet.nextBlink = t + 2000;

    const hr = new Date().getHours(), cg = GREETINGS.find((x) => hr >= x.from && hr < x.to);
    if (cg) greeted.add(new Date().toDateString() + cg.id); // 시작하자마자 시간대 인사는 하지 않음
    const sp = L.speciesById(state.plant.speciesId);
    if (off.finishedNow) {
      setMode('celebrate', t, 2500);
      say(`자는 동안 ${sp.name}이(가) 다 자랐어요! ${stars(sp.id)}${off.isNewSpecies ? '\n도감에 새로 등록했어요' : ''}`, 7000);
    } else if (!saved) {
      say('안녕하세요!\n저를 잘 키워주세요 🌱', 5000);
    } else if (off.elapsed > 600) {
      say(L.isDry(state.plant) ? '어서 와요!\n목이 많이 말랐어요…' : '어서 와요! 보고 싶었어요', 4500);
    }
    saveNow();
    pushSnapshot(true);
    requestAnimationFrame(loop);
  }

  // 개발/점검용 (개발자 도구 콘솔에서 __potpet 로 접근)
  window.__potpet = {
    get state() { return state; },
    pet, L, S, doWater, doStroke, newSeed, resetGame, setName, doNutri, setGrowSpeed: setSpeed, equip, dropToy, checkItems, itemQueue, say, snapshot,
    setSpeed: (n) => { testSpeed = n; },
  };

  init();
})();
