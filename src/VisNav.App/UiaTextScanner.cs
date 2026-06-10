using System.Text;
using System.Windows;
using System.Windows.Automation;
using VisNav.App.Interop;

namespace VisNav.App;

/// <summary>
/// Finds readable text blocks on a window via UI Automation (no page injection).
///
/// Strategy (the research-recommended UIA-first path): walk the window's control-view
/// subtree collecting text-bearing elements — <c>Text</c> labels (via their Name) and
/// <c>Document</c>/<c>Edit</c> controls (via TextPattern visible text) — each with its
/// on-screen bounding rectangle, then lightly merge fragments that are clearly the same
/// paragraph (touching vertically and overlapping horizontally). Heavy layout analysis and
/// an OCR fallback are a later step; this covers accessible apps and browsers.
/// </summary>
public sealed class UiaTextScanner
{
    private const int MaxElementsVisited = 4000;
    private const int MaxBlocks = 60;
    private const int MaxDocChars = 4000;

    /// <param name="groupingIntensity">1–10. Higher = tighter (less merging); lower = larger
    /// groups (more merging across vertical gaps). See <see cref="GapFactorFor"/>.</param>
    public IReadOnlyList<TextGroup> Scan(IntPtr windowHandle, int groupingIntensity = 5)
    {
        if (windowHandle == IntPtr.Zero)
            return Array.Empty<TextGroup>();

        Rect windowBounds = GetWindowBounds(windowHandle);

        AutomationElement root;
        try
        {
            root = AutomationElement.FromHandle(windowHandle);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or ArgumentException)
        {
            return Array.Empty<TextGroup>();
        }

        var raw = new List<TextGroup>();
        int visited = 0;
        Collect(root, windowBounds, raw, ref visited);

        var merged = MergeParagraphFragments(raw, GapFactorFor(groupingIntensity))
            .OrderBy(b => b.Bounds.Top)
            .ThenBy(b => b.Bounds.Left)
            .Take(MaxBlocks)
            .ToList();

        Diagnostics.Log($"UIA scan: visited={visited} raw={raw.Count} merged={merged.Count} intensity={groupingIntensity}");
        return merged;
    }

    private static Rect GetWindowBounds(IntPtr hWnd)
    {
        if (NativeMethods.GetWindowRect(hWnd, out var r))
            return new Rect(r.Left, r.Top, Math.Max(0, r.Right - r.Left), Math.Max(0, r.Bottom - r.Top));
        return new Rect(0, 0, double.MaxValue, double.MaxValue);
    }

    private void Collect(AutomationElement element, Rect windowBounds, List<TextGroup> output, ref int visited)
    {
        if (visited >= MaxElementsVisited || output.Count >= MaxBlocks)
            return;
        visited++;

        try
        {
            var info = element.Current;

            // Skip offscreen / zero-size subtrees early.
            Rect bounds = info.BoundingRectangle;
            if (info.IsOffscreen || bounds.IsEmpty || bounds.Width < 4 || bounds.Height < 4)
            {
                RecurseChildren(element, windowBounds, output, ref visited);
                return;
            }

            var type = info.ControlType;

            // Edit fields (text boxes, Notepad): read the whole field as one block.
            if (type == ControlType.Edit
                && element.TryGetCurrentPattern(TextPattern.Pattern, out var editPat)
                && editPat is TextPattern editText)
            {
                string t = ReadVisibleText(editText);
                if (IsReadable(t) && Intersects(bounds, windowBounds))
                    output.Add(new TextGroup(Clean(t), bounds));
                return;
            }

            // Documents (web pages, rich docs): if they expose structured Text children,
            // descend to collect paragraph/heading blocks instead of one giant block;
            // otherwise (no child text) take the whole visible text.
            if (type == ControlType.Document
                && element.TryGetCurrentPattern(TextPattern.Pattern, out var docPat)
                && docPat is TextPattern docText)
            {
                if (!HasTextDescendant(element))
                {
                    string t = ReadVisibleText(docText);
                    if (IsReadable(t) && Intersects(bounds, windowBounds))
                        output.Add(new TextGroup(Clean(t), bounds));
                    return;
                }
                // else: fall through and recurse to gather the paragraph-level Text blocks
            }

            // Plain text labels: the Name is the content.
            if (type == ControlType.Text)
            {
                string name = info.Name ?? string.Empty;
                if (IsReadable(name) && Intersects(bounds, windowBounds))
                {
                    output.Add(new TextGroup(Clean(name), bounds));
                    return; // a text leaf; no need to descend
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            return;
        }

        RecurseChildren(element, windowBounds, output, ref visited);
    }

    private void RecurseChildren(AutomationElement element, Rect windowBounds, List<TextGroup> output, ref int visited)
    {
        if (visited >= MaxElementsVisited || output.Count >= MaxBlocks)
            return;

        AutomationElement? child;
        try
        {
            child = TreeWalker.ControlViewWalker.GetFirstChild(element);
        }
        catch (ElementNotAvailableException)
        {
            return;
        }

        while (child is not null)
        {
            Collect(child, windowBounds, output, ref visited);
            if (visited >= MaxElementsVisited || output.Count >= MaxBlocks)
                return;
            try
            {
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
            catch (ElementNotAvailableException)
            {
                return;
            }
        }
    }

    private static bool HasTextDescendant(AutomationElement element)
    {
        try
        {
            var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            return element.FindFirst(TreeScope.Descendants, cond) is not null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string ReadVisibleText(TextPattern textPattern)
    {
        try
        {
            var ranges = textPattern.GetVisibleRanges();
            if (ranges.Length == 0)
                return textPattern.DocumentRange.GetText(MaxDocChars);

            var sb = new StringBuilder();
            foreach (var range in ranges)
            {
                sb.Append(range.GetText(MaxDocChars));
                sb.Append('\n');
                if (sb.Length > MaxDocChars)
                    break;
            }
            return sb.ToString();
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Maps grouping intensity (1–10) to a vertical-gap tolerance (in line-heights), centered
    /// so intensity 5 ≈ the original behavior. Higher intensity → smaller tolerance (tighter,
    /// less merging); lower → larger tolerance (merges across paragraph gaps).
    /// </summary>
    private static double GapFactorFor(int intensity)
    {
        int i = Math.Clamp(intensity, 1, 10);
        return 0.6 * Math.Pow(2.0, 5 - i); // i=5 → 0.6, i=10 → ~0.02, i=1 → ~9.6
    }

    /// <summary>Merges fragments that are vertically close and horizontally overlapping. The
    /// gap tolerance comes from the grouping intensity, so the user controls group size.</summary>
    private static List<TextGroup> MergeParagraphFragments(List<TextGroup> blocks, double gapFactor)
    {
        var ordered = blocks
            .OrderBy(b => b.Bounds.Top)
            .ThenBy(b => b.Bounds.Left)
            .ToList();

        var result = new List<TextGroup>();
        foreach (var block in ordered)
        {
            var prev = result.Count > 0 ? result[^1] : null;
            if (prev is not null && SameParagraph(prev.Bounds, block.Bounds, gapFactor))
            {
                var union = Rect.Union(prev.Bounds, block.Bounds);
                result[^1] = new TextGroup(prev.Text + " " + block.Text, union);
            }
            else
            {
                result.Add(block);
            }
        }
        return result;
    }

    private static bool SameParagraph(Rect a, Rect b, double gapFactor)
    {
        double lineHeight = Math.Min(a.Height, b.Height);
        double verticalGap = b.Top - a.Bottom;             // b is below a (ordered by Top)
        bool touching = verticalGap <= lineHeight * gapFactor && verticalGap >= -a.Height;
        double overlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        bool horizontallyAligned = overlap > Math.Min(a.Width, b.Width) * 0.4;
        return touching && horizontallyAligned;
    }

    private static bool Intersects(Rect a, Rect b)
    {
        a.Intersect(b);
        return !a.IsEmpty;
    }

    private static bool IsReadable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            return false;
        return text.Any(char.IsLetterOrDigit);
    }

    private static string Clean(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
