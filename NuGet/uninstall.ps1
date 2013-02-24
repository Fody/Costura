param($installPath, $toolsPath, $package, $project)


function Update-FodyConfig($addinName, $project)
{
    $fodyWeaversPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), "FodyWeavers.xml")

    if (!(Test-Path ($fodyWeaversPath)))
    {
        return
    }   

    $xml = [xml](get-content $fodyWeaversPath)

    $weavers = $xml["Weavers"]
    $node = $weavers.SelectSingleNode($addinName)

    if ($node)
    {
        $weavers.RemoveChild($node)
    }

    $xml.Save($fodyWeaversPath)
}

function Uninstall-Target($targetName, $project)
{
    # Need to load MSBuild assembly if it's not loaded yet.
    Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

    # Grab the loaded MSBuild project for the project
    $msbuild = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1
    $importToRemove = $msbuild.Xml.Imports | Where-Object { $_.Project.Endswith($targetName + '.targets') }

    # Remove the import and save the project
    $msbuild.Xml.RemoveChild($importToRemove) | out-null
    $project.Save()
}



Update-FodyConfig "Costura" $project

Uninstall-Target "Costura" $project