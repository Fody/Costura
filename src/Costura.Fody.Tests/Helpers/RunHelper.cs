using System.Diagnostics;

internal static class RunHelper
{
    public static string RunExecutable(string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();
        return process.StandardOutput.ReadToEnd();
    }
}
