function Resolve-ProjectName {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    
    if($ProjectName) {
        $projects = Get-Project $ProjectName
    }
    else {
        # All projects by default
        $projects = Get-Project
    }
    
    $projects
}

function Get-MSBuildProject {
    param(
        [parameter(ValueFromPipelineByPropertyName = $true)]
        [string[]]$ProjectName
    )
    Process {
        (Resolve-ProjectName $ProjectName) | % {
            $path = $_.FullName
            @([Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($path))[0]
        }
    }
}

function Install-CleanReferencesTarget()
{
    $buildProject = Get-MSBuildProject

    if ($buildProject.Xml.Targets | Where-Object { "CleanReferenceCopyLocalPaths" -contains $_.Name })
    {
        Write-Host "Target CleanReferenceCopyLocalPaths already exists." -foregroundcolor Black -backgroundcolor Yellow

        return
    }

    $usingTask = $buildProject.Xml.AddUsingTask("CosturaCleanup", "`$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll", "")
	$usingTask.TaskFactory = "CodeTaskFactory"
    $parameterGroup = $usingTask.AddParameterGroup()
    $configParam = $parameterGroup.AddParameter("Config", "false", "true", "Microsoft.Build.Framework.ITaskItem")
    $filesParam = $parameterGroup.AddParameter("Files", "false", "true", "Microsoft.Build.Framework.ITaskItem[]")
	$taskBody = $usingTask.AddUsingTaskBody("true", "<Reference xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Include=`"System.Xml`"/>
      <Reference xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Include=`"System.Xml.Linq`"/>
      <Using xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Namespace=`"System`"/>
      <Using xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Namespace=`"System.IO`"/>
      <Using xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Namespace=`"System.Xml.Linq`"/>
      <Code xmlns=`"http://schemas.microsoft.com/developer/msbuild/2003`" Type=`"Fragment`" Language=`"cs`">
<![CDATA[
var config = XElement.Load(Config.ItemSpec).Elements(`"Costura`").FirstOrDefault();

if (config == null) return true;

var excludedAssemblies = new List<string>();
var attribute = config.Attribute(`"ExcludeAssemblies`");
if (attribute != null)
    foreach (var item in attribute.Value.Split('|').Select(x => x.Trim()).Where(x => x != string.Empty))
        excludedAssemblies.Add(item);
var element = config.Element(`"ExcludeAssemblies`");
if (element != null)
    foreach (var item in element.Value.Split(new[] { `"\r\n`", `"\n`" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x != string.Empty))
        excludedAssemblies.Add(item);

var filesToCleanup = Files.Select(f => f.ItemSpec).Where(f => !excludedAssemblies.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.InvariantCultureIgnoreCase));

foreach (var item in filesToCleanup)
  File.Delete(item);
]]>
      </Code>")

    $target = $buildProject.Xml.AddTarget("CleanReferenceCopyLocalPaths")
    $target.AfterTargets = "AfterBuild;NonWinFodyTarget"
    $deleteTask = $target.AddTask("CosturaCleanup")
    $deleteTask.SetParameter("Config", "FodyWeavers.xml")
    $deleteTask.SetParameter("Files", "@(ReferenceCopyLocalPaths->`'`$(OutDir)%(DestinationSubDirectory)%(Filename)%(Extension)`')")

    $buildProject.Save()

    Write-Host "Added target CleanReferenceCopyLocalPaths."
}

function Uninstall-CleanReferencesTarget()
{
    $buildProject = Get-MSBuildProject

    $target = $buildProject.Xml.Targets | Where-Object { "CleanReferenceCopyLocalPaths" -contains $_.Name }
    $usingTask = $buildProject.Xml.UsingTasks | Where-Object { "CosturaCleanup" -contains $_.TaskName }

    if (!$target)
    {
        Write-Host "Target CleanReferenceCopyLocalPaths did not exist." -foregroundcolor Black -backgroundcolor Yellow

        return
    }

    $buildProject.Xml.RemoveChild($usingTask)
    $buildProject.Xml.RemoveChild($target)

    $buildProject.Save()

    Write-Host "Removed target CleanReferenceCopyLocalPaths."
}