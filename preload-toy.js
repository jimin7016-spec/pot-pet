'use strict';
const { contextBridge, ipcRenderer } = require('electron');

// 장난감 창(toy.html)용: 화면 위에서 떨어지는 장난감을 그려요
contextBridge.exposeInMainWorld('toyApi', {
  onSpawn: (cb) => ipcRenderer.on('toy-spawn', (_e, o) => cb(o)),
  onKick: (cb) => ipcRenderer.on('toy-kick', (_e, o) => cb(o)),
  pos: (o) => ipcRenderer.send('toy-pos', o),
  end: () => ipcRenderer.send('toy-end'),
});
