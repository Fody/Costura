// Note: code originally comes from https://stackoverflow.com/questions/50826394/how-to-print-tool-command-line-in-cake

/// <summary>
/// Temporary sets logging verbosity.
/// </summary>
/// <example>
/// <code>
/// // Temporary sets logging verbosity to Diagnostic.
/// using(context.UseVerbosity(Verbosity.Diagnostic))
/// {
///     context.DotNetBuild(project, settings);
/// }
/// </code>
/// </example>
public static VerbosityChanger UseVerbosity(this ICakeContext context, Verbosity newVerbosity) =>
     new VerbosityChanger(context.Log, newVerbosity);


/// <summary>
/// Temporary sets logging verbosity to Diagnostic.
/// </summary>
/// <example>
/// <code>
/// // Temporary sets logging verbosity to Diagnostic.
/// using(context.UseDiagnosticVerbosity())
/// {
///     context.DotNetBuild(project, settings);
/// }
/// </code>
/// </example>
public static VerbosityChanger UseDiagnosticVerbosity(this ICakeContext context) =>
    context.UseVerbosity(Verbosity.Diagnostic);

/// <summary>
/// Cake log verbosity changer.
/// Restores old verbosity on Dispose.
/// </summary>
public class VerbosityChanger : IDisposable
{
    ICakeLog _log;
    Verbosity _oldVerbosity;

    public VerbosityChanger(ICakeLog log, Verbosity newVerbosity)
    {
        _log = log;
        _oldVerbosity = log.Verbosity;
        _log.Verbosity = newVerbosity;
    }

    public void Dispose() => _log.Verbosity = _oldVerbosity;
}