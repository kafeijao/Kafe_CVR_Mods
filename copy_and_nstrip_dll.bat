@echo off
setlocal enableDelayedExpansion

echo Copy all dll from the game so we can work and modify them
xcopy /f /y "%CVRPATH%\MelonLoader\0Harmony.dll" "%0\..\ManagedLibs"
xcopy /f /y "%CVRPATH%\MelonLoader\MelonLoader.dll" "%0\..\ManagedLibs"
xcopy /f /s /y "%CVRPATH%\ChilloutVR_Data\Managed" "%0\..\ManagedLibs"

@REM echo Generating file with all lib names
@REM echo 0Harmony>lib_names
@REM echo MelonLoader>>lib_names
@REM for %%f in ("%CVRPATH%\ChilloutVR_Data\Managed\*") do echo %%~nf>>lib_names

echo Nstrip convert all private/protected stuff to public, yay
for %%x in (Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.CoreModule.dll Cohtml.Runtime.dll) do (
    NStrip.exe -p -n "%CVRPATH%\ChilloutVR_Data\Managed\%%x" "%0\..\ManagedLibs\%%x"
)
echo We re done now
pause
