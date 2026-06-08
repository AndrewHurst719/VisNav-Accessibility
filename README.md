# VisNav Accessibility

A Windows desktop **overlay** toolkit for people with low vision. It floats on top of the
browser (and any other app) and provides large, easy-to-operate assistive tools. Built with
accessibility-first design — high contrast, large targets, keyboard/hotkey driven.

> Personal motivation: built for anyone who has trouble seeing. Inspired in part by a friend with
> Stargardt's disease (central vision loss), but designed for low vision generally.

## Features (planned)

- **Crosshair cursor** — large, high-contrast crosshair lines that follow the pointer, easy to
  locate in peripheral vision (which conditions like Stargardt's tend to preserve). Optional
  screen dimming to make it pop.
- **Magnifier** — a smooth lens that follows the cursor, powered by the native Windows
  Magnification API (the same engine behind Windows Magnifier). Adjustable zoom and lens size.
- **Narrator** — detects on-screen text blocks (paragraphs, headings) via Windows UI Automation,
  drops a large "▶ Read" button on each group, and reads it aloud with the OS voices. Works over
  the browser and other apps without injecting into the page.

All features live in a single tray app and can be toggled on/off independently.

## Platform & stack

- **Windows only** (Windows 10/11).
- **.NET 8 + WPF** for transparent, click-through, always-on-top overlay windows.
- **Windows Magnification API** (magnifier), **UI Automation** (narrator text grouping),
  **System.Speech / OS voices** (text-to-speech).

## Status

🚧 Early scaffolding. See [`docs/design.md`](docs/design.md) for the architecture and build plan.

## Repo layout

```
src/
  VisNav.App/    WPF tray app: settings window, hotkeys, feature toggles, overlays
  VisNav.Core/   settings persistence, Win32 interop wrappers, shared models
docs/            design notes
tests/           unit tests
```
