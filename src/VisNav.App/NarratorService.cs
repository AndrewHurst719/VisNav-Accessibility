using System.Speech.Synthesis;

namespace VisNav.App;

/// <summary>
/// Text-to-speech via System.Speech (SAPI5): free, offline, in-box. Each <see cref="Speak"/>
/// cancels the current utterance first (so the most recent request always wins) and returns
/// its <see cref="Prompt"/>; <see cref="Completed"/> reports which prompt finished so callers
/// can tie a highlight to a specific utterance. Supports global pause/resume/stop.
/// </summary>
public sealed class NarratorService : IDisposable
{
    // SAPI clips the first word or two while the audio device warms up (worse at high rates).
    // Prepending a leading silence lets the warm-up eat the silence instead of the speech.
    // SAPI honors roughly half the requested break, so ~700ms yields ~420ms of real silence
    // (measured) — enough to cover device warm-up without a sluggish lead-in.
    private const int LeadingSilenceMs = 700;

    private readonly SpeechSynthesizer _synth = new();

    /// <summary>Raised (on the UI thread is NOT guaranteed) when an utterance finishes or is cancelled.</summary>
    public event Action<Prompt>? Completed;

    public NarratorService()
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.SpeakCompleted += (_, e) =>
        {
            if (e.Prompt is not null)
                Completed?.Invoke(e.Prompt);
        };
    }

    /// <summary>Names of installed, enabled voices for the settings dropdown.</summary>
    public IReadOnlyList<string> InstalledVoices()
    {
        try
        {
            return _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    public void Configure(string? voiceName, int rate)
    {
        _synth.Rate = Math.Clamp(rate, -10, 10);
        if (!string.IsNullOrWhiteSpace(voiceName))
        {
            try { _synth.SelectVoice(voiceName); }
            catch (ArgumentException) { /* voice no longer installed — keep current */ }
        }
    }

    /// <summary>Stops any current speech and reads the given text aloud. Returns the prompt.</summary>
    public Prompt? Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        Stop();

        // Leading silence avoids the first-word clip; AppendText also escapes the content.
        var builder = new PromptBuilder();
        builder.AppendBreak(TimeSpan.FromMilliseconds(LeadingSilenceMs));
        builder.AppendText(text);
        return _synth.SpeakAsync(builder);
    }

    /// <summary>Pauses if speaking, resumes if paused (a global play/pause toggle).</summary>
    public void TogglePause()
    {
        if (_synth.State == SynthesizerState.Speaking)
            _synth.Pause();
        else if (_synth.State == SynthesizerState.Paused)
            _synth.Resume();
    }

    public void Stop()
    {
        if (_synth.State == SynthesizerState.Paused)
            _synth.Resume(); // can't cancel while paused
        _synth.SpeakAsyncCancelAll();
    }

    public void Dispose()
    {
        try { Stop(); } catch { /* ignore */ }
        _synth.Dispose();
    }
}
