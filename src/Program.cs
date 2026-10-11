using LiteDrops.UI;

namespace LiteDrops;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\LiteDrops.SingleInstance", out var isFirst);
        if (!isFirst && args.Contains("--restart"))
        {
            // restarted after a language change: wait for the previous instance to exit
            try { isFirst = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (AbandonedMutexException) { isFirst = true; }
        }
        var settings = AppSettings.Load();
        L.Init(settings.Language);
        if (!isFirst)
        {
            MessageBox.Show(L.AlreadyRunning, "LiteDrops", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(settings, startHidden: args.Contains("--tray")));
    }
}
