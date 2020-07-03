using System;
using System.Collections.Generic;
using System.Text;

public class EmbeddedReferenceInfo
{
    public string ResourceName { get; set; }

    public string Version { get; set; }

    public string AssemblyName { get; set; }

    public string RelativeFileName { get; set; }

    public string Checksum { get; set; }

    public override string ToString()
    {
        return $"{ResourceName}|{Version}|{AssemblyName}|{RelativeFileName}|{Checksum}";
    }
}
