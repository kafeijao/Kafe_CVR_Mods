@echo off
setlocal enableDelayedExpansion

echo Copy all dll from the game so we can work and modify them

xcopy /f /y "%CVRPATH%\MelonLoader\0Harmony.dll" "%~dp0ManagedLibs\"
xcopy /f /y "%CVRPATH%\MelonLoader\MelonLoader.dll" "%~dp0ManagedLibs\"
xcopy /f /s /y "%CVRPATH%\ChilloutVR_Data\Managed" "%~dp0ManagedLibs\"

if exist "%CVRPATH%\Mods\BTKUILib.dll" (
    xcopy /f /y "%CVRPATH%\Mods\BTKUILib.dll" "%~dp0ManagedLibs\"
)

@REM echo Generating file with all lib names
@REM echo 0Harmony>lib_names
@REM echo MelonLoader>>lib_names
@REM for %%f in ("%CVRPATH%\ChilloutVR_Data\Managed\*") do echo %%~nf>>lib_names

echo Press Enter to NStrip the files
pause

echo Nstrip convert all private/protected stuff to public, yay
for %%x in (Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.CoreModule.dll Cohtml.Runtime.dll) do (
    NStrip.exe -p -n "%CVRPATH%\ChilloutVR_Data\Managed\%%x" "%~dp0ManagedLibs\%%x"
)
echo We re done now
pause
