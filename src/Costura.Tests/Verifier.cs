﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using NUnit.Framework;

public static class Verifier
{
    public static void Verify(string beforeAssemblyPath, string afterAssemblyPath)
    {
        var before = Validate(beforeAssemblyPath);
        var after = Validate(afterAssemblyPath);
        var message = string.Format("Failed processing {0}\r\n{1}", Path.GetFileName(afterAssemblyPath), after);
        Assert.AreEqual(TrimLineNumbers(before), TrimLineNumbers(after), message);
    }

    public static string Validate(string assemblyPath2)
    {
        var exePath = GetPathToPEVerify();
        using (var process = Process.Start(new ProcessStartInfo(exePath, String.Format("\"{0}\"", assemblyPath2))
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            process.WaitForExit(10000);
            return process.StandardOutput.ReadToEnd().Trim().Replace(assemblyPath2, "");
        }
    }

    static string GetPathToPEVerify()
    {
		if (TestPaths.IsMono ()) 
		{
			return "/usr/bin/peverify";
		}
		else 
		{
			var path = Path.Combine (ToolLocationHelper.GetPathToDotNetFrameworkSdk (TargetDotNetFrameworkVersion.Version40), @"bin\NETFX 4.0 Tools\peverify.exe");
			if (!File.Exists (path))
				path = path.Replace ("v7.0", "v8.0");
			if (!File.Exists (path))
				Assert.Ignore ("PEVerify could not be found");
			return path;
		}
    }

    static string TrimLineNumbers(string foo)
    {
        return Regex.Replace(foo, @"0x.*]", "");
    }
}