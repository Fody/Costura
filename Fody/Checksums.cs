using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

partial class ModuleWeaver
{
    readonly Dictionary<string, string> checksums = new Dictionary<string, string>();

    static string CalculateChecksum(string filename)
    {
        using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            return CalculateChecksum(fs);
        }
    }

    static string CalculateChecksum(Stream stream)
    {
        using (BufferedStream bs = new BufferedStream(stream))
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(bs);
                StringBuilder formatted = new StringBuilder(2 * hash.Length);
                foreach (byte b in hash)
                {
                    formatted.AppendFormat("{0:X2}", b);
                }
                return formatted.ToString();
            }
        }
    }

    void AddChecksumsToTemplate()
    {
        if (checksumsField == null)
            return;

        foreach (var checksum in checksums)
            AddToDictionary(checksumsField, checksum.Key, checksum.Value);
    }
}