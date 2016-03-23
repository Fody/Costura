using System;
using System.IO;
using System.Linq;

public static class TestPaths
{
	public static string GetProcessingDirectory(string projectName)
	{
		var directory = Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
		var directoryParts = directory.Split(Path.DirectorySeparatorChar);
		var suffix = string.Join(Path.DirectorySeparatorChar.ToString(), directoryParts.Reverse().Take(2).Reverse().ToArray());

		return Path.GetFullPath(Path.Combine(directory, "..", "..", "..", projectName, suffix));
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

	public static bool IsMono()
	{
		return Type.GetType("Mono.Runtime") != null;
	}
}