param($installPath, $toolsPath, $package, $project)

$addinName = "Costura"

$fodyWeaversPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), "FodyWeavers.xml")

if (!(Test-Path ($fodyWeaversPath)))
{
	exit
}	

$xml = [xml](get-content $fodyWeaversPath)

$weavers = $xml["Weavers"]
$node = $xml.Weavers[$addinName]

if ($node -ne $null)
{
    $weavers.RemoveChild($node)
}

$xml.Save($fodyWeaversPath)

