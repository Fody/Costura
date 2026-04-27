using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using VerifyNUnit;
using VerifyTests;

public static class VerifyHelper
{
    static VerifyHelper()
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task AssertIlCodeAsync(string assemblyFileName, [CallerMemberName] string callerMemberName = "")
    {
        var actualIl = Ildasm.Decompile(assemblyFileName, "Costura.AssemblyLoader");

        var settings = new VerifySettings
        {

        };

        settings.UniqueForAssemblyConfiguration();
        settings.UniqueForTargetFrameworkAndVersion();

        // Replace versions so it never breaks on updates
        foreach (var assembly in new[]
        {
                typeof(VerifyHelper).Assembly, // test assembly version
                typeof(CriticalHandleMinusOneIsInvalid).Assembly, // System.Private.CoreLib
            })
        {
            var search = $"Version={assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version}";

            settings.ScrubLinesWithReplace(replaceLine: _ =>
            {
                return _.Replace(search, "Version=Version");
            });
        }

        await Verifier.Verify(actualIl, settings);
    }
}
