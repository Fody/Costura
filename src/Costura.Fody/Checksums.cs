using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public partial class ModuleWeaver
{
    private Dictionary<string, string> _checksums = new Dictionary<string, string>();

    private static string CalculateChecksum(string filename)
    {
        using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            return CalculateChecksum(fs);
        }
    }

    private static string CalculateChecksum(Stream stream)
    {
        using (var bs = new BufferedStream(stream))
        using (var sha1 = new SHA1CryptoServiceProvider())
        {
            var hash = sha1.ComputeHash(bs);
            var formatted = new StringBuilder(2 * hash.Length);
            foreach (var b in hash)
            {
                formatted.AppendFormat("{0:X2}", b);
            }

            return formatted.ToString();
        }
    }

    private void AddChecksumsToTemplate()
    {
        if (_checksumsField == null)
        {
            return;
        }

        foreach (var checksum in _checksums)
        {
            AddToDictionary(_checksumsField, checksum.Key, checksum.Value);
        }
    }
}
