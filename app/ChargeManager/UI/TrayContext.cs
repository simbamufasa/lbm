using ChargeManager.Core;

namespace ChargeManager.UI;

public sealed class TrayContext : ApplicationContext
{
    private readonly DashboardForm _dashboard;
    private readonly NotifyIcon _notify;
    private readonly System.Windows.Forms.Timer _timer;

    public TrayContext(LbmClient client, ProfileStore store)
    {
        _dashboard = new DashboardForm(client, store);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Charge Manager", null, (_, _) => ShowDashboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _notify = new NotifyIcon {
            Icon = SystemIcons.Information,
            Text = "Charge Manager",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notify.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowDashboard(); };
        _notify.DoubleClick += (_, _) => ShowDashboard();

        _dashboard.FormClosing += (_, e) => {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            _dashboard.Hide();
        };
        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += (_, _) => _dashboard.RefreshBattery();
        _timer.Start();
    }

    private void ShowDashboard()
    {
        _dashboard.RefreshBattery();
        _dashboard.Show();
        _dashboard.WindowState = FormWindowState.Normal;
        _dashboard.Activate();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        _dashboard.Dispose();
        base.ExitThreadCore();
    }
}
