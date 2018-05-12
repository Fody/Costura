using System;
using System.Collections.Generic;
using System.IO;
public class ConfigFileFinder
{
    public static List<string> FindWeaverConfigs(string solutionDirectoryPath, string projectDirectory)
    {
        var files = new List<string>();

        var solutionConfigFilePath = Path.Combine(solutionDirectoryPath, "FodyWeavers.xml");
        if (File.Exists(solutionConfigFilePath))
        {
            files.Add(solutionConfigFilePath);
        }

        var projectConfigFilePath = Path.Combine(projectDirectory, "FodyWeavers.xml");
        if (File.Exists(projectConfigFilePath))
        {
            files.Add(projectConfigFilePath);
        }

        if (files.Count == 0)
        {
            // ReSharper disable once UnusedVariable
            var pathsSearched = string.Join("', '", solutionConfigFilePath, projectConfigFilePath);
            throw new WeavingException($@"Could not find path to weavers file. Searched '{pathsSearched}'. Some project types do not support using NuGet to add content files e.g. netstandard projects. In these cases it is necessary to manually add a FodyWeavers.xml to the project. Example content:
  <Weavers>
    <WeaverName/>
  </Weavers>
  ");
        }
        return files;
    }
}