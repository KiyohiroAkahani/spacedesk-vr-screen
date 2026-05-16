@echo off
rem ============================================================
rem  VrDesktopBridge - one-click launcher
rem  Double-click this file to start the app.
rem  No setup needed beyond the spacedesk prerequisites (README).
rem ============================================================
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "EXE=dist\VrDesktopBridge.exe"

rem 1) Prefer the published self-contained exe (no .NET needed).
if exist "%EXE%" (
  start "" "%EXE%"
  exit /b 0
)

rem 2) Otherwise use any already-built exe.
for /f "delims=" %%F in ('dir /b /s /o-d "src\VrDesktopBridge\bin\VrDesktopBridge.exe" 2^>nul') do (
  start "" "%%F"
  exit /b 0
)

rem 3) Otherwise build & run from source with the .NET SDK.
set "CSPROJ=src\VrDesktopBridge\VrDesktopBridge.csproj"
where dotnet >nul 2>nul && (
  echo Building and starting ^(first run may take a minute^)...
  dotnet run --project "%CSPROJ%" -c Release
  exit /b 0
)
if exist "%ProgramFiles%\dotnet\dotnet.exe" (
  echo Building and starting ^(first run may take a minute^)...
  "%ProgramFiles%\dotnet\dotnet.exe" run --project "%CSPROJ%" -c Release
  exit /b 0
)

echo.
echo  .NET 8 が見つかりません / .NET 8 was not found.
echo.
echo  どちらかを行ってください / Do one of:
echo   - 配布された dist\VrDesktopBridge.exe を置く
echo     ^(place the released VrDesktopBridge.exe into a "dist" folder^)
echo   - .NET 8 SDK を入れる / install the .NET 8 SDK:
echo     https://dotnet.microsoft.com/download/dotnet/8.0
echo.
pause
exit /b 1
