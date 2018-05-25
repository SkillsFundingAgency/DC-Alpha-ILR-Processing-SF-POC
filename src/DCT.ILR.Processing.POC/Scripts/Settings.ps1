param (
    [string]$ProjDirectory = ""
)

$privateFilesPath =  [io.path]::combine($(get-item $ProjDirectory).parent.parent.FullName,"DC-PrivateConfigs","DCT","ServiceFabricSettings",$env:UserName,"*.xml")
Write-Host $privateFilesPath 
 
$destination = [io.path]::combine($(get-item $ProjDirectory ).FullName,"ApplicationParameters")
Write-Host $destination 
if (Test-Path $privateFilesPath)
{ 
    Write-Host "Found private repo files, copying..." 
    Copy-Item $privateFilesPath $destination -recurse -force
}
else
{
    $sourceCloudFilePath = [io.path]::combine($destination,"Cloud.xml") 
    $node1File =  [io.path]::combine($destination,"Local.1Node.xml") 
    $node5File =  [io.path]::combine($destination,"Local.5Node.xml") 

    Write-Host $sourceCloudFilePath 
    Write-Host "Copying template files" 
   
    Copy-Item  $sourceCloudFilePath  -Destination $node1File -Force
    Copy-Item  $sourceCloudFilePath  -Destination $node5File -Force
}