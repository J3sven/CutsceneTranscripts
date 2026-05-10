namespace CutsceneTranscripts.Classes;

internal sealed class VoiceCaptureProbe {
    public int EntryIndex { get; init; }

    public DateTimeOffset EndsAt { get; init; }

    public DateTimeOffset NextSampleAt { get; set; }

    public HashSet<string> StaleVoiceKeys { get; init; } = [];
}

