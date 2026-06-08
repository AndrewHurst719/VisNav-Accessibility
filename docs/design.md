# VisNav Accessibility — Design & Build Plan

## What it is

A single Windows tray application that runs assistive **overlays** on top of everything else on
screen. It does not embed into web pages or any app — it floats above them. Each feature is an
independent overlay the user can toggle on/off from a settings window or a global hotkey.

## Why these technical choices

| Concern | Choice | Why |
|---|---|---|
| Overlay windows | WPF, layered + click-through (`WS_EX_LAYERED`, `WS_EX_TRANSPARENT`), topmost | Proven path for transparent always-on-top overlays on Windows; per-pixel alpha. |
| Magnifier | Windows **Magnification API** (`magnification.dll` via P/Invoke) | Native, GPU-accelerated lens — same engine as Windows Magnifier. Far smoother than screenshot-and-scale. |
| Narrator text grouping | **UI Automation** (`System.Windows.Automation`) | Gets clean text + structure (paragraphs, headings) with exact screen coordinates from the browser/any app, without injecting into pages. How real screen readers work. Beats OCR on accuracy. |
| Text-to-speech | `System.Speech` (SAPI) now; Windows OneCore voices later | Free, offline, simple to start; upgrade path to higher-quality voices. |
| Settings storage | JSON file in `%AppData%\VisNav` | Simple, human-readable, no DB needed. |

**The tool's own UI must be accessible:** large fonts, high contrast, full keyboard + global
hotkey operation. An assistive tool you can't read is a contradiction.

## Design note: Stargardt's / central vision loss

Central (macular) vision loss preserves peripheral vision. This shapes defaults:
- Crosshair uses **full-width/height lines** catchable in the periphery, not a small pointer.
- High contrast and large hit targets throughout.
- Magnification centers on the cursor / point of regard.

## Build order (each step independently usable)

0. **Setup** — install .NET 8 SDK, scaffold solution, git + private GitHub repo.
1. **Skeleton** — tray app, high-contrast settings window, JSON-persisted settings, non-functional toggles.
2. **Crosshair** — full-screen transparent click-through overlay following the cursor; size/thickness/color/dim settings. (Easiest; instant win.)
3. **Magnifier** — Magnification-API lens following the cursor; zoom level, lens size, toggle hotkey.
4. **Narrator** — UI Automation finds text blocks on the focused window → overlay "▶ Read" buttons → TTS on click/hotkey. (Hardest; the differentiated feature.)
5. **Polish** — global hotkeys, start-on-login, README screenshots, CI build check.

## Project layout

```
src/
  VisNav.App/    WPF tray app: settings window, hotkeys, feature toggles, overlay windows
  VisNav.Core/   settings model + persistence, Win32 interop wrappers, shared models
tests/
  VisNav.Core.Tests/
```
