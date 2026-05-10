using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CutsceneTranscripts.NativeElements.Addons;

internal sealed class ConfigWindow(CutsceneTranscripts plugin) : Window("Cutscene Transcript Settings", ImGuiWindowFlags.AlwaysAutoResize, true) {
    public override bool DrawConditions()
        => !plugin.IsCutsceneActive();

    public override void Draw()
        => plugin.DrawConfigWindowContents();
}

