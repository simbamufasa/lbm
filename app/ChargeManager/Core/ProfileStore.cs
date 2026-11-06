using System.Text.Json;

namespace ChargeManager.Core;

public sealed record Settings
{
    public int CustomStart { get; init; } = 50;
    public int CustomStop { get; init; } = 85;
    public string LastProfile { get; init; } = "";
}

public sealed class ProfileStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChargeManager", "settings.json");

    public Settings Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new() : new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
    }

    public void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }
}

public static class BatteryInfo
{
    public static BatteryReading Read()
    {
        var power = SystemInformation.PowerStatus;
        int percent = power.BatteryLifePercent >= 0 ? (int)Math.Round(power.BatteryLifePercent * 100) : -1;
        bool ac = power.PowerLineStatus == PowerLineStatus.Online;
        return new(percent, ac, ac ? "External power connected" : "Running on battery");
    }
}
