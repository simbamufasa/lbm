namespace ChargeManager.Core;

public sealed record ChargeProfile(string Name, int Start, int Stop, bool Unrestricted = false);
public sealed record BatteryReading(int Percent, bool OnAc, string Description);
public sealed record ThresholdReading(bool Enabled, int Start, int Stop, string Barcode);

public static class Profiles
{
    public static readonly ChargeProfile Daily = new("Daily", 60, 80);
    public static readonly ChargeProfile Gaming = new("Gaming / AC", 75, 80);
    public static readonly ChargeProfile Travel = new("Travel", 0, 100, true);
    public static ChargeProfile Custom(int start, int stop) => new("Custom", start, stop);

    public static void Validate(ChargeProfile profile)
    {
        if (profile.Unrestricted) return;
        if (profile.Start < 0 || profile.Stop > 99 || profile.Start >= profile.Stop)
            throw new ArgumentOutOfRangeException(nameof(profile), "Start must be below stop, and both limits must be between 0 and 99. Use Travel for 100%.");
    }
}
