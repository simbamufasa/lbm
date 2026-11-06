using ChargeManager.Core;
using ChargeManager.UI;

namespace ChargeManager;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\ChargeManager_LBM_Tray", out bool first);
        if (!first) return;
        Application.Run(new TrayContext(new LbmClient(), new ProfileStore()));
    }
}
