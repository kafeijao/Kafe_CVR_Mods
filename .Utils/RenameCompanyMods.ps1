# Simple powershell script that renames the melon attribute Company into empty string. Which makes mods to be attempted
# to load regardless the company name

######## CONFIG ########################################################################################
# Change this path to match your melon loader installation (we're using their bundled Mono.Cecil.dll)
$cecilPath = "C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\MelonLoader\net35\Mono.Cecil.dll"
# Change this path to a folder where your mods are located, it will then output the fixed mods into a
# folder named ~RenamedCompanyMods, inside of the picked folder
$sourceDir = "C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR\Mods"
######## CONFIG ########################################################################################


# Registers the Cecil path for the current terminal session
Add-Type -Path $cecilPath

# Create and use this folder
$outputDir = Join-Path $sourceDir "~RenamedCompanyMods"

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$dllFiles = Get-ChildItem -Path $sourceDir -Filter *.dll

foreach ($dll in $dllFiles) {
    try {
        Write-Host "Processing: $($dll.Name)"

        # Load assembly
        $readerParams = New-Object Mono.Cecil.ReaderParameters
        $readerParams.ReadWrite = $false
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll.FullName, $readerParams)
        $module = $assembly.MainModule

        # Find MelonGameAttribute
        $attr = $module.Assembly.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq "MelonLoader.MelonGameAttribute"
        }

        if ($attr) {
            # Modify constructor arguments
            $stringType = $module.TypeSystem.String
            $attr.ConstructorArguments[0] = New-Object Mono.Cecil.CustomAttributeArgument($stringType, "")
            $attr.ConstructorArguments[1] = New-Object Mono.Cecil.CustomAttributeArgument($stringType, "ChilloutVR")

            # Save to new location
            $outputPath = Join-Path $outputDir $dll.Name
            $assembly.Write($outputPath)
            Write-Host " -> Modified and saved to: $outputPath"
        }
        else {
            Write-Host " -> No MelonGame attribute found in: $($dll.Name)"
        }

    } catch {
        Write-Warning "Failed to process $($dll.FullName): $_"
    }
}
