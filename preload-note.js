'use strict';
const { contextBridge, ipcRenderer } = require('electron');

// 스티커 메모 창(note.html)용
contextBridge.exposeInMainWorld('noteApi', {
  onState: (cb) => ipcRenderer.on('note-state', (_e, snap) => cb(snap)),
  requestState: () => ipcRenderer.send('note-request-state'),
  action: (type, value) => ipcRenderer.send('note-action', { type, value }),
  hide: () => ipcRenderer.send('note-hide'),
});
