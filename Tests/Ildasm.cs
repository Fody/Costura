using System;
using System.Linq;
using System.Text.RegularExpressions;

class Ildasm
{
    public static string Decompile(string afterAssemblyPath, string costuraAssemblyLoader)
    {
        var decompile = Fody.Ildasm.Decompile(afterAssemblyPath, costuraAssemblyLoader);
        var checksumPattern = new Regex(@"^(\s*IL_[0123456789abcdef]{4}:  ldstr\s*"")[0123456789ABCDEF]{32,40}""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return string.Join(Environment.NewLine, decompile.Split(new[]{ Environment.NewLine }, StringSplitOptions.None)
            .Where(l => !l.StartsWith("// ", StringComparison.CurrentCulture) && !string.IsNullOrEmpty(l))
            .Select(l => checksumPattern.Replace(l, e => e.Groups[1].Value + "[CHECKSUM]\""))
            .ToList());
    }
}