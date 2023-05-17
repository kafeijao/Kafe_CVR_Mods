# CVR and Melon Loader Dependencies
$0HarmonydllPath    = "\MelonLoader\0Harmony.dll"
$melonLoaderdllPath = "\MelonLoader\MelonLoader.dll"
$cvrManagedDataPath = "\ChilloutVR_Data\Managed"

$cvrPath = $env:CVRPATH
$cvrExecutable = "ChilloutVR.exe"
$cvrDefaultPath = "C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR"

# Array with the dlls to strip
$dllsToStrip = @('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','AVProVideo.Runtime.dll', 'Unity.TextMeshPro.dll', 'MagicaCloth.dll')

# Array with the mods to grab
$modNames = @("BTKUILib", "BTKSAImmersiveHud", "ActionMenu", "MenuScalePatch")

if ($cvrPath -and (Test-Path "$cvrPath\$cvrExecutable")) {
    # Found ChilloutVR.exe in the existing CVRPATH
    Write-Host ""
    Write-Host "Found the ChilloutVR folder on: $cvrPath"
}
else {
    # Check if ChilloutVR.exe exists in default Steam location
    if (Test-Path "$cvrDefaultPath\$cvrExecutable") {
        # Set CVRPATH environment variable to default Steam location
        Write-Host "Found the ChilloutVR on the default steam location, setting the CVRPATH Env Var at User Level!"
        [Environment]::SetEnvironmentVariable("CVRPATH", $cvrDefaultPath, "User")
        $env:CVRPATH = $cvrDefaultPath
        $cvrPath = $env:CVRPATH
    }
    else {
        Write-Host "[ERROR] ChilloutVR.exe not found in CVRPATH or the default Steam location."
        Write-Host "        Please define the Environment Variable CVRPATH pointing to the ChilloutVR folder!"
        return
    }
}

$scriptDir = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$managedLibsFolder = $scriptDir + "\.ManagedLibs"

if (!(Test-Path $managedLibsFolder)) {
    New-Item -ItemType Directory -Path $managedLibsFolder
    Write-Host ".ManagedLibs folder created successfully."
}

Write-Host ""
Write-Host "Copying the DLLs from the CVR, MelonLoader, and Mods folder to the .ManagedLibs"


Copy-Item $cvrPath$0HarmonydllPath -Destination $managedLibsFolder
Copy-Item $cvrPath$melonLoaderdllPath -Destination $managedLibsFolder
Copy-Item $cvrPath$cvrManagedDataPath"\*" -Destination $managedLibsFolder


# Saving XML ready libs for the Build.props file
$lib_names_xml = "<Project><ItemGroup>"
$lib_names_xml += '<Reference Include="0Harmony"><HintPath>$(MsBuildThisFileDirectory)\.ManagedLibs\0Harmony.dll</HintPath><Private>False</Private></Reference>'
$lib_names_xml += '<Reference Include="MelonLoader"><HintPath>$(MsBuildThisFileDirectory)\.ManagedLibs\MelonLoader.dll</HintPath><Private>False</Private></Reference>'
foreach ($file in Get-ChildItem $cvrPath$cvrManagedDataPath"\*") {
    $lib_names_xml += "<Reference Include=`"$($file.BaseName)`"><HintPath>`$(MsBuildThisFileDirectory)\.ManagedLibs\$($file.BaseName).dll</HintPath><Private>False</Private></Reference>"
}
$lib_names_xml += "</ItemGroup></Project>"
$lib_names_xml | Out-File -Encoding UTF8 -FilePath lib_names.xml


# Third Party Dependencies
$melonModsPath="\Mods\"
$missingMods = New-Object System.Collections.Generic.List[string]


foreach ($modName in $modNames) {
    $modDll = $modName + ".dll"
    $modPath = $cvrPath + $melonModsPath + $modDll
    $managedLibsModPath = "$managedLibsFolder\$modDll"

    # Attempt to grab from the mods folder
    if (Test-Path $modPath -PathType Leaf) {
        Write-Host "    Copying $modDll from $melonModsPath to \.ManagedLibs!"
        Copy-Item $modPath -Destination $managedLibsFolder
    }
    # Check if they already exist in the .ManagedLibs
    elseif (Test-Path $managedLibsModPath -PathType Leaf) {
        Write-Host "    Ignoring $modDll since already exists in \.ManagedLibs!"
    }
    # If we fail, lets add to the missing mods list
    else {
        $missingMods.Add($modName)
    }
}

if ($missingMods.Count -gt 0) {
    # If we have missing mods, let's fetch them from the latest CVR Modding Group API
    Write-Host ""
    Write-Host "Failed to find $($missingMods.Count) mods. We're going to search in CVR MG verified mods" -ForegroundColor Red
    Write-Host "You can download them and move to $managedLibsFolder"

    $cvrModdingApiUrl = "https://api.cvrmg.com/v1/mods"
    $cvrModdingDownloadUrl = "https://api.cvrmg.com/v1/mods/download/"
    $latestModsResponse = Invoke-RestMethod $cvrModdingApiUrl -UseBasicParsing

    foreach ($modName in $missingMods) {
        $mod = $latestModsResponse | Where-Object { $_.name -eq $modName }
        if ($mod) {
            $modDownloadUrl = $cvrModdingDownloadUrl + $($mod._id)
            # It seems power shell doesn't like to download .dll from https://api.cvrmg.com (messes with some anti-virus)
            # Invoke-WebRequest -Uri $modDownloadUrl -OutFile $managedLibsFolder -UseBasicParsing
            # Write-Host "  $modName was downloaded successfully to $managedLibsFolder!"
            Write-Host "    $modName Download Url: $modDownloadUrl"
        } else {
            Write-Host "  $modName was not found in the CVR Modding Group verified mods!"
        }
    }
}


Write-Host ""
Write-Host "Copied all libraries!"
Write-Host ""
Write-Host "Press any key to strip the Dlls using NStrip"
$HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
$HOST.UI.RawUI.Flushinputbuffer()

Write-Host "NStrip Convert all private/protected stuff to public. Requires <AllowUnsafeBlocks>true></AllowUnsafeBlocks>"

# Check if NStrip.exe exists in the current directory
if(Test-Path -Path ".\NStrip.exe") {
    $nStripPath = ".\NStrip.exe"
}
else {
    # Try to locate NStrip.exe in the PATH
    $nStripPath = Get-Command -Name NStrip.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if($nStripPath -eq $null) {
        # Display an error message if NStrip.exe could not be found
        Write-Host "Could not find NStrip.exe in the current directory nor in the PATH." -ForegroundColor Red
        Write-Host "Visit https://github.com/bbepis/NStrip/releases/latest to grab a copy." -ForegroundColor Red
        return
    }
}

# Loop through each DLL file to strip and call NStrip.exe
foreach($dllFile in $dllsToStrip) {
    $dllPath = Join-Path -Path $managedLibsFolder -ChildPath $dllFile
    & $nStripPath -p -n $dllPath $dllPath
}

Write-Host ""
Write-Host "Copied all libraries and stripped the DLLs!"
Write-Host ""
Write-Host "Press any key to exit"
$HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
$HOST.UI.RawUI.Flushinputbuffer()
