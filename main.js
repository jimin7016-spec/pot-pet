'use strict';
const { app, BrowserWindow, ipcMain, screen, Menu, Tray, nativeImage, globalShortcut, powerMonitor } = require('electron');
const path = require('path');
const fs = require('fs');

if (!app.requestSingleInstanceLock()) app.quit();

const HIDE_KEY = 'CommandOrControl+Alt+P';
const TOY_KEY = 'CommandOrControl+Alt+T';
const TOYS = ['ball', 'yarn', 'butterfly', 'bubble'];
const NOTE_DEFS = {
  status: { title: '화분 상태', w: 240, h: 700, offsetX: 0 },
  dex: { title: '도감', w: 330, h: 540, offsetX: 360 },
  closet: { title: '옷장', w: 300, h: 580, offsetX: 700 },
};

let win = null; // 화분 창
let tray = null;
let size = { w: 220, h: 230 }; // 렌더러가 set-size 로 덮어씀
let quitting = false;
let allHidden = false;
let lastNoteState = null;
const notes = {}; // kind -> BrowserWindow
let ui = { notes: { status: { visible: true }, dex: { visible: false }, closet: { visible: false } } };

const assetPath = (name) => path.join(__dirname, 'assets', name);
const gameFile = () => path.join(app.getPath('userData'), 'potpet-save.json');
const uiFile = () => path.join(app.getPath('userData'), 'potpet-ui.json');

function send(channel, ...args) {
  if (win && !win.isDestroyed()) win.webContents.send(channel, ...args);
}

/* ---------- 저장 ---------- */
function readJson(file) {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch (e) { return null; }
}
function writeJson(file, data) {
  try {
    fs.mkdirSync(path.dirname(file), { recursive: true });
    const tmp = file + '.tmp';
    fs.writeFileSync(tmp, JSON.stringify(data));
    fs.renameSync(tmp, file);
    return true;
  } catch (e) {
    console.error('save failed', file, e);
    return false;
  }
}
let uiTimer = null;
function saveUi() {
  clearTimeout(uiTimer);
  uiTimer = setTimeout(() => writeJson(uiFile(), ui), 300);
}

/* ---------- 메뉴 (렌더러가 보낸 템플릿 → 네이티브 메뉴) ---------- */
const noteVisible = (kind) => !!(notes[kind] && !notes[kind].isDestroyed() && notes[kind].isVisible());

function toMenuTemplate(items) {
  return items.map((it) => {
    const o = {};
    if (it.type) o.type = it.type;
    if (typeof it.label === 'string') o.label = it.label;
    if (typeof it.checked === 'boolean') o.checked = it.checked;
    if (typeof it.enabled === 'boolean') o.enabled = it.enabled;
    if (Array.isArray(it.submenu)) o.submenu = toMenuTemplate(it.submenu);
    else if (typeof it.id === 'string') {
      if (it.id === 'ui:status') { o.checked = noteVisible('status'); o.click = () => toggleNote('status'); }
      else if (it.id === 'ui:dex') { o.checked = noteVisible('dex'); o.click = () => toggleNote('dex'); }
      else if (it.id === 'ui:closet') { o.checked = noteVisible('closet'); o.click = () => toggleNote('closet'); }
      else if (it.id === 'ui:hide') o.click = () => setAllHidden(true);
      else o.click = () => send('menu-action', it.id);
    }
    return o;
  });
}

function requestQuit() {
  send('save-and-quit');
  setTimeout(() => app.quit(), 1500); // 렌더러가 응답 못 해도 종료
}

/* ---------- 화분 창 ---------- */
function createWindow() {
  const wa = screen.getPrimaryDisplay().workArea;
  const iconFile = assetPath('icon.png');
  win = new BrowserWindow({
    width: size.w,
    height: size.h,
    x: Math.round(wa.x + wa.width / 2 - size.w / 2),
    y: Math.round(wa.y + wa.height - size.h),
    transparent: true,
    frame: false,
    resizable: false,
    maximizable: false,
    minimizable: false,
    fullscreenable: false,
    hasShadow: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    show: false,
    icon: fs.existsSync(iconFile) ? iconFile : undefined,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      backgroundThrottling: false,
    },
  });
  win.setAlwaysOnTop(true, 'screen-saver');
  win.setIgnoreMouseEvents(true, { forward: true });
  win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  win.once('ready-to-show', () => { if (!allHidden) win.showInactive(); });
  win.on('closed', () => { win = null; });
  if (process.env.POTPET_DEBUG) win.webContents.openDevTools({ mode: 'detach' });
}

/* ---------- 스티커 메모 창 (상태 / 도감) ---------- */
function defaultNotePos(kind) {
  const wa = screen.getPrimaryDisplay().workArea;
  const d = NOTE_DEFS[kind];
  return { x: wa.x + wa.width - d.w - 24 - d.offsetX, y: wa.y + 40 };
}
function posOnScreen(p, d) {
  if (!p || !Number.isFinite(p.x) || !Number.isFinite(p.y)) return false;
  return screen.getAllDisplays().some((disp) => {
    const b = disp.bounds;
    return p.x + 40 > b.x && p.x < b.x + b.width - 40 && p.y >= b.y - 4 && p.y < b.y + b.height - 40;
  });
}

function createNote(kind) {
  const def = NOTE_DEFS[kind];
  const saved = ui.notes[kind] || {};
  const pos = posOnScreen(saved, def) ? { x: saved.x, y: saved.y } : defaultNotePos(kind);
  const w = new BrowserWindow({
    width: def.w,
    height: def.h,
    x: Math.round(pos.x),
    y: Math.round(pos.y),
    frame: false,
    resizable: false,
    maximizable: false,
    minimizable: false,
    fullscreenable: false,
    skipTaskbar: true,
    alwaysOnTop: true,
    show: false,
    backgroundColor: '#fff3a6',
    title: def.title,
    webPreferences: {
      preload: path.join(__dirname, 'preload-note.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });
  w.setAlwaysOnTop(true, 'floating'); // 화분(screen-saver 레벨)보다는 아래
  w.loadFile(path.join(__dirname, 'renderer', 'note.html'), { query: { kind } });
  w.on('close', (e) => {
    if (quitting) return;
    e.preventDefault(); // 닫기 = 숨기기. 화분 메뉴나 트레이에서 다시 열 수 있어요.
    hideNote(kind);
  });
  w.on('move', () => {
    const b = w.getBounds();
    ui.notes[kind] = Object.assign(ui.notes[kind] || {}, { x: b.x, y: b.y });
    saveUi();
  });
  w.webContents.on('did-finish-load', () => { if (lastNoteState) w.webContents.send('note-state', lastNoteState); });
  notes[kind] = w;
  return w;
}

function showNote(kind) {
  ui.notes[kind] = Object.assign(ui.notes[kind] || {}, { visible: true });
  saveUi();
  if (allHidden) return;
  const w = notes[kind] && !notes[kind].isDestroyed() ? notes[kind] : createNote(kind);
  if (lastNoteState) w.webContents.send('note-state', lastNoteState);
  w.showInactive();
}
function hideNote(kind) {
  ui.notes[kind] = Object.assign(ui.notes[kind] || {}, { visible: false });
  saveUi();
  if (notes[kind] && !notes[kind].isDestroyed()) notes[kind].hide();
}
function toggleNote(kind) {
  if (noteVisible(kind)) hideNote(kind);
  else showNote(kind);
}

/** 화분과 메모를 한꺼번에 숨기기 / 다시 보이기 (단축키, 트레이) */
function setAllHidden(h) {
  allHidden = h;
  if (win && !win.isDestroyed()) { if (h) win.hide(); else win.showInactive(); }
  for (const kind of Object.keys(NOTE_DEFS)) {
    if (h) { if (notes[kind] && !notes[kind].isDestroyed()) notes[kind].hide(); }
    else if (ui.notes[kind] && ui.notes[kind].visible) showNote(kind);
  }
  rebuildTray();
}

/* ---------- 트레이 ---------- */
function rebuildTray() {
  if (!tray) return;
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: allHidden ? '화분 다시 보이기' : '화분 숨기기', click: () => setAllHidden(!allHidden) },
    { label: '화분 불러오기 (화면 아래로)', click: () => { if (allHidden) setAllHidden(false); send('reset-pos'); } },
    { type: 'separator' },
    { label: '상태 메모', type: 'checkbox', checked: noteVisible('status'), click: () => toggleNote('status') },
    { label: '도감 메모', type: 'checkbox', checked: noteVisible('dex'), click: () => toggleNote('dex') },
    { label: '옷장 메모', type: 'checkbox', checked: noteVisible('closet'), click: () => toggleNote('closet') },
    { type: 'separator' },
    { label: '종료', click: requestQuit },
  ]));
}
function createTray() {
  try {
    const img = nativeImage.createFromPath(assetPath('tray.png'));
    if (img.isEmpty()) return;
    tray = new Tray(img.resize({ width: 16, height: 16 }));
    tray.setToolTip('화분 펫');
    tray.on('click', () => rebuildTray());
    rebuildTray();
  } catch (e) {
    console.error('tray failed', e);
  }
}

/* ---------- IPC ---------- */
ipcMain.handle('get-env', () => {
  const cursor = screen.getCursorScreenPoint();
  const b = win && !win.isDestroyed() ? win.getBounds() : { x: 0, y: 0, width: size.w, height: size.h };
  const primary = screen.getPrimaryDisplay().id;
  const displays = screen.getAllDisplays()
    .map((d) => ({ id: d.id, bounds: d.bounds, workArea: d.workArea }))
    .sort((a, c) => (a.id === primary ? -1 : c.id === primary ? 1 : 0)); // 첫 번째가 주 모니터
  let idle = 0;
  try { idle = powerMonitor.getSystemIdleTime(); } catch (e) { /* 지원 안 되면 낮잠 없음 */ }
  return { cursor, displays, bounds: b, idle };
});

ipcMain.on('set-bounds', (_e, x, y) => {
  if (!win || win.isDestroyed() || !Number.isFinite(x) || !Number.isFinite(y)) return;
  // 크기를 매번 같이 넘겨야 Windows 배율(125% 등)에서 창이 조금씩 커지지 않아요
  win.setBounds({ x: Math.round(x), y: Math.round(y), width: size.w, height: size.h });
});

ipcMain.on('set-size', (_e, w, h) => {
  if (!Number.isFinite(w) || !Number.isFinite(h)) return;
  size = { w: Math.round(w), h: Math.round(h) };
  if (!win || win.isDestroyed()) return;
  const b = win.getBounds();
  // 아래쪽 가운데를 기준으로 크기만 바꿈
  win.setBounds({ x: Math.round(b.x + b.width / 2 - size.w / 2), y: Math.round(b.y + b.height - size.h), width: size.w, height: size.h });
});

ipcMain.on('set-ignore', (_e, ignore) => {
  if (!win || win.isDestroyed()) return;
  if (ignore) win.setIgnoreMouseEvents(true, { forward: true });
  else win.setIgnoreMouseEvents(false);
});

ipcMain.handle('load-state', () => readJson(gameFile()));
ipcMain.handle('save-state', (_e, state) => writeJson(gameFile(), state));

ipcMain.on('show-menu', (_e, template) => {
  if (!win || win.isDestroyed() || !Array.isArray(template)) return;
  Menu.buildFromTemplate(toMenuTemplate(template)).popup({ window: win });
});

// 화분 → 메모 창들
ipcMain.on('note-state', (_e, snap) => {
  lastNoteState = snap;
  for (const w of Object.values(notes)) if (w && !w.isDestroyed() && w.isVisible()) w.webContents.send('note-state', snap);
});
ipcMain.on('note-request-state', (e) => { if (lastNoteState) e.sender.send('note-state', lastNoteState); });
// 메모 창 → 화분
ipcMain.on('note-action', (_e, a) => {
  if (!a || typeof a.type !== 'string') return;
  if (!['water', 'newseed', 'replant', 'resetall', 'skin', 'name', 'nutri', 'speed', 'equip', 'toy'].includes(a.type)) return;
  send('note-action', { type: a.type, value: typeof a.value === 'string' ? a.value.slice(0, 40) : undefined });
});
ipcMain.on('note-hide', (e) => {
  for (const kind of Object.keys(notes)) if (notes[kind] && !notes[kind].isDestroyed() && notes[kind].webContents === e.sender) hideNote(kind);
});

/* ---------- 장난감 창: 화분이 있는 모니터 전체를 덮는 투명 창(클릭은 통과) ---------- */
let toyWin = null;
let toyOrigin = { x: 0, y: 0 };
function closeToy() {
  if (toyWin && !toyWin.isDestroyed()) toyWin.destroy();
  toyWin = null;
}
ipcMain.on('drop-toy', (_e, o) => {
  if (!o || !TOYS.includes(o.kind) || !Number.isFinite(o.px) || !Number.isFinite(o.py) || allHidden) return;
  closeToy();
  const d = screen.getDisplayNearestPoint({ x: Math.round(o.px), y: Math.round(o.py - 10) });
  const b = d.bounds;
  const cur = screen.getCursorScreenPoint();
  const onThis = cur.x >= b.x + 40 && cur.x <= b.x + b.width - 40;
  const sx = onThis ? cur.x : Math.min(b.x + b.width - 60, Math.max(b.x + 60, o.px + (Math.random() * 300 - 150)));
  toyOrigin = { x: b.x, y: b.y };
  toyWin = new BrowserWindow({
    x: b.x, y: b.y, width: b.width, height: b.height,
    transparent: true, frame: false, resizable: false, movable: false, focusable: false,
    maximizable: false, minimizable: false, fullscreenable: false, hasShadow: false,
    skipTaskbar: true, alwaysOnTop: true, show: false,
    webPreferences: { preload: path.join(__dirname, 'preload-toy.js'), contextIsolation: true, nodeIntegration: false, sandbox: true, backgroundThrottling: false },
  });
  const w = toyWin;
  w.setAlwaysOnTop(true, 'screen-saver');
  w.setIgnoreMouseEvents(true);
  w.loadFile(path.join(__dirname, 'renderer', 'toy.html'));
  w.webContents.once('did-finish-load', () => {
    if (w.isDestroyed()) return;
    w.showInactive();
    w.webContents.send('toy-spawn', { kind: o.kind, x: sx - b.x, floorY: o.py - b.y });
    send('toy-start', { kind: o.kind, x: sx, floorY: o.py });
  });
  w.on('closed', () => { if (toyWin === w) toyWin = null; });
});
ipcMain.on('toy-pos', (_e, p) => {
  if (!p || !Number.isFinite(p.x) || !Number.isFinite(p.y)) return;
  send('toy-pos', { x: p.x + toyOrigin.x, y: p.y + toyOrigin.y, vx: p.vx || 0, vy: p.vy || 0, kind: p.kind });
});
ipcMain.on('toy-kick', (_e, k) => {
  if (!k || !toyWin || toyWin.isDestroyed()) return;
  toyWin.webContents.send('toy-kick', { vx: Number(k.vx) || 0, vy: Number(k.vy) || 0 });
});
ipcMain.on('toy-end', () => { closeToy(); send('toy-end'); });
ipcMain.on('toy-cancel', () => { closeToy(); });

ipcMain.on('quit', () => app.quit());

/* ---------- 앱 ---------- */
app.on('second-instance', () => {
  if (allHidden) setAllHidden(false);
  send('reset-pos');
});
app.on('before-quit', () => { quitting = true; closeToy(); });
app.on('will-quit', () => globalShortcut.unregisterAll());
app.on('window-all-closed', () => app.quit());

app.whenReady().then(() => {
  const savedUi = readJson(uiFile());
  if (savedUi && savedUi.notes) ui = { notes: Object.assign({ status: { visible: true }, dex: { visible: false }, closet: { visible: false } }, savedUi.notes) };
  createWindow();
  createTray();
  for (const kind of Object.keys(NOTE_DEFS)) if (ui.notes[kind] && ui.notes[kind].visible) showNote(kind);
  try { globalShortcut.register(HIDE_KEY, () => setAllHidden(!allHidden)); } catch (e) { /* 단축키를 못 잡아도 트레이로 숨길 수 있어요 */ }
  try { globalShortcut.register(TOY_KEY, () => { if (!allHidden) send('toy-hotkey'); }); } catch (e) { /* 메뉴로도 장난감을 줄 수 있어요 */ }
});
