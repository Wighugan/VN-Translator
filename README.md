# Visual Novel Detect (VN Detect)

A lightweight, non-intrusive word lookup tool built for learning English while playing Visual Novels.

It captures text directly from running Visual Novel games (windowed or borderless), detects the exact word at your cursor, and fetches **Cambridge Dictionary** definitions alongside **Google Translate** results in parallel. Everything appears in a clean pop-up overlay **without stealing keyboard, mouse, or controller focus** from your game.

> **Note:** Looking for a tool that translates entire dialogue boxes or the whole game? This isn't it. This tool is designed specifically for players who want to read and learn English through Visual Novels, it translates and explains **only the single word** you want to look up, nothing more.
>
> It's also useful for translating everything on your screen, not VN only.

---

## Table of Contents

- [Features](#-features)
- [User Guide (How to Run & Use)](#-user-guide)
  - [Download Pre-built Release](#1-download-pre-built-release)
  - [How to Use in Game](#2-how-to-use)
  - [Settings & Customization](#3-settings--customization)
- [How to Build from Source](#-how-to-build-from-source)
  - [Prerequisites](#prerequisites)
  - [Clone Repository](#1-clone-repository)
  - [Build and Run (Debug)](#2-build-and-run-debug)
  - [Run Unit Tests](#3-run-unit-tests)
  - [Create Standalone Release Build](#4-create-standalone-release-build)
- [Architecture & Tech Stack](#-architecture--tech-stack)
- [License](#-license)

---

## Features

- **Game Focus-Safe (No Alt-Tab / No Lost Inputs)**:
  - You can continue advancing dialogues with `Space`, `Enter`, or gamepad buttons without the popup interrupting your game controls.
- **Parallel Cambridge Dictionary & Google Translate**:
  - Queries Cambridge Dictionary and Google Translate simultaneously in parallel.
  - **Cambridge Dictionary**: Explanation from Cambridge Dictionary.
  - **Google Translate**: Translate the word into your language.
- **Pop-up**:
  - **Comfortable Fixed Positioning**: Spawns at your preferred comfortable screen area (upper-right by default) instead of blocking the dialogue text you are reading.
  - **`Move` Handle**: Click and drag the move handle on the top-right to place the popup anywhere across your monitors.
  - **Resize**: Drag the bottom-right corner grip to resize the popup.
  - **Pin**: Click the **Pin** button in the header or check *"Pin popup by default"* in Settings to keep the popup open indefinitely without disappearing. Unpinned popups automatically dismiss after your configured timeout (default 6 seconds) or upon closing.
- **Custom Interactive Shortcut Key Recorder**:
  - Set your preferred trigger combination in Settings simply by pressing keys on your keyboard (e.g., `Ctrl + Shift + D`, `Alt + Q`, `Ctrl + Space`, `F1`, etc.).
  - Supports Middle-Mouse Click trigger as well.
- **Configuration Default**:
  - Works out of the box using Windows Native OCR and zero-config dictionary/translation endpoints. Optional API key support for Cambridge and Google Cloud Translation.

---

## User Guide

### 1. Download [Pre-built Release](https://github.com/Wighugan/VN-Translator/releases)
1. Extract the `.zip` archive to any folder on your computer.
2. Double-click **`VisualNovelDetect.exe`** to launch the app.
3. The app will minimize to your Windows System Tray (near the clock on your taskbar).

### 2. How to use
1. Launch your Visual Novel game (borderless windowed or windowed mode).
2. Hover your mouse cursor over any word in the game's dialogue box.
3. Press the global shortcut:
   - **Default Hotkey**: `Ctrl + Shift + D`
   - **Default Mouse Trigger**: Click your **Middle Mouse Button** (scroll wheel click).
4. The floating popup appears near your saved location:
   - **Google Translate**: Direct translation of the word and the full dialogue sentence into your target language (default: Vietnamese `VI`).
   - **Cambridge Explanations**: Rich list of definitions, CEFR badges, grammar codes, and example sentences.
5. **Moving & Resizing**:
   - Drag the **`Move`** button on the top right to relocate the popup anywhere.
   - Drag the bottom-right corner grip to resize the card.
6. **Dismissal**:
   - The popup hides automatically after 6 seconds (Default), or click the `✕` close button.
   - Click **`Pin`** to keep the popup open permanently.

### 3. Settings & Customization
Right-click the **Visual Novel Detect** icon in the Windows System Tray and choose **Settings**:
- **Shortcut & Position Tab**:
  - Click **Record** and press any key combination to rebind your shortcut.
  - Toggle Middle-Mouse Click trigger.
  - Toggle Fixed Screen Position mode vs. dynamic cursor follow mode.
- **Languages & OCR Tab**:
  - **Target Language**: Change translation destination (e.g., `vi` for Vietnamese, `ja` for Japanese, `es` for Spanish, `fr` for French, etc.).
  - **OCR Engine**: Switch between `Windows Native OCR` (recommended) and `Tesseract`.
  - **Image Preprocessing**: Toggle 2x upscale and adaptive binarization for stylized fonts.
- **Appearance & API Keys Tab**:
  - Adjust popup opacity slider.
  - Set auto-close delay seconds (or `0` to disable).
  - Enable **Pin popup by default**.
  - (Optional) Enter your private Cambridge Dictionary or Google Cloud Translation API keys.

---

## Build from Source

You can easily compile and package the application yourself using the .NET SDK.

### Prerequisites
- **Operating System**: Windows 10 (version 19041 or higher) or Windows 11 (x64).
- **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.

### 1. Clone Repository
```powershell
git clone https://github.com/your-username/VisualNovelDetect.git
cd "VisualNovelDetect"
```

### 2. Build and Run (Debug)
To build and run the application in development mode:
```powershell
dotnet restore
dotnet build VisualNovelDetect.sln
dotnet run --project VisualNovelDetect.csproj
```

### 3. Run Unit Tests
The project contains 51 automated unit tests covering word lemmatization, OCR punctuation sanitization, SQLite caching, idiom detection, DPI coordinate clamping, and the translation pipeline:
```powershell
dotnet test VisualNovelDetect.sln
```

### 4. Create Standalone Release Build
You can package the application into a single self-contained executable that runs on any 64-bit Windows machine **without requiring users to install the .NET runtime**:

#### Option A: Using the Included Script
Double-click **`publish.bat`** or run:
```powershell
.\publish.bat
```

#### Option B: Using .NET CLI
```powershell
dotnet publish VisualNovelDetect.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish
```
The output executable will be placed in the `publish\` directory:
```
publish/
├── VisualNovelDetect.exe       # Standalone executable (~170 MB with runtime included)
├── e_sqlite3.dll               # Native SQLite provider
└── ...
```

---

## Architecture & Tech Stack

```
Visual Novel Detect/
├── VisualNovelDetect.sln                     # Visual Studio Solution
├── VisualNovelDetect.csproj                  # .NET 8 WPF Project (net8.0-windows10.0.19041.0)
├── Run-VisualNovelDetect.bat                 # One-click desktop launcher
├── publish.bat                               # Standalone build & packaging script
├── .github/workflows/release.yml             # Automated CI/CD GitHub release workflow
├── app.manifest                              # PerMonitorV2 DPI awareness manifest
├── App.xaml / App.xaml.cs                    # DI Host (Microsoft.Extensions.DependencyInjection), Tray icon
│
├── Models/
│   ├── AppSettings.cs                        # Application settings schema
│   ├── DetectedWordInfo.cs                   # Physical & DIP bounding boxes, sentence context
│   ├── DictionaryResult.cs                   # Cambridge headwords, senses, IPA, CEFR, idioms
│   ├── ScreenRegion.cs                       # Dialogue box bounding coordinates
│   └── TranslationResult.cs                  # Word and sentence translations
│
├── Native/
│   ├── Win32.cs                              # P/Invoke for WS_EX_NOACTIVATE, hooks, DPI APIs
│   └── DpiHelper.cs                          # Per-monitor DPI scaling, clamping & position algorithms
│
├── Services/
│   ├── Capture/                              # Screen capture & image preprocessing (upscale, binarization)
│   ├── Ocr/                                  # Windows Media OCR & Tesseract adapter
│   ├── Dictionary/                           # Cambridge dictionary scraper/API & IdiomDetector
│   ├── Translation/                          # Multi-tier Google Translate & MyMemory service
│   ├── Cache/                                # Memory cache + persistent SQLite database
│   ├── Audio/                                # Pronunciation audio playback
│   ├── Hotkey/                               # Win32 RegisterHotKey & low-level mouse hooks
│   ├── Lookup/                               # Parallel lookup coordinator
│   └── Settings/                             # Local %AppData% JSON settings persistence
│
├── ViewModels/                               # MVVM CommunityToolkit ViewModels
│   ├── PopupViewModel.cs                     # Popup card state, timer, pin, move, and sizing
│   ├── SettingsViewModel.cs                  # Settings window logic & shortcut recorder
│   ├── TrayViewModel.cs                      # System tray context menu
│   └── RegionSelectorViewModel.cs            # Dialogue region selection overlay
│
├── Views/                                    # WPF Views
│   ├── PopupWindow.xaml                      # Non-intrusive floating popup & custom scrollbar
│   ├── SettingsWindow.xaml                   # Tabbed settings dialog with interactive recorder
│   └── RegionSelectorWindow.xaml             # Draggable screen region selector
│
└── VisualNovelDetect.Tests/                  # Unit test suite (51 tests via xUnit)
```

- **Framework**: .NET 8 WPF (`net8.0-windows10.0.19041.0`)
- **MVVM Framework**: `CommunityToolkit.Mvvm`
- **System Tray**: `Hardcodet.NotifyIcon.Wpf`
- **Database & Caching**: `Microsoft.Data.Sqlite`
- **OCR Engine**: Windows Runtime Native OCR (`Windows.Media.Ocr.OcrEngine`) + `Tesseract` fallback
- **DPI Handling**: Windows PerMonitorV2 DPI awareness

---

## License

This project is licensed under the [MIT License](LICENSE). You are free to use, modify, and distribute it for personal or commercial projects.
