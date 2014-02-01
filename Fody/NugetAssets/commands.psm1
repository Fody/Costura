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

	$target = $buildProject.Xml.AddTarget("CleanReferenceCopyLocalPaths")

	$target.AfterTargets = "AfterBuild;NonWinFodyTarget"

	$deleteTask = $target.AddTask("Delete")

	$deleteTask.SetParameter("Files", "@(ReferenceCopyLocalPaths->`'`$(OutDir)%(DestinationSubDirectory)%(Filename)%(Extension)`')")

	$buildProject.Save()

	Write-Host "Added target CleanReferenceCopyLocalPaths."
}

function Uninstall-CleanReferencesTarget()
{
	$buildProject = Get-MSBuildProject

	$target = $buildProject.Xml.Targets | Where-Object { "CleanReferenceCopyLocalPaths" -contains $_.Name }

	if (!$target)
	{
		Write-Host "Target CleanReferenceCopyLocalPaths did not exist." -foregroundcolor Black -backgroundcolor Yellow

		return
	}

	$buildProject.Xml.RemoveChild($target)

	Write-Host "Removed target CleanReferenceCopyLocalPaths."
}