'use strict';
// 스프라이트를 PNG 로 뽑아서 눈으로 확인하는 도구:  node tools/preview.js [출력폴더]
const fs = require('fs');
const path = require('path');
const S = require('../renderer/sprites.js');
const L = require('../renderer/logic.js');
const { encodePNG, blit, fill } = require('./png.js');

const outDir = process.argv[2] || path.join(__dirname, '..', 'preview');
fs.mkdirSync(outDir, { recursive: true });

const SCALE = 3;
const CELL_W = S.W * SCALE + 8;
const CELL_H = S.H * SCALE + 8;
const BG = [201, 214, 232];

function sheet(rows, cols, drawCell) {
  const w = cols * CELL_W, h = rows * CELL_H;
  const buf = new Uint8Array(w * h * 4);
  fill(buf, BG[0], BG[1], BG[2], 255);
  for (let r = 0; r < rows; r++) for (let c = 0; c < cols; c++) drawCell(buf, w, c * CELL_W + 4, r * CELL_H + 4, r, c);
  return encodePNG(w, h, Buffer.from(buf));
}

// 1) 식물 10종 × (씨앗, 새싹, 쑥쑥, 모양, 수확, 시든 모습)
const species = L.SPECIES.map((s) => s.id);
fs.writeFileSync(path.join(outDir, 'species.png'), sheet(species.length, 6, (buf, w, ox, oy, r, c) => {
  const stage = c < 5 ? c : 3;
  const wilt = c === 5;
  blit(buf, w, S.potGrid(S.SKIN_IDS[r % S.SKIN_IDS.length], wilt ? 'sad' : 'idle', 0, 0), S.toRGBA, ox, oy, SCALE);
  blit(buf, w, S.plantGrid(species[r], stage, wilt), S.toRGBA, ox, oy, SCALE);
}));

// 2) 화분 색 6종 × 표정/동작
const faces = [['idle', 0, 0], ['blink', 0, 0], ['happy', 0, 0], ['love', 0, 0], ['sad', 0, 0], ['sleep', 0, 0], ['idle', 1, 1], ['idle', 1, 2]];
fs.writeFileSync(path.join(outDir, 'pots.png'), sheet(S.SKIN_IDS.length, faces.length, (buf, w, ox, oy, r, c) => {
  const [face, look, feet] = faces[c];
  blit(buf, w, S.potGrid(S.SKIN_IDS[r], face, look, feet), S.toRGBA, ox, oy, SCALE);
  blit(buf, w, S.plantGrid('basil', 3, face === 'sad'), S.toRGBA, ox, oy, SCALE);
}));

// 3) 꾸미기 아이템 8종 × 화분 색 6종
const items = L.ITEMS.map((i) => i.id);
fs.writeFileSync(path.join(outDir, 'items.png'), sheet(S.SKIN_IDS.length, items.length, (buf, w, ox, oy, r, c) => {
  const it = L.ITEMS[c], head = it.slot === 'head';
  blit(buf, w, S.potGrid(S.SKIN_IDS[r], c === 4 ? 'idle' : 'happy', 0, 0, head ? '' : items[c]), S.toRGBA, ox, oy, SCALE);
  blit(buf, w, S.plantGrid('basil', 3, false), S.toRGBA, ox, oy, SCALE);
  if (head) blit(buf, w, S.headGrid(items[c]), S.toRGBA, ox, oy, SCALE);
}));

// 4) 장난감 4종 × 2프레임
const toys = L.TOYS;
fs.writeFileSync(path.join(outDir, 'toys.png'), sheet(1, toys.length * 2, (buf, w, ox, oy, r, c) => {
  blit(buf, w, S.toyGrid(toys[c >> 1], c & 1), S.toRGBA, ox, oy + 20, SCALE * 2);
}));

console.log('미리보기 저장:', outDir);
