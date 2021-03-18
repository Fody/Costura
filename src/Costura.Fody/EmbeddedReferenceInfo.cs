public class EmbeddedReferenceInfo
{
    public string ResourceName { get; set; }

    public string Version { get; set; }

    public string AssemblyName { get; set; }

    public string RelativeFileName { get; set; }

    public string Sha1Checksum { get; set; }

    public long Size { get; set; }

    public override string ToString()
    {
        return $"{ResourceName}|{Version}|{AssemblyName}|{RelativeFileName}|{Sha1Checksum}|{Size}";
    }
}
