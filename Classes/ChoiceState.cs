namespace CutsceneTranscripts.Classes;

internal sealed class ChoiceState {
    public string AddonName { get; init; } = string.Empty;
    public List<string> Options { get; } = [];
    public int SelectedIndex { get; set; } = -1;
    public int ListItemIndex { get; set; } = -1;
    public int LastEventParam { get; set; } = -1;
    public bool LastEventParamMayBeChoiceIndex { get; set; } = true;
    public DateTimeOffset LastSeenAt { get; set; }

    public bool SubmitSeen { get; set; }

    public bool Recorded { get; set; }
}

