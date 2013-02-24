param($installPath, $toolsPath, $package, $project)


function Update-FodyConfig($addinName, $project)
{
    $fodyWeaversPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), "FodyWeavers.xml")

    if (!(Test-Path ($fodyWeaversPath)))
    {
        Throw "Could not find FodyWeavers.xml in this project. Please enable Fody for this projet http://visualstudiogallery.msdn.microsoft.com/074a2a26-d034-46f1-8fe1-0da97265eb7a"
    }   

    $xml = [xml](get-content $fodyWeaversPath)

    $weavers = $xml["Weavers"]
    $node = $weavers.SelectSingleNode($addinName)

    if (-not $node)
    {
        $newNode = $xml.CreateElement($addinName)
        $weavers.AppendChild($newNode)
    }

    $xml.Save($fodyWeaversPath)
}

function Install-Target($targetName, $toolsPath, $project)
{
    # This is the MSBuild targets file to add
    $targetsFile = [System.IO.Path]::Combine($toolsPath, $targetName + '.targets')
 
    # Need to load MSBuild assembly if it's not loaded yet.
    Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

    # Grab the loaded MSBuild project for the project
    $msbuild = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1
 
    # Make the path to the targets file relative.
    $projectUri = new-object Uri('file://' + $project.FullName)
    $targetUri = new-object Uri('file://' + $targetsFile)
    $relativePath = $projectUri.MakeRelativeUri($targetUri).ToString().Replace([System.IO.Path]::AltDirectorySeparatorChar, [System.IO.Path]::DirectorySeparatorChar)
 
    # Add the import and save the project
    $msbuild.Xml.AddImport($relativePath) | out-null
    $project.Save()
}

$project.ProjectItems.Item("Fody_ToBeDeleted.txt").Delete()

Update-FodyConfig "Costura" $project

Install-Target "Costura" $toolsPath $project