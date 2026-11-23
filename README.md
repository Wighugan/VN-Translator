# Visual Novel Detect (VN Detect)

A lightweight, low-latency Windows desktop application built in **C# (.NET 8, WPF)** designed specifically for Visual Novel players. It captures text directly from running Visual Novel games (borderless or windowed), recognizes the target word at the cursor via OCR, looks up its definition from the **Cambridge Dictionary** and translation from **Google Translate** in parallel, and presents a non-intrusive floating popup **without stealing keyboard or mouse focus** from the game.

---

## ⚡ How to Run (Without Terminal)

You do **not** need to open a terminal or run `dotnet build` every time:

1. **Option A (One-Click Launcher)**:
   - Double-click **`Run-VisualNovelDetect.bat`** in this folder. It immediately launches the compiled application.
2. **Option B (Direct Executable / Desktop Shortcut)**:
   - Navigate to `publish\VisualNovelDetect.exe` (or `bin\Debug\net8.0-windows10.0.19041.0\VisualNovelDetect.exe`).
   - Right-click `VisualNovelDetect.exe` $\to$ **Send to** $\to$ **Desktop (create shortcut)**.
   - Now you can launch it anytime directly from your desktop like any normal Windows program!

---

## 📦 What Should Users Download When Pushed to GitHub?

When you push this repository to GitHub, your end users **do NOT need to clone the repository, install the .NET SDK, or compile code**. 

### What Users Download:
- In your GitHub repository's **Releases** section (on the right sidebar of GitHub), users download a single file:
  **`VisualNovelDetect-Windows-x64.zip`**
- The user simply:
  1. Downloads `VisualNovelDetect-Windows-x64.zip`.
  2. Extracts it to any folder.
  3. Double-clicks **`VisualNovelDetect.exe`** to start playing!

### How to Create the GitHub Release:

#### Method 1: Automated via GitHub Actions (Recommended)
This repository already includes `.github/workflows/release.yml`. Whenever you push a release tag:
```powershell
git tag v1.0.0
git push origin v1.0.0
```
GitHub Actions will automatically compile the release, package it into `VisualNovelDetect-Windows-x64.zip`, and attach it to your GitHub Releases page!

#### Method 2: Manual Local Publish
1. Double-click **`publish.bat`** in this folder.
2. It compiles a standalone release build into the `publish\` folder and opens it in Windows Explorer.
3. Right-click the `publish` folder $\to$ **Compress to ZIP file** (name it `VisualNovelDetect-v1.0.0-win-x64.zip`).
4. On GitHub, go to **Releases** $\to$ **Draft a new release**, drag and drop your `.zip` file, and click **Publish release**!

---

## 🌟 Key Features & Capabilities

1. **Focus-Safe Non-Intrusive Floating Popup**:
   - Built with `ShowActivated="False"` and Win32 `WS_EX_NOACTIVATE` (`0x08000000`) and `WS_EX_TOOLWINDOW` (`0x00000080`).
   - The game window never loses keyboard, mouse, or gamepad focus when you hover over words. You can continue advancing dialogues with `Space` or `Enter` seamlessly.

2. **Fixed Screen Position & Movable Popup (No More Obscuring Words)**:
   - **Fixed Position Mode (Default)**: Instead of spawning directly over the dialogue text where it obstructs reading, the popup appears at your preferred screen location (upper-right corner by default).
   - **`✥ Move` Handle**: A dedicated move handle button on the top-right of the popup header lets you click and drag the popup anywhere across your monitors. The application automatically remembers your custom position for all future lookups!
   - Reset button available in Settings to restore default positioning at any time.

3. **Resizable Popup**:
   - The popup window can be freely resized in both dimensions (width and height).
   - Use the **bottom-right corner resize grip** (`◢`) to expand or shrink the card. Content dynamically adapts, and the definition scroll area expands to show more definitions and examples.
   - Resized dimensions are automatically remembered.

4. **Interactive Shortcut Key Recorder**:
   - In Settings under the **Shortcut & Position** tab, click the shortcut box or the **`🔴 Record`** button.
   - Press your desired key combination directly on your keyboard (e.g., `Ctrl + Shift + D`, `Alt + Q`, `Ctrl + Space`, `F1`, etc.).
   - Automatically detects modifiers (`Ctrl`, `Alt`, `Shift`) and key codes without needing manual checkbox configurations.

5. **Per-Monitor DPI V2 & Multi-Monitor Compatibility**:
   - Native awareness of Per-Monitor V2 DPI (`app.manifest` + Win32 DPI APIs).
   - Accurately converts physical screen pixels to WPF DIPs across mixed DPI setups (100%, 125%, 150%, 200%).
   - Automatic screen clamping to ensure the popup never clips off screen edges.

6. **Image Preprocessing & OCR Word Detection**:
   - 2x bicubic upscaling to optimize glyph recognition for small dialogue fonts.
   - Grayscale conversion ($Y = 0.299R + 0.587G + 0.114B$) and contrast stretching.
   - Adaptive Otsu binarization to cleanly extract text from semi-transparent VN text boxes and dark backgrounds.
   - **Primary Engine**: Native `Windows.Media.Ocr.OcrEngine` (ultra-fast, zero external runtime dependency).
   - **Fallback Engine**: `Tesseract` OCR engine adapter.
   - Sanitizes punctuation, Japanese quotation marks (`「`, `」`, `『`, `』`), quotes (`“`, `”`, `'`), ellipses, and extracts lemma base forms.

7. **Parallel Cambridge Dictionary & Google Translate**:
   - Calls both services simultaneously (`Task.WhenAll`).
   - **Cambridge Dictionary**: Headword, Part of Speech badge, IPA pronunciation (`/ˈkæm.brɪdʒ/`), audio pronunciation URL, top 2–3 concise definitions, and example sentences. Includes intelligent English lemmatizer for inflected forms (e.g. `went` $\to$ `go`, `running` $\to$ `run`, `cities` $\to$ `city`). Supports official Cambridge API or zero-config Cambridge web adapter.
   - **Google Translate**: Translates the isolated word and optionally the surrounding dialogue sentence for context. Supports official Google Cloud Translation API or zero-config public endpoint.

8. **SQLite Persistent & In-Memory Caching**:
   - Instant 0 ms repeat lookups via two-tier caching: in-memory `ConcurrentDictionary` backed by a local SQLite database in `%AppData%\VisualNovelDetect\cache.db`.

---

## 🏗️ Architecture & Project Structure

```
Visual Novel Detect/
├── VisualNovelDetect.sln                     # Visual Studio Solution
├── VisualNovelDetect.csproj                  # .NET 8 WPF Project
├── Run-VisualNovelDetect.bat                 # One-click launcher
├── publish.bat                               # One-click release builder
├── .github/workflows/release.yml             # Automated GitHub Release CI/CD
├── app.manifest                              # PerMonitorV2 DPI awareness manifest
├── App.xaml / App.xaml.cs                    # DI host, tray setup, and lifecycle
│
├── Models/
│   ├── AppSettings.cs                        # Persistent configuration settings
│   ├── DetectedWordInfo.cs                   # Physical & DIP bounding boxes, distance
│   ├── DictionaryResult.cs                   # Cambridge headword, IPA, definitions, audio URL
│   ├── ScreenRegion.cs                       # Dialogue box physical coordinates
│   └── TranslationResult.cs                  # Word and sentence translation
│
├── Native/
│   ├── Win32.cs                              # Win32 P/Invoke declarations   
│   └── DpiHelper.cs                          # Per-monitor DPI scaling, fixed pos & clamping
│
├── Services/
│   ├── Capture/                              # Screen capture & image preprocessing
│   ├── Ocr/                                  # Windows OCR & Tesseract services
│   ├── Dictionary/                           # Cambridge dictionary & word lemmatizer
│   ├── Translation/                          # Google Translate service
│   ├── Cache/                                # Memory cache + SQLite database
│   ├── Audio/                                # Pronunciation audio playback
│   ├── Hotkey/                               # Win32 RegisterHotKey & WH_MOUSE_LL hook
│   ├── Lookup/                               # Parallel lookup coordinator
│   └── Settings/                             # App settings JSON persistence
│
├── ViewModels/                               # MVVM ViewModels (Popup, Settings, Tray, Region)
├── Views/                                    # WPF Views (PopupWindow, SettingsWindow, Region)
└── VisualNovelDetect.Tests/                  # Automated unit test suite (30 unit tests)
```
#   V N - T r a n s l a t o r 
 
 
