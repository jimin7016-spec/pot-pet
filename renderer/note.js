/* 스티커 메모 창(상태 / 도감 / 옷장)의 화면 코드. 화분 창이 보내주는 snapshot 을 그려요. */
(() => {
  'use strict';
  const L = window.PotLogic, S = window.Sprites, api = window.noteApi;
  const kindRaw = new URLSearchParams(location.search).get('kind');
  const kind = kindRaw === 'dex' || kindRaw === 'closet' ? kindRaw : 'status';
  const $ = (id) => document.getElementById(id);
  const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text !== undefined) e.textContent = text; return e; };
  const body = $('body');
  let snap = null;
  let preview = false;
  const ui = {};

  $('title').textContent = { dex: '도감', closet: '옷장', status: '화분 상태' }[kind];
  document.title = $('title').textContent;
  $('close').addEventListener('click', () => api.hide());

  function gauge(label, cls) {
    const row = el('div', 'row');
    const g = el('div', 'gauge ' + cls), i = el('i');
    g.appendChild(i);
    row.append(el('span', '', label), g);
    return { row, g, i };
  }

  /* ---------- 상태 메모 ---------- */
  const mkBtn = (text, onClick, title) => {
    const b = el('button', 'nb', text);
    b.type = 'button';
    if (title) b.title = title;
    b.addEventListener('click', onClick);
    return b;
  };
  const TOY_NAMES = { ball: '공', yarn: '털실', butterfly: '나비', bubble: '비눗방울' };

  function buildStatus() {
    ui.name = el('div', 'name', '???');
    ui.sub = el('div', 'sub');
    // 이름 짓기
    ui.nick = el('input', 'nick');
    ui.nick.type = 'text';
    ui.nick.maxLength = L.NAME_MAX;
    ui.nick.placeholder = '이름 지어주기';
    ui.nick.setAttribute('aria-label', '식물 이름');
    const saveNick = () => api.action('name', ui.nick.value);
    ui.nick.addEventListener('keydown', (e) => { if (e.key === 'Enter') { saveNick(); ui.nick.blur(); } });
    const nickRow = el('div', 'btns');
    ui.nickBtn = mkBtn('저장', saveNick, '이름을 저장해요 (비우면 지워져요)');
    ui.nickBtn.style.flex = 'none';
    nickRow.append(ui.nick, ui.nickBtn);

    ui.water = gauge('물', '');
    ui.grow = gauge('성장', 'grow');
    ui.love = gauge('애정', 'love');
    ui.time = el('div', 'time');

    ui.waterBtn = mkBtn('물 주기', () => api.action('water'));
    ui.nutriBtn = mkBtn('영양제', () => api.action('nutri'), '1시간 동안 성장이 1.5배. 먹이고 2시간 뒤에 또 줄 수 있어요');
    const btns = el('div', 'btns');
    btns.append(ui.waterBtn, ui.nutriBtn);

    // 장난감
    ui.toys = {};
    const toyRow = el('div', 'toys');
    L.TOYS.forEach((id) => {
      const b = el('button', 'toy');
      b.type = 'button';
      b.title = `${TOY_NAMES[id]} 떨어뜨리기`;
      b.setAttribute('aria-label', `${TOY_NAMES[id]} 떨어뜨리기`);
      const c = document.createElement('canvas');
      c.width = 12; c.height = 12;
      const x = c.getContext('2d'); x.imageSmoothingEnabled = false;
      x.drawImage(S.toyCanvas(id, 0), 0, 0);
      b.appendChild(c);
      b.addEventListener('click', () => api.action('toy', id));
      ui.toys[id] = b;
      toyRow.appendChild(b);
    });

    // 성장 배속
    ui.speeds = {};
    const seg = el('div', 'seg');
    L.SPEEDS.forEach((n) => {
      const b = mkBtn(n === 1 ? '1배' : `${n}배`, () => api.action('speed', String(n)), n === 1 ? '기본 속도' : `성장이 ${n}배 빨라져요. 물은 그대로 마르니 자주 챙겨 주세요`);
      b.classList.add('segb');
      ui.speeds[n] = b;
      seg.appendChild(b);
    });

    ui.newBtn = mkBtn('새 씨앗 심기', () => api.action('newseed'));
    ui.replantBtn = mkBtn('갈아엎기', () => api.action('replant'), '이 화분을 버리고 새 씨앗을 심어요 (도감·아이템은 그대로)');
    const btns2 = el('div', 'btns');
    btns2.append(ui.newBtn, ui.replantBtn);
    ui.resetBtn = mkBtn('전부 초기화 (도감·아이템 포함)', () => api.action('resetall'), '도감과 아이템까지 모두 지우고 처음부터 시작해요');
    ui.resetBtn.classList.add('danger');

    const sw = el('div', 'swatches');
    ui.swatches = {};
    L.SKIN_IDS.forEach((id) => {
      const b = el('button', 'sw');
      b.type = 'button';
      b.style.background = S.SKINS[id].body;
      b.title = S.SKINS[id].name;
      b.setAttribute('aria-label', S.SKINS[id].name + ' 화분');
      b.addEventListener('click', () => api.action('skin', id));
      ui.swatches[id] = b;
      sw.appendChild(b);
    });
    const tip = el('div', 'tip', '화분을 누른 채 좌우로 문지르면 쓰다듬어요.\nShift를 누른 채 끌면 옮길 수 있어요.\n우클릭하면 메뉴가 나와요.');
    body.append(ui.name, ui.sub, nickRow, ui.water.row, ui.grow.row, ui.love.row, ui.time, btns,
      el('div', 'label', '놀아주기 (Ctrl+Alt+T)'), toyRow,
      el('div', 'label', '성장 속도 (자리를 비우는 날)'), seg,
      btns2, ui.resetBtn, el('div', 'label', '화분 색'), sw, tip);
  }

  function updateStatus(s) {
    const sp = s.name ? `${s.name}  ${'★'.repeat(s.stars)}` : s.stage === 0 ? '씨앗' : '무슨 식물일까?';
    ui.name.textContent = s.nick ? (s.name ? `${s.nick} (${s.name})` : s.nick) : sp;
    if (document.activeElement !== ui.nick) ui.nick.value = s.nick || '';
    ui.sub.textContent = s.done ? `${s.rarity} · 수확 완료! 새 씨앗을 심어 보세요.`
      : s.soggy && s.thirsty ? '물이 모자라졌어요. 이제 물을 줘도 돼요!'
      : s.soggy ? '물을 너무 많이 줬어요! 다음 성장 때 회복해요.'
      : s.wilted ? '목말라서 시들었어요. 물을 주면 다시 자라요.'
      : s.full ? '물이 가득해요. 더 주려면 한 번 더 눌러야 해요 (시들어요!)'
      : s.napping ? '낮잠 자는 중…'
      : s.stage === 2 ? `${s.stageName} · 잎 색을 잘 보세요`
      : s.stageName;
    ui.water.i.style.width = s.water + '%';
    ui.water.g.classList.toggle('low', s.thirsty);
    ui.grow.i.style.width = s.growth + '%';
    ui.love.i.style.width = s.love + '%';
    const sp2 = s.testSpeed || 1, f = (sec) => L.fmtLeft(sec / sp2);
    if (s.done || !s.left) ui.time.textContent = '';
    else {
      const lines = [`다 클 때까지 약 ${f(s.left.done)}`];
      if (s.stage < 3) lines.push(`다음 단계까지 ${f(s.left.next)}`);
      lines.push(s.water <= 0 ? '물이 없어서 성장이 멈췄어요!' : `물은 약 ${f(s.waterLeft)} 뒤에 바닥나요`);
      if (s.soggy) lines.push('과습이라 성장이 절반 속도예요');
      if (s.growSpeed > 1) lines.push(`성장 ${s.growSpeed}배속 중`);
      if (s.nutri && s.nutri.activeMs > 0) lines.push(`영양제 효과 ${L.fmtLeft(s.nutri.activeMs / 1000)} 남음 (1.5배)`);
      ui.time.textContent = lines.join('\n');
    }
    ui.grow.row.title = `물 먹은 시간 ${s.hours}시간 / 8시간`;
    ui.water.row.style.display = s.done ? 'none' : 'flex';
    ui.waterBtn.disabled = s.done;
    const nw = s.nutri ? s.nutri.waitMs : 0;
    ui.nutriBtn.disabled = s.done;
    ui.nutriBtn.textContent = nw > 0 && !(s.nutri.activeMs > 0) ? `영양제 (${L.fmtLeft(nw / 1000)} 뒤)` : s.nutri && s.nutri.activeMs > 0 ? '영양제 먹는 중' : '영양제';
    ui.newBtn.disabled = !s.done;
    for (const id of Object.keys(ui.toys)) ui.toys[id].disabled = !!s.toy || !!s.napping;
    for (const n of Object.keys(ui.speeds)) ui.speeds[n].setAttribute('aria-pressed', String(s.growSpeed === Number(n)));
    for (const id of Object.keys(ui.swatches)) ui.swatches[id].setAttribute('aria-pressed', String(s.skin === id));
  }

  /* ---------- 도감 메모 ---------- */
  function buildDex() {
    ui.count = el('div', 'name', '');
    ui.preview = el('button', 'nb', '다 채운 모습 보기');
    ui.preview.type = 'button';
    ui.preview.style.flex = 'none';
    ui.preview.addEventListener('click', () => { preview = !preview; renderDex(); });
    const head = el('div', 'dhead');
    head.append(ui.count, ui.preview);
    ui.grid = el('div', 'dgrid');
    body.append(head, ui.grid);
  }

  function renderDex() {
    if (!snap) return;
    const col = snap.collection || {};
    ui.grid.textContent = '';
    let n = 0;
    L.SPECIES.forEach((sp) => {
      const got = col[sp.id], on = !!got || preview;
      if (got) n++;
      const card = el('div', 'card' + (on ? '' : ' locked'));
      const c = document.createElement('canvas');
      c.width = 32; c.height = 40;
      const x = c.getContext('2d');
      x.imageSmoothingEnabled = false;
      if (on) {
        x.drawImage(S.potCanvas(got ? got.skin : snap.skin, 'happy', 0, 0), 0, 0);
        x.drawImage(S.plantCanvas(sp.id, 4, false), 0, 0);
      } else x.drawImage(S.silhouetteCanvas(sp.id), 0, 0);
      const r = L.RARITY[sp.rarity];
      card.append(c, el('div', 'n', on ? sp.name : '???'), el('div', 'r', on ? `${'★'.repeat(r.stars)} ${r.label}${got ? ' ×' + got.count : ''}` : r.label));
      if (got && got.name) card.appendChild(el('div', 'r nickl', `“${got.name}”`));
      ui.grid.appendChild(card);
    });
    ui.count.textContent = `${n} / ${L.SPECIES.length}`;
    ui.preview.textContent = preview ? '미리보기 끄기' : '다 채운 모습 보기';
  }

  /* ---------- 옷장 메모 ---------- */
  const SLOT_NAMES = { head: '머리', face: '얼굴', neck: '목' };
  function buildCloset() {
    ui.cCount = el('div', 'name', '');
    ui.cInfo = el('div', 'sub', '아이템을 누르면 입고, 다시 누르면 벗어요.');
    ui.cBody = el('div', 'closet');
    body.append(ui.cCount, ui.cInfo, ui.cBody);
  }
  function renderCloset() {
    if (!snap) return;
    const owned = new Set((snap.items && snap.items.owned) || []), eq = (snap.items && snap.items.equipped) || {};
    ui.cBody.textContent = '';
    for (const slot of L.SLOTS) {
      ui.cBody.appendChild(el('div', 'label', SLOT_NAMES[slot]));
      const grid = el('div', 'dgrid');
      L.ITEMS.filter((i) => i.slot === slot).forEach((it) => {
        const has = owned.has(it.id), on = eq[slot] === it.id;
        const card = el('button', 'card item' + (has ? '' : ' locked') + (on ? ' worn' : ''));
        card.type = 'button';
        card.disabled = !has;
        card.setAttribute('aria-pressed', String(on));
        const c = document.createElement('canvas');
        c.width = 32; c.height = 40;
        const x = c.getContext('2d'); x.imageSmoothingEnabled = false;
        const head = it.slot === 'head';
        x.drawImage(S.potCanvas(snap.skin, 'happy', 0, 0, head ? '' : it.id), 0, 0);
        if (head) x.drawImage(S.headCanvas(it.id), 0, 0);
        if (!has) { c.style.filter = 'brightness(0) opacity(0.35)'; }
        card.append(c, el('div', 'n', has ? it.name : '???'), el('div', 'r', has ? (on ? '착용 중' : '입기') : it.how));
        if (has) card.addEventListener('click', () => api.action('equip', it.id));
        grid.appendChild(card);
      });
      ui.cBody.appendChild(grid);
    }
    ui.cCount.textContent = `${owned.size} / ${L.ITEMS.length}`;
  }

  /* ---------- 시작 ---------- */
  if (kind === 'dex') buildDex(); else if (kind === 'closet') buildCloset(); else buildStatus();
  let lastCol = '';
  api.onState((s) => {
    snap = s;
    if (kind === 'status') updateStatus(s);
    else if (kind === 'closet') {
      const key = JSON.stringify(s.items) + s.skin;
      if (key !== lastCol) { lastCol = key; renderCloset(); }
    } else {
      const key = JSON.stringify(s.collection) + s.skin;
      if (key !== lastCol) { lastCol = key; renderDex(); }
    }
  });
  api.requestState();
})();
