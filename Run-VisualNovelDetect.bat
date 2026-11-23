@echo off
title Visual Novel Detect
cd /d "%~dp0"

if exist "publish\VisualNovelDetect.exe" (
    start "" "publish\VisualNovelDetect.exe"
    exit
)

if exist "bin\Debug\net8.0-windows10.0.19041.0\VisualNovelDetect.exe" (
    start "" "bin\Debug\net8.0-windows10.0.19041.0\VisualNovelDetect.exe"
    exit
)

echo VisualNovelDetect.exe not found. Building now...
dotnet build VisualNovelDetect.csproj -c Release -o publish
start "" "publish\VisualNovelDetect.exe"
exit
