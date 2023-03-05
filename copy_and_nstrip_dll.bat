@echo off
setlocal enableDelayedExpansion

echo Copy all dll from the game so we can work and modify them

xcopy /f /y "%CVRPATH%\MelonLoader\0Harmony.dll" "%~dp0ManagedLibs\"
xcopy /f /y "%CVRPATH%\MelonLoader\MelonLoader.dll" "%~dp0ManagedLibs\"
xcopy /f /s /y "%CVRPATH%\ChilloutVR_Data\Managed" "%~dp0ManagedLibs\"


echo Saving XML ready libs for the Build.props file
echo ^<Project^>^<ItemGroup^> > lib_names.xml
echo ^<Reference Include="0Harmony"^>^<HintPath^>$(MsBuildThisFileDirectory)\ManagedLibs\0Harmony.dll^</HintPath^>^<Private^>False^</Private^>^</Reference^> >> lib_names.xml
echo ^<Reference Include="MelonLoader"^>^<HintPath^>$(MsBuildThisFileDirectory)\ManagedLibs\MelonLoader.dll^</HintPath^>^<Private^>False^</Private^>^</Reference^> >> lib_names.xml
for %%f in ("%CVRPATH%\ChilloutVR_Data\Managed\*") do echo ^<Reference Include="%%~nf"^>^<HintPath^>$(MsBuildThisFileDirectory)\ManagedLibs\%%~nf.dll^</HintPath^>^<Private^>False^</Private^>^</Reference^> >> lib_names.xml
echo ^</ItemGroup^>^</Project^> >> lib_names.xml


echo Press Enter to NStrip the files
pause

echo Nstrip convert all private/protected stuff to public, yay
for %%x in (Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.CoreModule.dll Cohtml.Runtime.dll) do (
    NStrip.exe -p -n "%CVRPATH%\ChilloutVR_Data\Managed\%%x" "%~dp0ManagedLibs\%%x"
)
echo We re done now
pause
