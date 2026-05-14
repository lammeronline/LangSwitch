@echo off
setlocal

set SLN=%~dp0..\LangSwitch.sln
set OUT=%~dp0out

echo Building LangSwitch...
dotnet publish "%SLN%" -c Release -r win-x64 --self-contained false -o "%OUT%"

if %errorlevel% neq 0 (
    echo.
    echo BUILD FAILED
    pause
    exit /b %errorlevel%
)

echo.
echo Done: %OUT%
pause
