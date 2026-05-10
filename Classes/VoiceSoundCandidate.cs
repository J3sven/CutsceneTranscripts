using FFXIVClientStructs.FFXIV.Client.Sound;

namespace CutsceneTranscripts.Classes;

internal readonly record struct VoiceSoundCandidate(
    string Path,
    uint SoundNumber,
    float Elapsed,
    float Volume,
    SoundVolumeCategory VolumeCategory,
    bool IsPlaying,
    bool IsLoading,
    bool IsPositional,
    bool IsAutoRelease,
    int MidiNote);

