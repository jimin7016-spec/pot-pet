'use strict';
// 외부 패키지 없이 PNG 를 만드는 작은 인코더 (미리보기/아이콘 생성용)
const zlib = require('zlib');

const table = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf) {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = table[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function encodePNG(w, h, rgba) {
  const stride = w * 4;
  const raw = Buffer.alloc((stride + 1) * h);
  for (let y = 0; y < h; y++) {
    raw[y * (stride + 1)] = 0;
    Buffer.from(rgba.buffer, rgba.byteOffset + y * stride, stride).copy(raw, y * (stride + 1) + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // RGBA
  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(raw)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/** RGBA 캔버스에 그리드(스프라이트)를 정수 배율로 찍어 넣기 */
function blit(dst, dw, grid, toRGBA, ox, oy, scale) {
  const src = toRGBA(grid);
  for (let y = 0; y < grid.h; y++)
    for (let x = 0; x < grid.w; x++) {
      const i = (y * grid.w + x) * 4;
      if (!src[i + 3]) continue;
      for (let sy = 0; sy < scale; sy++)
        for (let sx = 0; sx < scale; sx++) {
          const j = ((oy + y * scale + sy) * dw + (ox + x * scale + sx)) * 4;
          dst[j] = src[i]; dst[j + 1] = src[i + 1]; dst[j + 2] = src[i + 2]; dst[j + 3] = 255;
        }
    }
}

function fill(dst, r, g, b, a) {
  for (let i = 0; i < dst.length; i += 4) { dst[i] = r; dst[i + 1] = g; dst[i + 2] = b; dst[i + 3] = a; }
}

module.exports = { encodePNG, blit, fill };
