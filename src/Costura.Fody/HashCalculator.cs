using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

public partial class ModuleWeaver
{
    private string _resourcesHash;

    private void CalculateHash()
    {
        var data = ModuleDefinition.Resources.OfType<EmbeddedResource>()
            .OrderBy(r => r.Name)
            .Where(r => r.Name.StartsWith("costura"))
            .SelectMany(r => r.FixedGetResourceData())
            .ToArray();

        using (var md5 = MD5.Create())
        {
            var hashBytes = md5.ComputeHash(data);

            var sb = new StringBuilder();
            for (var i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }

            _resourcesHash = sb.ToString();
        }
    }
}
