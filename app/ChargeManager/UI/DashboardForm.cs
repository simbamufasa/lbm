using ChargeManager.Core;

namespace ChargeManager.UI;

public sealed class DashboardForm : Form
{
    private static readonly Color Background = Color.FromArgb(16, 19, 26);
    private static readonly Color Surface = Color.FromArgb(29, 34, 44);
    private static readonly Color TextColor = Color.FromArgb(240, 244, 250);
    private static readonly Color Muted = Color.FromArgb(162, 174, 190);
    private static readonly Color Accent = Color.FromArgb(132, 222, 160);

    private readonly LbmClient _client;
    private readonly ProfileStore _store;
    private Settings _settings;
    private readonly Label _percentage, _connection, _limits, _message;
    private readonly NumericUpDown _start, _stop;
    private readonly Button[] _buttons;
    private bool _busy;

    public DashboardForm(LbmClient client, ProfileStore store)
    {
        _client = client;
        _store = store;
        _settings = store.Load();

        Text = "Charge Manager";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 565);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10);

        Controls.Add(Label("CHARGE MANAGER", 22, 20, 390, 20, 10, Accent, FontStyle.Bold));
        Controls.Add(Label("Battery, on your terms.", 22, 44, 395, 40, 19, TextColor, FontStyle.Bold));
        Controls.Add(new Panel { Location = new Point(18, 98), Size = new Size(404, 112), BackColor = Surface });
        _percentage = Label("--%", 33, 112, 150, 49, 32, TextColor, FontStyle.Bold);
        _connection = Label("Reading power status…", 34, 164, 367, 25, 10, Muted);
        _limits = Label("Reading thresholds…", 192, 120, 214, 58, 10, Accent);
        Controls.Add(_percentage);
        Controls.Add(_connection);
        Controls.Add(_limits);

        Controls.Add(Label("PROFILES", 23, 231, 370, 24, 10, Muted, FontStyle.Bold));
        _start = NumberInput(22, 411, _settings.CustomStart);
        _stop = NumberInput(223, 411, _settings.CustomStop);
        _buttons = new[] {
            ModeButton("Daily   60–80%", 22, 264, () => Profiles.Daily),
            ModeButton("Gaming   75–80%", 223, 264, () => Profiles.Gaming),
            ModeButton("Travel   to 100%", 22, 319, () => Profiles.Travel),
            ModeButton("Custom limits", 223, 319, () => Profiles.Custom((int)_start.Value, (int)_stop.Value))
        };
        Controls.Add(Label("CUSTOM THRESHOLDS", 23, 381, 380, 24, 10, Muted, FontStyle.Bold));
        Controls.Add(_start);
        Controls.Add(_stop);
        Controls.Add(Label("Start %", 92, 419, 94, 24, 10, Muted));
        Controls.Add(Label("Stop %", 293, 419, 94, 24, 10, Muted));
        _message = Label("Choose a profile to apply it.", 22, 473, 396, 65, 10, Muted);
        Controls.Add(_message);

        Shown += async (_, _) => await RefreshLimitsAsync();
        RefreshBattery();
    }

    public void RefreshBattery()
    {
        var b = BatteryInfo.Read();
        _percentage.Text = b.Percent < 0 ? "--%" : $"{b.Percent}%";
        _connection.Text = b.Description;
    }

    private async Task RefreshLimitsAsync()
    {
        try
        {
            var t = await _client.StatusAsync();
            _limits.Text = t.Enabled ? $"Start below {t.Start}%\nStop at {t.Stop}%" : "No charge cap\nUp to 100%";
        }
        catch (Exception e)
        {
            _limits.Text = "LBM unavailable";
            _message.ForeColor = Color.FromArgb(245, 155, 155);
            ShowFailure("Unable to read charging thresholds", e);
        }
    }

    private async Task ApplyAsync(ChargeProfile profile)
    {
        if (_busy) return;
        _busy = true;
        foreach (var button in _buttons) button.Enabled = false;
        try
        {
            Profiles.Validate(profile);
            _message.ForeColor = Muted;
            _message.Text = $"Applying {profile.Name}… Approve UAC if requested.";
            await _client.ApplyAsync(profile);
            _settings = _settings with {
                LastProfile = profile.Name,
                CustomStart = (int)_start.Value,
                CustomStop = (int)_stop.Value
            };
            _store.Save(_settings);
            await RefreshLimitsAsync();
            _message.ForeColor = Accent;
            _message.Text = $"{profile.Name} applied and configuration verified. Firmware controls physical charging.";
        }
        catch (Exception e)
        {
            _message.ForeColor = Color.FromArgb(245, 155, 155);
            ShowFailure("Unable to apply charging profile", e);
        }
        finally
        {
            _busy = false;
            foreach (var button in _buttons) button.Enabled = true;
        }
    }

    private void ShowFailure(string title, Exception e)
    {
        _message.ForeColor = Color.FromArgb(245, 155, 155);
        _message.Text = title + ". See details.";
        MessageBox.Show(this, e.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private Button ModeButton(string name, int x, int y, Func<ChargeProfile> profile)
    {
        var b = new Button {
            Text = name, Location = new Point(x, y), Size = new Size(195, 45),
            BackColor = Surface, ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(65, 75, 90);
        b.Click += async (_, _) => await ApplyAsync(profile());
        Controls.Add(b);
        return b;
    }

    private static Label Label(string value, int x, int y, int width, int height, float size,
        Color color, FontStyle style = FontStyle.Regular) => new() {
        Text = value, Location = new Point(x, y), Size = new Size(width, height),
        Font = new Font("Segoe UI", size, style), ForeColor = color, BackColor = Color.Transparent
    };

    private static NumericUpDown NumberInput(int x, int y, int value) => new() {
        Location = new Point(x, y), Size = new Size(65, 32), Minimum = 0, Maximum = 99,
        Value = Math.Clamp(value, 0, 99),
        BackColor = Surface, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle
    };
}
