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



$project.ProjectItems.Item("Fody_ToBeDeleted.txt").Delete()

Update-FodyConfig "Costura" $project