'use strict';
const { contextBridge, ipcRenderer } = require('electron');

// 화분 창(index.html)용
contextBridge.exposeInMainWorld('api', {
  getEnv: () => ipcRenderer.invoke('get-env'),
  setBounds: (x, y) => ipcRenderer.send('set-bounds', x, y),
  setSize: (w, h) => ipcRenderer.send('set-size', w, h),
  setIgnore: (ignore) => ipcRenderer.send('set-ignore', !!ignore),
  loadState: () => ipcRenderer.invoke('load-state'),
  saveState: (state) => ipcRenderer.invoke('save-state', state),
  showMenu: (template) => ipcRenderer.send('show-menu', template),
  sendNoteState: (snap) => ipcRenderer.send('note-state', snap),
  dropToy: (o) => ipcRenderer.send('drop-toy', o),
  toyKick: (o) => ipcRenderer.send('toy-kick', o),
  toyCancel: () => ipcRenderer.send('toy-cancel'),
  onToyStart: (cb) => ipcRenderer.on('toy-start', (_e, o) => cb(o)),
  onToyPos: (cb) => ipcRenderer.on('toy-pos', (_e, o) => cb(o)),
  onToyEnd: (cb) => ipcRenderer.on('toy-end', () => cb()),
  onToyHotkey: (cb) => ipcRenderer.on('toy-hotkey', () => cb()),
  quit: () => ipcRenderer.send('quit'),
  onMenuAction: (cb) => ipcRenderer.on('menu-action', (_e, id) => cb(id)),
  onNoteAction: (cb) => ipcRenderer.on('note-action', (_e, a) => cb(a)),
  onResetPos: (cb) => ipcRenderer.on('reset-pos', () => cb()),
  onSaveAndQuit: (cb) => ipcRenderer.on('save-and-quit', () => cb()),
});
