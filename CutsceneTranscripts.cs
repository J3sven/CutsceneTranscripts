using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using KamiToolKit;

namespace CutsceneTranscripts;

public sealed unsafe partial class CutsceneTranscripts : IDalamudPlugin {
    private readonly WindowSystem windowSystem = new("CutsceneTranscripts");
    private readonly TranscriptWindow transcriptWindow;
    private readonly TranscriptOpenButtonAddon transcriptOpenButton;
    private readonly ConfigWindow configWindow;
    private readonly List<TranscriptEntry> entries = [];
    private readonly Dictionary<nint, ChoiceState> choiceStates = [];
    private readonly Dictionary<string, Vector4> speakerColors = [];
    private readonly List<VoiceCaptureProbe> voiceCaptureProbes = [];
    private bool lastCutsceneActive;
    private DateTimeOffset lastCutsceneActiveAt;
    private string? lastObservedTalkKey;
    private string? lastTranscriptEntryKey;
    private string? lastDialogSpeaker;
    private TalkWindowBounds? talkWindowBounds;
    private DateTimeOffset lastTalkWindowBoundsAt;
    private long nextTranscriptEntryId;
    private int transcriptRevision;
    private bool escapeWasPressed;

    internal Configuration Configuration { get; }

    internal IReadOnlyList<TranscriptEntry> Entries => entries;
    internal int TranscriptRevision => transcriptRevision;

    public CutsceneTranscripts(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();

        KamiToolKitLibrary.Initialize(pluginInterface, "CutsceneTranscripts");

        Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ClampConfiguration();

        transcriptWindow = new TranscriptWindow(this) {
            InternalName = "CutsceneTranscript",
            Title = "Cutscene Transcript",
            Size = new Vector2(Configuration.WindowWidth, Configuration.WindowHeight),
            ContentPadding = new Vector2(12f, 6f),
            DisableClose = true,
            DisableCloseTransition = true,
        };
        transcriptOpenButton = new TranscriptOpenButtonAddon(this) {
            InternalName = "CutsceneTranscriptButton",
            Title = "Cutscene Transcript",
            Size = TranscriptOpenButtonAddon.ButtonSize,
            ContentPadding = Vector2.Zero,
            OpenWindowSoundEffectId = 0,
            RememberClosePosition = false,
            RespectCloseAll = false,
            DisableCloseTransition = true,
            OpenInBounds = false,
            CreateWindowNode = TranscriptOpenButtonAddon.CreateInvisibleWindowNode,
        };
        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        Services.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = "Opens the current cutscene transcript. Use 'config' for settings or 'clear' to clear the transcript."
        });
        Services.CommandManager.AddHandler(ShortCommandName, new CommandInfo(OnCommand) {
            HelpMessage = "Opens the current cutscene transcript."
        });

        Services.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", OnTalkPostUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Talk", OnTalkPostUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Talk", OnTalkFinalize);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, ChoiceAddonNames, OnChoicePostUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, ChoiceAddonNames, OnChoicePostUpdate);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, ChoiceAddonNames, OnChoiceReceiveEvent);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, ChoiceAddonNames, OnChoiceReceiveEvent);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, ChoiceAddonNames, OnChoiceFinalize);

        Services.PluginInterface.UiBuilder.Draw += Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi += OpenTranscriptWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi += OpenConfigWindow;
    }

    public void Dispose() {
        Services.PluginInterface.UiBuilder.Draw -= Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi -= OpenTranscriptWindow;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfigWindow;
        transcriptOpenButton.Dispose();
        transcriptWindow.Dispose();
        windowSystem.RemoveAllWindows();
        KamiToolKitLibrary.Dispose();
        Services.AddonLifecycle.UnregisterListener(OnTalkPostUpdate, OnTalkFinalize, OnChoicePostUpdate, OnChoiceReceiveEvent, OnChoiceFinalize);
        Services.CommandManager.RemoveHandler(CommandName);
        Services.CommandManager.RemoveHandler(ShortCommandName);
    }

    private void OnCommand(string command, string args) {
        switch (args.Trim().ToLowerInvariant()) {
            case "on":
                Configuration.Enabled = true;
                Configuration.Save();
                break;
            case "off":
                Configuration.Enabled = false;
                Configuration.Save();
                break;
            case "clear":
                ClearTranscript();
                break;
            case "config":
            case "settings":
                OpenConfigWindow();
                break;
            default:
                ToggleTranscriptWindow();
                break;
        }
    }

    internal void ToggleTranscriptWindow() {
        if (transcriptWindow.IsShown)
            transcriptWindow.RequestSoftHide();
        else
            transcriptWindow.RequestOpen();
    }

    private void OpenTranscriptWindow() {
        transcriptWindow.RequestOpen();
    }

    private void OpenConfigWindow() {
        if (!IsCutsceneActive())
            configWindow.IsOpen = true;
    }

    private void Draw() {
        var cutsceneActive = IsCutsceneActive();
        if (!lastCutsceneActive && cutsceneActive) {
            ClearTranscript();
        }
        else if (lastCutsceneActive && !cutsceneActive) {
            if (Configuration.OpenTranscriptWhenCutsceneEnds && entries.Count > 0) {
                transcriptWindow.RequestOpen();
            }
            else {
                if (!Configuration.OpenTranscriptWhenCutsceneEnds) {
                    transcriptWindow.RequestSoftHide();
                }

                if (!Configuration.KeepLastTranscriptAfterCutscene)
                    ClearTranscript();
            }
        }

        if (cutsceneActive)
            lastCutsceneActiveAt = DateTimeOffset.Now;

        lastCutsceneActive = cutsceneActive;

        var showTranscriptOpenButton = Configuration.Enabled
            && Configuration.ShowButtonDuringCutscenes
            && cutsceneActive
            && entries.Count > 0
            && IsTalkWindowVisible()
            && !IsChoiceAddonVisible();
        UpdateTranscriptOpenButton(showTranscriptOpenButton);

        ProcessVoiceCaptureProbes();
        RefreshActiveVoiceReplayState();
        HandleTranscriptEscapeClose();
        transcriptWindow.RefreshIfNeeded();
        windowSystem.Draw();
    }

    private void HandleTranscriptEscapeClose() {
        var escapePressed = Services.KeyState.IsVirtualKeyValid(VirtualKey.ESCAPE) && Services.KeyState[(int)VirtualKey.ESCAPE];
        if (escapePressed && !escapeWasPressed && transcriptWindow.IsShown) {
            transcriptWindow.RequestClose();
            Services.KeyState[(int)VirtualKey.ESCAPE] = false;
            escapePressed = false;
        }

        escapeWasPressed = escapePressed;
    }

    internal bool IsCutsceneActive() {
        return Services.PluginInterface.UiBuilder.CutsceneActive
            || Services.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            || Services.Condition[ConditionFlag.WatchingCutscene]
            || Services.Condition[ConditionFlag.WatchingCutscene78];
    }

    private void ClampConfiguration() {
        Configuration.ButtonX = Math.Clamp(Configuration.ButtonX, 0f, 7680f);
        Configuration.ButtonY = Math.Clamp(Configuration.ButtonY, 0f, 4320f);
        Configuration.WindowWidth = Math.Clamp(Configuration.WindowWidth, 320f, 1200f);
        Configuration.WindowHeight = Math.Clamp(Configuration.WindowHeight, 240f, 900f);
    }

    private void MarkTranscriptChanged() {
        transcriptRevision++;
        transcriptWindow.MarkDirty();
    }
}
