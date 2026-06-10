# VisNav Accessibility

A Windows desktop overlay toolkit for computer users with trouble seeing. It floats on top of the
browser, and any other app, and provides easy to operate assistive tools. Built with a high skill ceiling but a low learning curve in mind and operated with keyboard/hotkeys.

VisNav runs in the **system tray** (crosshair icon). Open **Settings** from the tray menu to
toggle tools and rebind hotkeys.

## ⬇️ Download & run (nothing to install)

1. Open the **[latest release](https://github.com/AndrewHurst719/VisNav-Accessibility/releases/latest)**
   and, under **Assets**, download **`VisNav-win-x64.zip`**.
2. Right-click the downloaded zip → **Extract All…**
3. Open the extracted folder and double-click **`VisNav.exe`**.
   - If Windows shows a blue *"Windows protected your PC"* box, click **More info → Run anyway**
     (the app simply isn't code-signed yet).
4. The Settings window opens and a small **crosshair icon** appears in your system tray
   (bottom-right, maybe under the `^` arrow). To quit: right-click that icon → **Exit**.

No .NET or other software required — everything is bundled in the one file. (There's also a
`HOW TO RUN.txt` inside the zip.) Settings are saved to `%AppData%\VisNav\settings.json`.

## Features

### Crosshair cursor
A thin, high-contrast circle centered on the mouse pointer that follows it everywhere — easy to
locate without obscuring content. Configurable **color** (green / red / white-on-black / cyan /
pink), **radius**, **thickness**, and **opacity**. It's excluded from screen capture, so it never
shows up inside the magnifier or screenshots.

### Magnifier
A GPU-accelerated lens (Windows Magnification API) that floats next to the cursor — its corner sits
at the pointer tip. Configurable **starting zoom** and **lens size** (drag the corner of the
on-screen diagram). The magnified cursor shows inside the lens.

- **Toggle:** `Ctrl + Shift + F` (default)
- **Zoom while active:** hold `Shift` and scroll the mouse wheel (up = in, down = out)

### Narrator (text-to-speech)
Two ways to have on-screen text read aloud (offline, via Windows SAPI voices):

- **Scan & hover** (`Ctrl + Shift + R`) — finds text blocks on the focused window via UI
  Automation; move the cursor over a block to highlight it, click to read it. Esc dismisses.
  A **grouping intensity** slider controls how tightly text is grouped.
- **Read a region** (`Ctrl + Shift + Q`, or the tray menu) — drag a box around *any* text, even
  in apps with no accessible text (Electron apps, images, games, PDFs). It's OCR'd
  (Windows.Media.Ocr) and read aloud, with the cropped area kept highlighted while it reads.
  Esc or the ✕ button cancels.
- **Pause/resume:** `Ctrl + Shift + P` · **Stop:** `Ctrl + Shift + X`. The most recent request
  always takes priority over any current speech.

All hotkeys are rebindable in Settings. Each tool can be toggled independently.

## Requirements

- Windows 10 (version 2004 / build 19041) or Windows 11.
- A speech voice (Windows ships with at least one) and the English OCR language pack for the
  read-a-region feature (both included by default on most installs).

> The downloadable release is self-contained — end users need nothing else installed. The
> .NET 8 SDK is only required to build from source (below).

## Build from source (developers)

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). From the repo root:

```powershell
./run.ps1            # Debug build, launches the tray app  (or double-click run.cmd)
./publish.ps1        # self-contained release zip -> dist/VisNav-win-x64.zip
```

## Platform & stack

- **Windows only**, **.NET 8 + WPF** (transparent, click-through, always-on-top overlays).
- **Windows Magnification API** (magnifier), **UI Automation** (text scanning),
  **Windows.Media.Ocr** (region OCR), **System.Speech / SAPI** (text-to-speech).

## Repo layout

```
src/
  VisNav.App/    WPF tray app: settings window, hotkeys, overlays, magnifier, narrator, OCR
  VisNav.Core/   settings model + JSON persistence
tests/
  VisNav.Core.Tests/   unit tests
docs/            design notes (design.md) + narrator research (research-narrator.md)
run.ps1 / run.cmd / publish.ps1   build, launch, and package helpers
```
