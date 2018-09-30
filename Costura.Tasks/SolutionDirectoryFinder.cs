using System.IO;

static class SolutionDirectoryFinder
{
    public static string Find(string solutionDir, string projectDirectory)
    {
        if (solutionDir == null)
        {
            return Directory.GetParent(projectDirectory).FullName;
        }
        return solutionDir;
    }
}