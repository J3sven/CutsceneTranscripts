using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CutsceneTranscripts;

public sealed unsafe partial class CutsceneTranscripts {
    private static string ReadTextNode(AtkTextNode* node) {
        return node == null
            ? string.Empty
            : CleanText(node->NodeText.AsDalamudSeString().TextValue);
    }

    private static void AddText(List<string> texts, string? text) {
        text = CleanText(text);
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (texts.Any(existing => string.Equals(existing, text, StringComparison.Ordinal)))
            return;

        texts.Add(text);
    }

    private static string CleanText(string? text) {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return string.Join(
            "\n",
            text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    private static bool TextEquivalent(string left, string right) {
        return string.Equals(NormalizeForComparison(left), NormalizeForComparison(right), StringComparison.Ordinal);
    }

    private static string NormalizeForComparison(string text) {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    internal string BuildTranscriptText() {
        return string.Join(
            Environment.NewLine,
            entries.Select(entry => string.IsNullOrWhiteSpace(entry.Speaker)
                ? entry.Text
                : $"{entry.Speaker}: {entry.Text}"));
    }
}
