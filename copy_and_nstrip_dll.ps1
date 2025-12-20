param(
    # Whether it should ask user for inputs to proceed or in should run the whole script without prompting
    [switch]$silent = $false
)

# CVR and Melon Loader Dependencies
$0HarmonydllPath    = "\MelonLoader\net35\0Harmony.dll"
$melonLoaderdllPath = "\MelonLoader\net35\MelonLoader.dll"
$CecilallPath       = "\MelonLoader\net35\Mono.Cecil.dll"
$cvrManagedDataPath = "\ChilloutVR_Data\Managed"

$cvrPath = $env:CVRPATH
$cvrExecutable = "ChilloutVR.exe"
$cvrDefaultPath = "C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR"
# $cvrDefaultPath = "E:\temp\CVR_Experimental"

# Array with the dlls to strip
$dllsToStrip = @('Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','AVProVideo.Runtime.dll', 'Unity.TextMeshPro.dll', 'MagicaCloth.dll', 'MagicaClothV2.dll')

# Array with the mods to grab
$modNames = @("PortableMirrorMod", "VRBinding")

# Array with dlls to ignore from ManagedLibs
$cvrManagedLibNamesToIgnore = @(
    "netstandard", # Breaks targeting netstandard on the mod
    "Mono.Cecil",
    "Unity.Burst.Cecil",
    "Microsoft.Win32.Registry",
    "System.Runtime.CompilerServices.Unsafe", # Gives a warning that it fails to resolve conflicts
    "System.Runtime.CompilerServices.Unsafe.OSCQuery" # Gives a warning that it fails to resolve conflicts
)

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
Copy-Item $cvrPath$CecilallPath -Destination $managedLibsFolder
Copy-Item $cvrPath$cvrManagedDataPath"\*" -Destination $managedLibsFolder


# Generate the References.Items.props file, that contains the references to all ManagedData Dlls
# Define indentation as a variable
$indent = '    '  # 4 spaces

$lib_names_xml = "<Project>`n${indent}<ItemGroup>`n"

# Manually add references with specific paths and settings
$lib_names_xml += "${indent}${indent}<Reference Include=`"0Harmony`">`n${indent}${indent}${indent}<HintPath>`$(MsBuildThisFileDirectory)\.ManagedLibs\0Harmony.dll</HintPath>`n${indent}${indent}${indent}<Private>False</Private>`n${indent}${indent}</Reference>`n"
$lib_names_xml += "${indent}${indent}<Reference Include=`"MelonLoader`">`n${indent}${indent}${indent}<HintPath>`$(MsBuildThisFileDirectory)\.ManagedLibs\MelonLoader.dll</HintPath>`n${indent}${indent}${indent}<Private>False</Private>`n${indent}${indent}</Reference>`n"
$lib_names_xml += "${indent}${indent}<Reference Include=`"Mono.Cecil`">`n${indent}${indent}${indent}<HintPath>`$(MsBuildThisFileDirectory)\.ManagedLibs\Mono.Cecil.dll</HintPath>`n${indent}${indent}${indent}<Private>False</Private>`n${indent}${indent}</Reference>`n"

# Iterate over files in a specified directory, adding them as references if not in the ignore list
foreach ($file in Get-ChildItem $cvrPath$cvrManagedDataPath"\*") {
    if ($cvrManagedLibNamesToIgnore -notcontains $file.BaseName) {
        $lib_names_xml += "${indent}${indent}<Reference Include=`"$($file.BaseName)`">`n${indent}${indent}${indent}<HintPath>`$(MsBuildThisFileDirectory)\.ManagedLibs\$($file.BaseName).dll</HintPath>`n${indent}${indent}${indent}<Private>False</Private>`n${indent}${indent}</Reference>`n"
    }
}

# Close the ItemGroup and Project tags with proper formatting
$lib_names_xml += "${indent}</ItemGroup>`n</Project>"

# Output the constructed XML content to a file with UTF8 encoding
$lib_names_xml | Out-File -Encoding UTF8 -FilePath "References.Items.props"

Write-Host ""
Write-Host "Generated References.Items.props file containing the references to all common ManagedLibs"



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

if (-not $silent) {
    Write-Host "Press any key to strip the Dlls using NStrip"
    $HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
    $HOST.UI.RawUI.Flushinputbuffer()
}

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

if (-not $silent) {
    Write-Host "Press any key to exit"
    $HOST.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | OUT-NULL
    $HOST.UI.RawUI.Flushinputbuffer()
}
