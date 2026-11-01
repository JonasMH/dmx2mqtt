@echo off
rem Builds build\dmxaudio.dll (microphone capture over miniaudio) with the Visual Studio
rem C++ build tools. Works from a plain Windows shell and from WSL (make native-windows).
rem Keep the miniaudio version and hash in step with the Makefile.
setlocal
set MINIAUDIO_VERSION=0.11.26
set MINIAUDIO_SHA256=6B376C7FBB1352BA1E0E432AB66069B335EA9405E45CB6C30B97EF0F3C9C3E0E

rem pushd, not cd: it also maps a \\wsl$ path to a drive letter
pushd "%~dp0" || exit /b 1
if not exist build mkdir build

if not exist build\miniaudio.h (
	curl.exe -fsSL -o build\miniaudio.h.tmp https://raw.githubusercontent.com/mackron/miniaudio/%MINIAUDIO_VERSION%/miniaudio.h || goto fail
	powershell -NoProfile -Command "if ((Get-FileHash build\miniaudio.h.tmp).Hash -ne '%MINIAUDIO_SHA256%') { Write-Error 'miniaudio.h: wrong checksum'; exit 1 }" || goto fail
	move /y build\miniaudio.h.tmp build\miniaudio.h >nul
)

where cl.exe >nul 2>nul
if errorlevel 1 (
	for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set VS=%%i
)
if defined VS call "%VS%\VC\Auxiliary\Build\vcvars64.bat" >nul || goto fail

cl.exe /nologo /O2 /LD /W3 /Ibuild dmxaudio.c /Fobuild\ /Febuild\dmxaudio.dll || goto fail
popd
exit /b 0

:fail
popd
exit /b 1
