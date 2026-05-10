using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace CutsceneTranscripts;

public sealed unsafe partial class CutsceneTranscripts {
    private const float TranscriptButtonOutsideMargin = 0f;
    private const float TranscriptButtonTopOffset = 12f;

    private void UpdateTranscriptOpenButton(bool visible) {
        if (!visible) {
            transcriptOpenButton.SetButtonState(false, Vector2.Zero);
            return;
        }

        var bounds = talkWindowBounds;
        var currentBounds = bounds.GetValueOrDefault();
        var anchored = bounds is not null
            && DateTimeOffset.Now - lastTalkWindowBoundsAt <= TimeSpan.FromSeconds(1.5);
        var windowPos = anchored
            ? new Vector2(currentBounds.Position.X + currentBounds.Size.X + TranscriptButtonOutsideMargin, currentBounds.Position.Y + TranscriptButtonTopOffset)
            : new Vector2(Configuration.ButtonX, Configuration.ButtonY);

        transcriptOpenButton.SetButtonState(true, windowPos);
    }

    internal void DrawConfigWindowContents() {
        var changed = false;
        changed |= Checkbox("Enabled", Configuration.Enabled, value => Configuration.Enabled = value);
        changed |= Checkbox("Show transcript button during cutscenes", Configuration.ShowButtonDuringCutscenes, value => Configuration.ShowButtonDuringCutscenes = value);
        changed |= Checkbox("Keep last transcript after cutscene", Configuration.KeepLastTranscriptAfterCutscene, value => Configuration.KeepLastTranscriptAfterCutscene = value);
        changed |= Checkbox("Open transcript when cutscene ends", Configuration.OpenTranscriptWhenCutsceneEnds, value => Configuration.OpenTranscriptWhenCutsceneEnds = value);

        if (changed) {
            ClampConfiguration();
            TrimEntries();
            Configuration.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled($"Recorded lines: {entries.Count}");
        if (ImGui.Button("Clear Transcript"))
            ClearTranscript();
    }

    private static bool Checkbox(string label, bool value, Action<bool> setter) {
        if (!ImGui.Checkbox(label, ref value))
            return false;

        setter(value);
        return true;
    }
}
