using System.Diagnostics;
using System.IO;

internal static class RunHelper
{
    public static string RunExecutable(string executablePath)
    {
        if (executablePath.EndsWith(".dll"))
        {
            executablePath = Path.ChangeExtension(executablePath, ".exe");
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };
        process.Start();
        process.WaitForExit();
        return process.StandardOutput.ReadToEnd();
    }
}
