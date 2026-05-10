using Dalamud.Configuration;

namespace CutsceneTranscripts.Classes;

internal sealed class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool ShowButtonDuringCutscenes { get; set; } = true;
    public bool KeepLastTranscriptAfterCutscene { get; set; } = true;
    public bool OpenTranscriptWhenCutsceneEnds { get; set; }

    public float ButtonX { get; set; } = 24f;
    public float ButtonY { get; set; } = 120f;
    public float WindowWidth { get; set; } = 520f;
    public float WindowHeight { get; set; } = 520f;

    public void Save()
        => Services.PluginInterface.SavePluginConfig(this);
}
