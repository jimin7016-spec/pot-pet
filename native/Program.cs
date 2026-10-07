// 시작점. 한 번에 하나만 실행돼요: 이미 켜져 있으면 그쪽 화분을 화면 아래로 불러오고 끝나요.
using System;
using System.Threading;
using System.Windows.Forms;

namespace PotPet {
  static class Program {
    [STAThread]
    static int Main(string[] args) {
      if (args.Length > 0 && args[0] == "--self-test") return SelfTest.Run();

      // 점검용: 말풍선 그림을 PNG 로 (글자 속 \n 은 줄바꿈)
      if (args.Length > 2 && args[0] == "--snap-bubble") { Native.InitDpi(); using (var bb = new Bubble()) { bb.SetText(args[2].Replace("\\n", "\n")); bb.SaveTo(args[1]); } return 0; }

      bool first;
      using (var mutex = new Mutex(true, "Local\\PotPet.Native", out first))
      using (var summon = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PotPet.Native.Summon")) {
        if (!first) { summon.Set(); return 0; }
        Native.InitDpi();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var app = new App();
        // 두 번째로 실행하면 이미 떠 있는 화분을 불러와요
        var waiter = ThreadPool.RegisterWaitForSingleObject(summon, (s, timedOut) => {
          try { app.Game.BeginInvoke((Action)app.SummonFromSecondLaunch); } catch { }
        }, null, -1, false);
        Application.Run(app);
        waiter.Unregister(null);
        GC.KeepAlive(mutex);
      }
      return 0;
    }
  }
}
