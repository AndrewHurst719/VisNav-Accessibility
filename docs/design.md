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
- High contrast and large hit targets throughout.
- Magnification centers on the cursor / point of regard.

## Crosshair design (as built)

The crosshair is a **thin circle centered on the mouse pointer** that follows it every
frame — subtle by design, easy to locate without obscuring content. Settings:
- **Color** preset: Green, Red, White-on-black-outline, Cyan, Neon pink. (White-on-black
  draws a black outline behind a white ring so it stays visible on any background.)
- **Outer circle radius** and **outer circle thickness**.

Implementation: a transparent, click-through (`WS_EX_LAYERED | WS_EX_TRANSPARENT`),
topmost, non-activating window spanning the virtual screen; an `Ellipse` repositioned to
the cursor on `CompositionTarget.Rendering`. Cursor read via `GetCursorPos`; physical
pixels converted to WPF DIPs via the window's device transform.

> Known limitation: a single spanning window is correct on one monitor and on multi-monitor
> setups with **uniform** DPI scaling. **Mixed per-monitor DPI** would need one overlay
> window per monitor (Per-Monitor-V2) — a future enhancement.

## Magnifier design (as built)

A **rectangle lens that follows the cursor**, shown/hidden by a configurable global hotkey.

- **Rendering:** Windows Magnification API. A native Win32 host window (`WS_EX_TOPMOST |
  LAYERED | TRANSPARENT | TOOLWINDOW | NOACTIVATE`, class `VisNavMagnifierHost`) holds a
  `WC_MAGNIFIER` child with the `MS_SHOWMAGNIFIEDCURSOR` style so the pointer shows inside
  the lens. A 16 ms `DispatcherTimer` positions the lens so its **bottom-left corner sits at
  the cursor tip** (floating up-and-right, flipping near screen edges), sizes the child, and
  sets the magnified source rect (centered on the cursor, clamped to the virtual screen). All
  physical pixels. → `MagnifierEngine`.
- **Crosshair vs lens:** the crosshair overlay uses `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`
  so it renders on screen but never appears inside the magnified lens.
- **Activation hotkey:** up to 3 keys held together, mouse buttons included (Windows VK codes
  cover mouse buttons: `VK_MBUTTON=4`, `VK_XBUTTON1=5`…). Global `WH_KEYBOARD_LL` +
  `WH_MOUSE_LL` hooks track the pressed set; left/right modifier variants are normalized.
  Default **Ctrl + Middle-click**, **toggle** on/off. Rebindable via a "press your combination"
  capture box. → `HotkeyManager`, `ChordFormatter`.
- **Zoom:** **Shift + scroll wheel** while active (caught and swallowed by the mouse hook only
  when the lens is on, so it never breaks normal Shift+scroll). Also a zoom slider.
- **Size/shape:** a diagram with a draggable corner handle; the opposite corner (top-left)
  stays anchored while width/height change. Bound to `LensWidth`/`LensHeight` (physical px).
- `Interop/MagnifierNative.cs` (magnification + window plumbing), `Interop/HookNative.cs` (hooks).
- Same single-DPI caveat as the crosshair applies to cursor centering.

## Build order (each step independently usable)

0. **Setup** — install .NET 8 SDK, scaffold solution, git + private GitHub repo.
1. **Skeleton** — tray app, high-contrast settings window, JSON-persisted settings, non-functional toggles.
2. **Crosshair** — full-screen transparent click-through overlay following the cursor; size/thickness/color/dim settings. (Easiest; instant win.)
3. **Magnifier** — Magnification-API lens following the cursor; zoom level, lens size, toggle hotkey.
4. **Narrator** — UI Automation finds text blocks on the focused window → overlay "▶ Read" buttons → TTS on click/hotkey. (Hardest; the differentiated feature.)

## Narrator design (as built — Step 4a)

See [`research-narrator.md`](research-narrator.md) for the evidence behind these choices.

- **Trigger:** a configurable global hotkey (default **Ctrl+Shift+R**) scans the *foreground*
  window on demand (UIA is cross-process/expensive, so we scan on demand, not continuously).
  Same hook machinery as the magnifier; `HotkeyManager` now tracks multiple named chords.
- **Text grouper (`UiaTextScanner`):** walks the foreground window's UIA control-view subtree
  collecting text-bearing elements — `Text` labels (via Name) and `Document`/`Edit` controls
  (via `TextPattern` visible text) — each with its on-screen `BoundingRectangle`, then lightly
  merges fragments that are clearly the same paragraph (small vertical gap + horizontal
  overlap). Bounded traversal (≤4000 elements, ≤60 blocks) to keep it responsive. Verified on
  a live window: 70 elements → 16 sensible blocks with correct screen coords.
- **Read overlay (`ReadButtonsOverlay`):** a dim, full-screen overlay outlines each block and
  drops a large "▶ Read" button; clicking reads that block and highlights it (button → "⏹ Stop").
  Esc or a click on empty space dismisses; pressing the hotkey again toggles it off.
- **TTS (`NarratorService`):** `System.Speech` (SAPI5) — free, offline; voice + rate are
  configurable; start/complete events drive the block highlight. Voices found on dev machine:
  Microsoft David, Microsoft Zira.

> **Deferred to Step 4b:** OCR fallback (`Windows.Media.Ocr`) for inaccessible/canvas apps,
> stronger paragraph segmentation (RXYC/Docstrum over UIA/OCR boxes), heading detection, and
> word-level highlight via `SpeakProgress`. The current light merge can over-group tightly
> stacked labels — fine for prose, tunable later.
5. **Polish** — global hotkeys, start-on-login, README screenshots, CI build check.

## Project layout

```
src/
  VisNav.App/    WPF tray app: settings window, hotkeys, feature toggles, overlay windows
  VisNav.Core/   settings model + persistence, Win32 interop wrappers, shared models
tests/
  VisNav.Core.Tests/
```
