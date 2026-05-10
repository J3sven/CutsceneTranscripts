namespace CutsceneTranscripts.Classes;

internal sealed record VoiceClipRef(string Path, uint SoundNumber, bool CanReplay = true);

