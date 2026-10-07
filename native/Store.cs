// 저장: 예전 Electron 버전과 같은 폴더·같은 파일(%APPDATA%\pot-pet\potpet-save.json, potpet-ui.json)을 써서 키우던 화분을 그대로 이어가요.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PotPet {
  public static class Store {
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "pot-pet");
    public static string GameFile { get { return Path.Combine(Dir, "potpet-save.json"); } }
    public static string UiFile { get { return Path.Combine(Dir, "potpet-ui.json"); } }

    static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }; }
    public static string Serialize(object o) { return Json().Serialize(o); }
    public static object Parse(string s) { try { return Json().DeserializeObject(s); } catch { return null; } }

    public static object Read(string file) {
      try { return File.Exists(file) ? Parse(File.ReadAllText(file, Encoding.UTF8)) : null; } catch { return null; }
    }
    /** 임시 파일에 쓴 뒤 바꿔치기해서, 쓰다가 꺼져도 저장본이 망가지지 않게 */
    public static bool Write(string file, object data) {
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        string tmp = file + ".tmp";
        File.WriteAllText(tmp, Serialize(data), new UTF8Encoding(false));
        if (File.Exists(file)) File.Replace(tmp, file, null); else File.Move(tmp, file);
        return true;
      } catch { return false; }
    }

    /* ---------- 메모 창 위치·표시 여부 ---------- */
    public class NoteUi { public bool Visible; public int? X, Y; }
    public static Dictionary<string, NoteUi> LoadUi() {
      var ui = new Dictionary<string, NoteUi> { { "status", new NoteUi { Visible = true } }, { "dex", new NoteUi() }, { "closet", new NoteUi() } };
      var raw = Read(UiFile) as Dictionary<string, object>;
      object notesObj;
      if (raw == null || !raw.TryGetValue("notes", out notesObj)) return ui;
      var notes = notesObj as Dictionary<string, object>;
      if (notes == null) return ui;
      foreach (var kind in new[] { "status", "dex", "closet" }) {
        object o; if (!notes.TryGetValue(kind, out o)) continue;
        var d = o as Dictionary<string, object>; if (d == null) continue;
        object v;
        if (d.TryGetValue("visible", out v) && v is bool) ui[kind].Visible = (bool)v;
        if (d.TryGetValue("x", out v) && (v is int || v is decimal || v is double)) ui[kind].X = (int)Convert.ToDouble(v);
        if (d.TryGetValue("y", out v) && (v is int || v is decimal || v is double)) ui[kind].Y = (int)Convert.ToDouble(v);
      }
      return ui;
    }
    public static void SaveUi(Dictionary<string, NoteUi> ui) {
      var notes = new Dictionary<string, object>();
      foreach (var kv in ui) {
        var d = new Dictionary<string, object> { { "visible", kv.Value.Visible } };
        if (kv.Value.X.HasValue) d["x"] = kv.Value.X.Value;
        if (kv.Value.Y.HasValue) d["y"] = kv.Value.Y.Value;
        notes[kv.Key] = d;
      }
      Write(UiFile, new Dictionary<string, object> { { "notes", notes } });
    }
  }
}
