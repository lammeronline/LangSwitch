@echo off
setlocal

set PROJ=%~dp0..\LangSwitch\LangSwitch.csproj
set OUT=%~dp0out

echo Stopping running instance...
taskkill /f /im LangSwitch.exe >nul 2>&1

if exist "%OUT%" rmdir /s /q "%OUT%"

echo Building LangSwitch (single-file, self-contained)...
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=none ^
    -o "%OUT%"

if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED
    pause
    exit /b %errorlevel%
)

echo.
echo Done: %OUT%
pause
