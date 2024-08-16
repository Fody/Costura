using System;

internal class Program
{
    private static void Main()
    {
        // When the AppContext switches are properly initialized with the executable targeting ".NETFramework,Version=v4.7.2",
        // the "Switch.System.Diagnostics.IgnorePortablePDBsInStackTraces" is _not_ set at all.
        // "Switch.System.Diagnostics.IgnorePortablePDBsInStackTraces" is set to true if the AppContext switches are initialized with a wrong target framework name
        // See https://github.com/Fody/Costura/issues/633 for more information
        var appContextDefaultSwitchIsCorrect = AppContext.TryGetSwitch("Switch.System.Diagnostics.IgnorePortablePDBsInStackTraces", out _) == false;
        const string errorMessage = "Switch.System.Diagnostics.IgnorePortablePDBsInStackTraces should not be set for an executable targeting .NET Framework 4.7.2";
        Console.Out.Write(appContextDefaultSwitchIsCorrect ? "Run-OK" : errorMessage);
    }
}
