'use strict';
// 트레이/앱 아이콘 PNG 를 스프라이트에서 만들어 assets/ 에 저장:  node tools/make-assets.js
const fs = require('fs');
const path = require('path');
const S = require('../renderer/sprites.js');
const { encodePNG, blit } = require('./png.js');

const outDir = path.join(__dirname, '..', 'assets');
fs.mkdirSync(outDir, { recursive: true });

function icon(size, scale) {
  const buf = new Uint8Array(size * size * 4); // 투명 배경
  const crop = (g) => ({ w: g.w, h: 32, d: g.d.slice(8, 40) }); // 위쪽 빈 줄은 잘라냄
  const ox = Math.floor((size - 32 * scale) / 2), oy = Math.floor((size - 32 * scale) / 2);
  blit(buf, size, crop(S.potGrid('terra', 'happy', 0, 0)), S.toRGBA, ox, oy, scale);
  blit(buf, size, crop(S.plantGrid('tomato', 4, false)), S.toRGBA, ox, oy, scale);
  return encodePNG(size, size, Buffer.from(buf));
}

fs.writeFileSync(path.join(outDir, 'tray.png'), icon(32, 1));
fs.writeFileSync(path.join(outDir, 'icon.png'), icon(256, 8));
console.log('아이콘 저장:', outDir);
