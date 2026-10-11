using LiteDrops.UI;

namespace LiteDrops;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\LiteDrops.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("LiteDrops is already running (check the system tray).", "LiteDrops",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(AppSettings.Load()));
    }
}
