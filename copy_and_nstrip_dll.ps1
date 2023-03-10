$0HarmonydllPath="\MelonLoader\0Harmony.dll"
$melonLoaderdllPath="\MelonLoader\MelonLoader.dll"
$cvrManagedDataPath="\ChilloutVR_Data\Managed"

# Third Party Dependencies
$btkuiLibPath="\Mods\BTKUILib.dll"

$cvrPath=$env:CVRPATH

$scriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
Write-Host "Copy all dll from the game so we can work and modify them"


Copy-Item $cvrPath$0HarmonydllPath -Destination $scriptDir"\ManagedLibs"
Copy-Item $cvrPath$melonLoaderdllPath -Destination $scriptDir"\ManagedLibs"
Copy-Item $cvrPath$cvrManagedDataPath"\*" -Destination $scriptDir"\ManagedLibs"

if (Test-Path "$cvrPath$btkuiLibPath" -PathType Leaf) {
    Copy-Item $cvrPath$btkuiLibPath -Destination $scriptDir"\ManagedLibs"
}

Write-Host "Copied all libraries!"
Write-Host ""
Write-Host "Press any key to strip the Dlls using NStrip"
$HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
$HOST.UI.RawUI.Flushinputbuffer()

Write-Host "Nstrip convert all private/protected stuff to public, yay"

$dllsToStrip=@('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.CoreModule.dll','Cohtml.Runtime.dll')

foreach($dllFile in $dllsToStrip)
{
 Write-Host "stripping dll : "$dllFile
 .\NStrip.exe -p -n $cvrPath$cvrManagedDataPath"\"$dllFile $scriptDir"\ManagedLibs\"$dllFile
}

Write-Host "Process Completed"
Write-Host ""
Write-Host "Press any key to exit"
$HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
$HOST.UI.RawUI.Flushinputbuffer()
