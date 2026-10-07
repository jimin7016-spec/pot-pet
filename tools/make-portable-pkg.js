'use strict';
// 설치 없이 실행되는 폴더용 package.json 만들기 (make-portable.bat 이 불러요)
const fs = require('fs');
const p = require('../package.json');
const out = process.argv[2];
if (!out) { console.error('사용법: node tools/make-portable-pkg.js <출력 경로>'); process.exit(1); }
fs.writeFileSync(out, JSON.stringify({ name: p.name, version: p.version, description: p.description, main: p.main, productName: '화분 펫' }, null, 2));
