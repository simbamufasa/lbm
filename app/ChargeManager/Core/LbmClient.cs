using System.Diagnostics;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ChargeManager.Core;

/// <summary>
/// Adapter to the unmodified upstream LBM binary. No registry writes or direct IOCTLs.
/// All writes are user-initiated. LBM performs its own UAC elevation.
/// </summary>
public sealed class LbmClient
{
    public string ExecutablePath { get; } = Path.Combine(AppContext.BaseDirectory, "lbm.exe");

    public async Task<ThresholdReading> StatusAsync(CancellationToken token = default)
    {
        var (code, output) = await RunAsync("--status", TimeSpan.FromSeconds(20), token);
        if (code != 0 || output.Contains("Error!", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LBM status failed: " + output.Trim());

        string Field(string name)
        {
            var match = Regex.Match(output, @"^\[\+\]\s*" + Regex.Escape(name) + @"\s*:\s*(.+)$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : throw new FormatException("Missing LBM field: " + name);
        }

        static int Percent(string value)
        {
            if (!int.TryParse(value.TrimEnd('%'), out int result) || result < 0 || result > 100 || !value.EndsWith('%'))
                throw new FormatException("Invalid percentage in LBM output: " + value);
            return result;
        }

        return new(
            Field("Threshold Status").Equals("ENABLED", StringComparison.OrdinalIgnoreCase),
            Percent(Field("Start Threshold")),
            Percent(Field("Stop Threshold")),
            Field("Battery Barcode"));
    }

    public async Task ApplyAsync(ChargeProfile profile, CancellationToken token = default)
    {
        Profiles.Validate(profile);
        string args = profile.Unrestricted ? "--disable" : $"--set {profile.Start} {profile.Stop}";

        // LBM uses a Windows GUI subsystem and can relaunch elevated. Success text
        // may appear in the elevated process rather than in the original child's pipe.
        // Therefore a write's stdout/exit code alone is NOT the proof of success.
        var (code, output) = await RunAsync(args, TimeSpan.FromSeconds(90), token);
        if (code != 0 || output.Contains("Error!", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LBM rejected the request or elevation was cancelled: " + output.Trim());

        // Poll for the expected configuration to allow the elevated process to finish.
        // Read-back confirms configuration, NOT physical battery bypass.
        var deadline = Stopwatch.StartNew();
        string lastError = "No matching threshold read-back.";
        while (deadline.Elapsed < TimeSpan.FromSeconds(25))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var current = await StatusAsync(token);
                if (profile.Unrestricted ? !current.Enabled :
                    current.Enabled && current.Start == profile.Start && current.Stop == profile.Stop)
                    return;
                lastError = $"Current: {(current.Enabled ? "enabled" : "disabled")} {current.Start}/{current.Stop}.";
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or TimeoutException)
            {
                lastError = ex.Message;
            }
            await Task.Delay(1000, token);
        }
        throw new InvalidOperationException("LBM write was not verified. " + lastError);
    }

    private async Task<(int ExitCode, string Output)> RunAsync(string args, TimeSpan timeout, CancellationToken token)
    {
        if (!File.Exists(ExecutablePath))
            throw new FileNotFoundException("Build LBM and copy bin\\lbm.exe alongside ChargeManager.exe.", ExecutablePath);
        var start = new ProcessStartInfo(ExecutablePath, args)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("LBM did not start.");
        }
        catch (Win32Exception ex)
        {
            string explanation = ex.NativeErrorCode switch
            {
                2 or 3 => "LBM was not found at the expected path.",
                5 => "Windows denied access to LBM. Check Windows Security > Protection history for a quarantine or block.",
                193 or 216 => "Windows rejected this LBM executable as incompatible or invalid.",
                740 => "Windows requires elevation to start LBM.",
                _ => "Windows could not start the LBM executable."
            };
            throw new InvalidOperationException(
                $"{explanation}\\nPath: {ExecutablePath}\\nWindows error {ex.NativeErrorCode}: {ex.Message}", ex);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            return (process.ExitCode, (await stdout) + Environment.NewLine + (await stderr));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill();
            throw new TimeoutException("LBM timed out. A UAC prompt may have been left unanswered.");
        }
    }
}
