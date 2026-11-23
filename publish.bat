@echo off
title Publishing Visual Novel Detect (Release)
cd /d "%~dp0"

echo ===================================================
echo   Publishing Visual Novel Detect for Windows x64
echo ===================================================
echo.

if exist "publish" rd /s /q "publish"

dotnet publish VisualNovelDetect.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===================================================
    echo   Build succeeded!
    echo   Files are located in the "publish" folder:
    echo   %cd%\publish\VisualNovelDetect.exe
    echo ===================================================
    explorer "%cd%\publish"
) else (
    echo.
    echo Publish failed. Please check the errors above.
    pause
)
