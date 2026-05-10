namespace CutsceneTranscripts.Classes;

internal sealed record TranscriptEntry(long Id, DateTimeOffset Timestamp, string? Speaker, string Text, VoiceClipRef? VoiceClip);

