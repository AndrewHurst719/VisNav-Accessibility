# Narrator research — text grouping & TTS (2026-06-10)

Deep, multi-source, adversarially-verified research to choose how to build the **text grouper**
and **text-to-speech** for the Narrator feature. 28 sources, 25 claims verified (22 confirmed,
3 refuted).

## Bottom line / recommendation

**Text grouper = hybrid: UI Automation first, OCR fallback.** This is exactly what NVDA (the
leading Windows screen reader) does — accessibility APIs first, built-in Windows OCR for
inaccessible content. We do not inject into web pages; we read the OS accessibility tree.

**TTS = `System.Speech` (SAPI5)** for a free, offline baseline that exposes word-boundary
(`SpeakProgress`) and `VisemeReached` events for highlight-as-it-reads. Optionally upgrade to
WinRT `Windows.Media.SpeechSynthesis` (OneCore neural voices) later for quality.

## Text grouper — verified findings

1. **UIA `TextPattern` is the Microsoft-blessed read-only API for read-aloud tools.** It
   exposes text content, attributes, embedded objects, and navigation by character/word/line/
   **paragraph** via `ExpandToEnclosingUnit` / `MoveEndpointByUnit`. *(high confidence)*

2. **Paragraph granularity is NOT guaranteed.** Only `Character` and `Document` TextUnits are
   mandatory for a provider; `Paragraph`/`Line`/`Word`/`Page` are optional and a request
   **silently degrades upward** to the next supported unit (often `Document`). → A UIA-only
   grouper is insufficient; we must implement our own segmentation fallback. *(high)*

3. **Performance: `TextPattern` is cross-process COM with no caching.** Per-character `GetText`
   = one cross-process hit *each*; one `GetText(-1)` = a single hit but higher latency on big
   providers. → Fetch block text in one call, never loop per-character, budget for cost in a
   real-time overlay. *(high)*

4. **Coordinates for button placement:** `GetBoundingRectangles` returns per-line rects in
   **screen coordinates** (what we need to place "▶ Read" buttons); `GetVisibleRanges` limits to
   the on-screen viewport. **Caveat:** these are *visibility* spans, not semantic blocks — a
   claim that visible ranges are "directly usable as blocks" was **refuted 0-3**. Provider
   quality varies (a known bug returns whole-line bounds with Left==0; some return only
   fully-visible lines). *(high)*

5. **Browsers expose text without injection.** Chromium (Chrome/Edge) implements MSAA +
   IAccessible2 + UIA; **Chrome 138 (Aug 2025) enables native UIA by default**. We read a
   **cached/mirrored** accessibility tree (renderer → `ui::AXNodeData` → browser process), so
   expect possible staleness/latency vs the live page. Char-level geometry exists internally
   ("inline text boxes") but is only reachable via bounding-box queries, not directly. *(high)*
   *(Firefox-specific UIA reliability was NOT directly verified — open question.)*

6. **OCR fallback = `Windows.Media.Ocr` (built-in WinRT, same engine NVDA uses).** Returns
   `OcrResult → OcrLine → OcrWord` with `BoundingRect`, but **no paragraph/block grouping** —
   we cluster the boxes ourselves. *(high)*

7. **Segmentation algorithms with real C#/.NET references:** **Recursive X-Y Cut** (top-down,
   splits into columns/blocks) and **Docstrum** (bottom-up, KNN-clusters words → lines →
   blocks), both implemented in **PdfPig** (`UglyToad.PdfPig.DocumentLayoutAnalysis`), plus a
   Klampfl-based reading-order detector. **Caveat:** PdfPig consumes PDF glyph boxes, so these
   are *portable, not turnkey* — we feed them UIA/OCR boxes or port the code. *(high; one
   supporting detail passed only 2-1)*. A modern ML graph-segmentation (GCN) approach was
   **refuted 1-2** as a recommended dependency — keep heuristics for a free .NET tool.

8. **Heading detection:** UIA `TextAttribute` `FontSize`/`FontWeight`, or relative OCR
   word-height / line-spacing heuristics when only OCR is available.

## TTS — verified findings

- **`System.Speech` (SAPI5)** is available on modern .NET (stable 10.0.x package line targets
  .NET 8). Exposes `VisemeReached` and `SpeakProgress` (word-boundary) events → synchronized
  highlighting. Free, offline, in-box. *(high)*
- **WinRT `Windows.Media.SpeechSynthesis`** (OneCore neural voices) = higher quality, but
  word-boundary timing comes via SSML word markers / timed metadata cues rather than a simple
  event, and adds WinRT-in-WPF interop friction. *(open question — not fully resolved)*
- **Azure neural voices** = best quality + clean `WordBoundary` events, but cloud + cost +
  not offline. Out of scope for the free baseline.

## Refuted claims (excluded)

- GetVisibleRanges ranges are directly usable as on-screen "blocks" — **0-3**.
- ML GCN cluster-and-sort is the recommended grouper design — **1-2** (background only).
- NVDA has an explicit UIA-for-Chromium / Office toggle — **0-3**.

## Open questions (worth a spike before/while building)

1. WinRT vs Azure voice quality and whether WinRT word-boundary timing is usable for live
   highlighting in WPF; real .NET 8 WinRT interop friction.
2. Firefox: how cleanly it exposes paragraph/heading structure + bounding rects to an external
   UIA client (vs its IAccessible2 path); whether we must request a specific accessibility mode.
3. Real-world heading-detection accuracy from UIA TextAttributes vs OCR height heuristics.
4. Measured end-to-end latency of a UIA-first full-window scan (no caching, cross-process) for
   a real-time overlay — and the crossover point where OCR-on-bitmap beats UIA traversal.

## Proposed implementation shape (for the build)

1. **UIA path:** walk the foreground window's UIA subtree → find `Document`/`Edit`/`Text`/
   `Group` elements with a `TextPattern` → `GetVisibleRanges` to limit to on-screen → try
   `ExpandToEnclosingUnit(Paragraph)`; **detect degradation to Document** and fall back to
   line-level ranges + our clustering → `GetBoundingRectangles` for button coords. One
   `GetText` per block; no per-character calls.
2. **OCR fallback** (when UIA yields no usable text — canvas/GDI/custom-drawn apps, images):
   capture the window bitmap → `Windows.Media.Ocr` → cluster `OcrWord` boxes into paragraphs.
3. **Segmenter:** port/adapt PdfPig RXYC or Docstrum to consume UIA/OCR boxes; heading
   detection via font size/weight; Klampfl-based reading order.
4. **TTS:** `System.Speech` baseline with `SpeakProgress`/`VisemeReached` highlight sync;
   leave a seam to swap in WinRT OneCore voices later.

### Key sources
- UIA TextPattern overview + implementing text/textrange (learn.microsoft.com)
- `ExpandToEnclosingUnit`, `GetBoundingRectangles`, `GetVisibleRanges` API docs
- Chromium accessibility overview + Chrome "Windows UIA support update" blog
- `Windows.Media.Ocr` OcrEngine/OcrLine/OcrWord docs; NVDA user guide + nvda-ocr repo
- PdfPig DocumentLayoutAnalysis (RecursiveXYCut.cs, DocstrumBoundingBoxes.cs) + BobLd/DocumentLayoutAnalysis
- `System.Speech` NuGet + `VisemeReached`/`SpeakProgressEventArgs`; Azure `WordBoundary`; WinRT timed metadata cues
