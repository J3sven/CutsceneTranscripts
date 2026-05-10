using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace CutsceneTranscripts;

public sealed unsafe partial class CutsceneTranscripts {
    private void OnTalkPostUpdate(AddonEvent eventType, AddonArgs args) {
        if (args.Addon.IsNull || !args.Addon.IsVisible) {
            talkWindowBounds = null;
            return;
        }

        var cutsceneActive = IsCutsceneActive();
        var addon = (AddonTalk*)args.Addon.Address;
        if (cutsceneActive)
            UpdateTalkWindowBounds(addon);

        if (!Configuration.Enabled || !cutsceneActive)
            return;

        CaptureTalkAddon(addon);
    }

    private void OnTalkFinalize(AddonEvent eventType, AddonArgs args) {
        lastObservedTalkKey = null;
        talkWindowBounds = null;
    }

    private void UpdateTalkWindowBounds(AddonTalk* addon) {
        var root = addon->RootNode;
        if (root == null) {
            talkWindowBounds = null;
            return;
        }

        var width = root->Width * root->ScaleX;
        var height = root->Height * root->ScaleY;
        if (width <= 0 || height <= 0) {
            talkWindowBounds = null;
            return;
        }

        talkWindowBounds = new TalkWindowBounds(new Vector2(root->ScreenX, root->ScreenY), new Vector2(width, height));
        lastTalkWindowBoundsAt = DateTimeOffset.Now;
    }

    private bool IsTalkWindowVisible() {
        return talkWindowBounds is not null
            && DateTimeOffset.Now - lastTalkWindowBoundsAt <= VisibleAddonGracePeriod;
    }

    private void CaptureTalkAddon(AddonTalk* addon) {
        if (addon == null)
            return;

        var texts = new List<string>();
        AddText(texts, addon->String268.AsDalamudSeString().TextValue);
        AddText(texts, addon->String2D0.AsDalamudSeString().TextValue);
        AddText(texts, addon->String338.AsDalamudSeString().TextValue);
        AddText(texts, addon->String408.AsDalamudSeString().TextValue);
        AddText(texts, addon->String470.AsDalamudSeString().TextValue);
        AddText(texts, addon->String4D8.AsDalamudSeString().TextValue);
        AddText(texts, addon->String540.AsDalamudSeString().TextValue);
        AddText(texts, ReadTextNode(addon->AtkTextNode220));
        AddText(texts, ReadTextNode(addon->AtkTextNode228));
        AddText(texts, ReadTextNode(addon->AtkTextNode238));
        AddText(texts, ReadTextNode(addon->AtkTextNode240));
        AddText(texts, ReadTextNode(addon->AtkTextNode248));

        if (texts.Count == 0)
            return;

        var talkKey = string.Join("\n", texts);
        if (string.Equals(talkKey, lastObservedTalkKey, StringComparison.Ordinal))
            return;

        lastObservedTalkKey = talkKey;
        AddTranscriptEntry(texts);
    }

    private void AddTranscriptEntry(List<string> texts) {
        var body = texts.OrderByDescending(text => text.Length).First();
        string? speaker = null;

        foreach (var candidate in texts) {
            if (TextEquivalent(candidate, body) || candidate.Length > 80 || candidate.Contains('\n'))
                continue;

            speaker = candidate;
            break;
        }

        if (string.IsNullOrWhiteSpace(speaker))
            speaker = lastDialogSpeaker;
        else
            lastDialogSpeaker = speaker;

        var entryKey = $"{speaker}\n{body}";
        if (string.Equals(entryKey, lastTranscriptEntryKey, StringComparison.Ordinal))
            return;

        lastTranscriptEntryKey = entryKey;
        voiceCaptureProbes.Clear();
        var voiceCandidates = ReadActiveSoundCandidates();
        var voiceClip = TryCaptureVoiceClip(voiceCandidates);
        entries.Add(new TranscriptEntry(nextTranscriptEntryId++, DateTimeOffset.Now, speaker, body, voiceClip));
        TrimEntries();
        MarkTranscriptChanged();
        StartVoiceCaptureProbe(entries.Count - 1, voiceCandidates);
    }

    private void AddChoiceEntry(string choiceText) {
        choiceText = CleanText(choiceText);
        if (string.IsNullOrWhiteSpace(choiceText))
            return;

        var playerName = GetPlayerName();
        var entryKey = $"{playerName}\n{choiceText}";
        if (string.Equals(entryKey, lastTranscriptEntryKey, StringComparison.Ordinal))
            return;

        lastTranscriptEntryKey = entryKey;
        voiceCaptureProbes.Clear();
        entries.Add(new TranscriptEntry(nextTranscriptEntryId++, DateTimeOffset.Now, playerName, choiceText, null));
        TrimEntries();
        MarkTranscriptChanged();
    }

    private string GetPlayerName() {
        var name = Services.ObjectTable.LocalPlayer?.Name.TextValue;
        return string.IsNullOrWhiteSpace(name)
            ? "Player"
            : name;
    }

    private void TrimEntries() {
        while (entries.Count > MaxTranscriptEntries)
            entries.RemoveAt(0);
    }

    internal void ClearTranscript() {
        entries.Clear();
        choiceStates.Clear();
        voiceCaptureProbes.Clear();
        speakerColors.Clear();
        lastObservedTalkKey = null;
        lastTranscriptEntryKey = null;
        lastDialogSpeaker = null;
        MarkTranscriptChanged();
    }

    internal Vector4 GetSpeakerColor(string speaker) {
        if (speakerColors.TryGetValue(speaker, out var color))
            return color;

        color = SpeakerColorPalette[speakerColors.Count % SpeakerColorPalette.Length];
        speakerColors[speaker] = color;
        return color;
    }
}
