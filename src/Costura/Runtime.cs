using System;
using System.IO;

public static class Runtime
{
	public static bool IsMono()
	{
		return Type.GetType("Mono.Runtime") != null;
	}

	public static string GetSymbolFileName(string fileName)
	{
		if (IsMono())
		{
			return fileName + ".mdb";
		}
		else 
		{
			return Path.ChangeExtension(fileName, ".pdb");
		}
	}
}