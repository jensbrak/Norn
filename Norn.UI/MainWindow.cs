using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Norn.Adapter;

namespace Norn.UI;

/// <summary>
/// The shell: a game-running banner, a top toolbar, a save-file sidebar (with
/// its own search/sort/backup-filter controls), a tab area built from
/// <see cref="TabModules.All"/>, and a status bar. Adding a tab touches
/// <see cref="TabModules"/>, never this file.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly ListBox _sidebar = new();
    private readonly SaveFileListControls _listControls = new();
    private readonly TabControl _tabs = new();
    private readonly StatusBar _statusBar = new();
    private readonly Toolbar _toolbar = new();
    private readonly GameRunningBanner _banner = new();
    private readonly List<SaveFileEntry> _saveEntries;
    private readonly bool _cliMode;
    private readonly SessionCache _sessions = new();
    private IReadOnlyList<SaveFileEntry> _visible = [];
    private string? _selectedPath;
    private bool _switchingProgrammatically;
    private bool _closeConfirmed;
    private StatisticsDto? _builtStatistics;

    public MainWindow(string? initialFilePath = null)
    {
        Title = AppInfo.Name;
        Icon = AppIcon.Default;
        // Widened/heightened from 1120x640: descriptions (TabRows.RowGroup's
        // fourth column) get more room before wrapping, and General - the
        // default first tab - fits without scrolling at this height.
        Width = 1366;
        Height = 820;

        _cliMode = initialFilePath is not null;
        List<string> rawFiles = initialFilePath is not null
            ? [initialFilePath]
            : SaveFileLocator.FindSaveFiles(SaveDirectoryShim.ResolveSaveDirectories()).ToList();
        _saveEntries = rawFiles.Select(SaveFileEntry.From).ToList();

        // supportsRecycling: false, deliberately (found in review, reproduced
        // against Avalonia 11.3.19). BuildSidebarRow snapshots the entry's
        // filename, dirty marker and tooltip into a plain TextBlock at
        // construction time — there are no bindings to re-evaluate. With
        // recycling on, Avalonia hands the already-built control back for a
        // different entry and those snapshotted values stay put, so a row
        // could display one save file's name while selecting another's.
        // Rebuilding per row costs nothing at this list's size (one row per
        // save file); binding the whole presentation instead would be the
        // alternative, for no gain here.
        //
        // The null guard is not defensive padding: Avalonia invokes an
        // item template's factory with a null item during some container
        // generation passes, and turning recycling off is exactly what
        // makes those passes reachable here. Without it, selecting any file
        // threw a first-chance NullReferenceException per generated row —
        // caught by the global handler, so the app carried on and the file
        // loaded correctly, but a debugger broke on every one and the
        // throws were pure waste. Found by running the app, which the test
        // suite cannot cover: no test exercises Avalonia's container
        // generation.
        _sidebar.ItemTemplate = new FuncDataTemplate<SaveFileEntry>(
            (entry, _) => entry is null
                ? new TextBlock()
                : BuildSidebarRow(entry, _sessions.TryGet(entry.Path, out var session) && session!.IsDirty),
            supportsRecycling: false);

        foreach (var module in TabModules.All)
        {
            _tabs.Items.Add(new TabItem { Header = module.Title });
        }

        _sidebar.SelectionChanged += async (_, _) => await OnSidebarSelectionChanged();
        _listControls.Changed += RefreshSidebarLabels;
        _toolbar.SaveRequested += SaveWithConfirmAsync;
        _toolbar.RevertRequested += async () => await RevertCurrent();
        _toolbar.SettingsRequested += async () => await SettingsWindow.Open(this);
        _toolbar.HelpRequested += async () => await HelpWindow.Open(this);
        _toolbar.AboutRequested += async () => await AboutWindow.Open(this);
        _toolbar.ExitRequested += Close;

        // Found by manually running the app, not by a test: switching files
        // was the only path that ever called Save(), so a single open file
        // had no way to be saved at all short of switching away and back.
        // Ctrl+S is the explicit save; Closing guards against losing an edit
        // by just closing the window, the same way switching files does.
        KeyDown += OnKeyDown;
        Closing += OnClosing;
        Activated += (_, _) => _banner.Recheck();
        // Opened, not fired inline here: WelcomeWindow.Open needs this
        // window already shown to be a valid ShowDialog owner, and Opened
        // is the framework's own signal for exactly that — fires once,
        // strictly after this constructor (and its own sidebar/empty-state
        // setup below) has fully run.
        Opened += async (_, _) => await ShowWelcomeIfDueAsync();

        var sidebarPanel = new DockPanel { Width = 240 };
        DockPanel.SetDock(_listControls, Dock.Top);
        sidebarPanel.Children.Add(_listControls);
        sidebarPanel.Children.Add(_sidebar);

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        Grid.SetColumn(sidebarPanel, 0);
        Grid.SetColumn(_tabs, 1);
        mainGrid.Children.Add(sidebarPanel);
        mainGrid.Children.Add(_tabs);

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        outerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        outerGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        outerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(_banner, 0);
        Grid.SetRow(_toolbar, 1);
        Grid.SetRow(mainGrid, 2);
        Grid.SetRow(_statusBar, 3);
        outerGrid.Children.Add(_banner);
        outerGrid.Children.Add(_toolbar);
        outerGrid.Children.Add(mainGrid);
        outerGrid.Children.Add(_statusBar);

        Content = outerGrid;

        RefreshSidebarLabels();
        _banner.Recheck();
        _ = StartWorldIdentityScanAsync();

        // No eager load: an unconditional first-file selection would surface
        // a corrupted or unsupported file as an error the instant the app
        // launches, before the user has chosen anything. CLI-arg mode is the
        // one case where "start already loaded" is the actual intent.
        if (initialFilePath is not null)
        {
            _sidebar.SelectedIndex = 0;
        }
        else
        {
            ShowEmptyState();
        }
    }

    /// <summary>
    /// Fire-and-forget from the constructor: scans every known world
    /// directory (local, plus Steam's own local Cloud mirror) for identity
    /// enrichment on a background
    /// thread so an unbounded number of <c>.fwl</c> files never blocks the
    /// window from showing. <c>await Task.Run</c> resumes back on the UI
    /// thread automatically (Avalonia's synchronization context), so both
    /// <see cref="StatusBar.ShowMessage"/> calls are safe as written — no
    /// manual dispatcher marshaling needed. Skipped entirely (quietly, no
    /// status message) when <see cref="Settings.ScanForLocalWorlds"/> is off
    /// — on by default, matching today's unconditional-scan behavior exactly
    /// until someone opts out.
    /// </summary>
    private async Task StartWorldIdentityScanAsync()
    {
        if (!SettingsStore.Current.ScanForLocalWorlds)
        {
            return;
        }

        _statusBar.ShowMessage("Reading world files metadata…");
        var found = await Task.Run(WorldIdentityShim.RefreshCatalog);
        _statusBar.ShowMessage(found == 1 ? "Found 1 world" : $"Found {found} worlds");

        // Push the newly-resolved identities into a session that was already
        // open when the scan finished (found in review) — otherwise its
        // Worlds tab keeps showing the unnamed/unseeded view it mapped
        // before the scan, and reselecting the file reuses that same cached
        // session, so the names never appear at all. Guaranteed to matter in
        // CLI-open mode, where a file is opened during construction.
        if (TryGetCurrentEditor(out var editor))
        {
            editor.RefreshWorldIdentities();
            RebuildTab<WorldsTabModule>(editor);
        }
    }

    /// <summary>Shows <see cref="WelcomeWindow"/> at most once per qualifying
    /// version bump (<see cref="WelcomePolicy"/>), then records the current
    /// version as seen — recording only happens if it was actually shown,
    /// not merely evaluated, so leaving <see cref="Settings.ShowWelcomePopup"/>
    /// off doesn't silently mark a version the user never actually saw.</summary>
    private async Task ShowWelcomeIfDueAsync()
    {
        if (!WelcomePolicy.ShouldShow(AppStateStore.Current.LastSeenVersion, AppInfo.Version, SettingsStore.Current.ShowWelcomePopup))
        {
            return;
        }

        await WelcomeWindow.Open(this);
        AppStateStore.Current.LastSeenVersion = AppInfo.Version;
        AppStateStore.Save();
    }

    /// <summary>
    /// Retrieves the currently-selected session's editor, if there is one
    /// open — the "is a file selected and its session open" check eight call
    /// sites in this class used to retype independently (found in review;
    /// that duplication is why <see cref="OnClosing"/> and
    /// <see cref="OnSidebarSelectionChanged"/> could each go stale
    /// independently — see their own comments below).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_selectedPath))]
    private bool TryGetCurrentEditor(out CharacterEditor editor)
    {
        if (_selectedPath is not null && _sessions.TryGet(_selectedPath, out var current) && current is not null)
        {
            editor = current;
            return true;
        }

        editor = null!;
        return false;
    }

    /// <summary>Built on <see cref="TryGetCurrentEditor"/>; see its comment.</summary>
    private bool CurrentIsDirty => TryGetCurrentEditor(out var editor) && editor.IsDirty;

    private async Task OnSidebarSelectionChanged()
    {
        if (_switchingProgrammatically)
        {
            return;
        }

        var newIndex = _sidebar.SelectedIndex;
        var newPath = newIndex >= 0 && newIndex < _visible.Count ? _visible[newIndex].Path : null;
        if (newPath == _selectedPath)
        {
            return;
        }

        void CancelSwitch()
        {
            _switchingProgrammatically = true;
            _sidebar.SelectedIndex = IndexOfVisible(_selectedPath);
            _switchingProgrammatically = false;
        }

        if (CurrentIsDirty)
        {
            var choice = await UnsavedChangesDialog.Ask(this, Path.GetFileName(_selectedPath!));
            if (choice == DirtyGuardChoice.Cancel)
            {
                CancelSwitch();
                return;
            }

            if (choice == DirtyGuardChoice.Save)
            {
                // Must check the result, not just await it: a cancelled
                // FileConflictDialog inside SaveCurrentAsync means nothing
                // was actually saved, but the switch below used to proceed
                // anyway, silently dropping the pending conflict the user
                // just asked to review (found in review).
                if (!await SaveCurrentAsync())
                {
                    CancelSwitch();
                    return;
                }
            }
            else if (!await RevertCurrent())
            {
                // Discard couldn't happen (the file no longer loads), so the
                // edits are still pending — stay put rather than switching
                // away and stranding them.
                CancelSwitch();
                return;
            }
        }

        _selectedPath = newPath;

        if (newPath is null)
        {
            ShowEmptyState();
        }
        else
        {
            LoadSelected(newPath);
        }

        // Re-sync the sidebar selection to the path we actually committed to
        // (found in review). Both dirty-guard branches above refresh the
        // sidebar while _selectedPath still names the *outgoing* file, which
        // snaps the highlighted row back to it; nothing afterwards moved the
        // highlight to the incoming file, so the editor showed one character
        // while the sidebar highlighted another — and re-clicking the
        // already-highlighted row raised no SelectionChanged at all.
        RefreshSidebarLabels();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || e.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        if (CurrentIsDirty)
        {
            await SaveWithConfirmAsync();
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        if (!CurrentIsDirty)
        {
            // No dirty-guard reason to stop the close — but Settings.ConfirmExit
            // (on by default) adds an extra opt-in safety net independent of
            // the dirty check above.
            if (!SettingsStore.Current.ConfirmExit)
            {
                return;
            }

            e.Cancel = true;
            if (!await ConfirmDialog.Ask(this, "Exit Norn", "Exit Norn?"))
            {
                return;
            }

            _closeConfirmed = true;
            Close();
            return;
        }

        e.Cancel = true;
        var choice = await UnsavedChangesDialog.Ask(this, Path.GetFileName(_selectedPath!));
        if (choice == DirtyGuardChoice.Cancel)
        {
            return;
        }

        if (choice == DirtyGuardChoice.Save)
        {
            // Must check the result: a cancelled FileConflictDialog inside
            // SaveCurrentAsync means the edits are still unsaved, but the
            // close below used to proceed anyway, permanently discarding
            // them (found in review).
            if (!await SaveCurrentAsync())
            {
                return;
            }
        }
        else if (!await RevertCurrent())
        {
            // Same as the file-switch guard: "discard my changes" didn't
            // happen, so closing would still lose them.
            return;
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>
    /// The toolbar's Save button and Ctrl+S both go through this, not
    /// <see cref="SaveCurrentAsync"/> directly — <see cref="Settings.ConfirmSave"/>
    /// gates only these two casual/direct paths. The dirty-guard dialogs
    /// (switching files, closing with unsaved changes) call
    /// <see cref="SaveCurrentAsync"/> directly instead: choosing "Save"
    /// there already *is* an explicit confirmation, so asking again would
    /// double-prompt on a decision just made.
    /// </summary>
    private async Task SaveWithConfirmAsync()
    {
        if (SettingsStore.Current.ConfirmSave && !await ConfirmDialog.Ask(this, "Save changes", "Save changes to disk?"))
        {
            return;
        }

        await SaveCurrentAsync();
    }

    /// <summary>
    /// Shared by <see cref="SaveWithConfirmAsync"/> and both dirty-guard
    /// dialogs — previously each call site duplicated this inline. Async
    /// (unlike the sibling <see cref="RevertCurrent"/>) because a changed
    /// player name may first offer a file rename via a modal dialog
    /// (<see cref="TryOfferFileRename"/>) before the actual write happens —
    /// and, since this is every save's one funnel, also the file-changed-
    /// on-disk conflict check below (see
    /// <see cref="CharacterEditor.HasChangedOnDisk"/> for why this
    /// check, not a running-process gate, is the actual safety mechanism).
    /// </summary>
    /// <returns>
    /// <c>true</c> once nothing is left pending — a completed save, or a
    /// conflict resolved by discarding (reverting). <c>false</c> only when
    /// the user backed out of the file-conflict dialog, leaving the edit
    /// still unsaved: callers that were about to close the window or switch
    /// files must check this rather than assume a call here always resolves
    /// the pending edit (found in review — see <see cref="OnClosing"/> and
    /// <see cref="OnSidebarSelectionChanged"/>, which used to not check it).
    /// </returns>
    private async Task<bool> SaveCurrentAsync()
    {
        if (!TryGetCurrentEditor(out var current))
        {
            return true;
        }

        // Returns null to continue, or a result to return immediately.
        // Extracted so it can run twice — see the second call below.
        async Task<bool?> ResolveDiskConflict()
        {
            if (!current.HasChangedOnDisk())
            {
                return null;
            }

            var conflictChoice = await FileConflictDialog.Ask(this, Path.GetFileName(current.Path));
            if (conflictChoice == DirtyGuardChoice.Cancel)
            {
                return false;
            }

            if (conflictChoice == DirtyGuardChoice.Discard)
            {
                // Same reload-and-rebuild shape as the plain Revert button —
                // reused rather than duplicated (RevertCurrent re-fetches
                // _selectedPath/current itself, redundant with what's
                // already in scope here, but harmlessly so). Its result is
                // propagated rather than assumed: this branch used to return
                // success unconditionally, so a reload that couldn't happen
                // still reported the conflict as resolved (found in review).
                return await RevertCurrent();
            }

            // DirtyGuardChoice.Save: proceed to overwrite, as the user just
            // explicitly chose — falls through to the ordinary save path
            // below rather than a separate write call, so a renamed-file
            // offer still applies exactly as it would otherwise.
            return null;
        }

        if (await ResolveDiskConflict() is { } earlyResult)
        {
            return earlyResult;
        }

        var renamedFrom = current.PlayerNameChangedThisSession
            ? await TryOfferFileRename(current)
            : null;

        // Checked again after the rename prompt (found in review). The
        // conflict check above is deliberately a narrow race — documented on
        // CharacterEditor.HasChangedOnDisk — but awaiting a modal between the
        // check and the write stretched that window across however long the
        // user left the rename dialog open, during which the game or a sync
        // client could rewrite the file. A rename preserves the file's
        // last-write time, so the check stays meaningful across it.
        if (renamedFrom is not null && await ResolveDiskConflict() is { } lateResult)
        {
            return lateResult;
        }

        // Re-point _selectedPath the moment the rename succeeds, not after
        // the write (found in review). TryOfferFileRename has already moved
        // the file and rekeyed _sessions by this point, so leaving
        // _selectedPath on the old key until after Save() meant a failed
        // write orphaned the session: TryGetCurrentEditor could no longer
        // find it, CurrentIsDirty went false despite pending edits, and
        // closing the window sailed straight past the unsaved-changes guard.
        if (renamedFrom is not null)
        {
            _selectedPath = current.Path;
        }

        try
        {
            current.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Expected write failures (file locked, permissions, disk full)
            // are reported and handled here rather than escaping to the
            // global crash handler. The session keeps its pending edits and
            // stays dirty, and the caller is told the save did not resolve —
            // so a close or file-switch in progress stops instead of
            // discarding them.
            await MessageDialog.Show(this, "Save failed",
                $"Couldn't save {Path.GetFileName(current.Path)}: {ex.Message}\n\nYour changes are still open and unsaved.");
            RefreshSidebarLabels();
            RefreshStatusBar();
            RefreshTitle();
            RefreshToolbar();
            return false;
        }

        _selectedPath = current.Path;

        // Refreshed after every successful save, not only after a rename
        // (found in review): SaveFileEntry carries the file's on-disk
        // LastModified, which an ordinary save changes too — leaving the
        // sidebar tooltip and the modified-time sort showing whatever the
        // values were at startup. Deferred until after Save() rather than
        // snapshotted the moment a rename happens, since the write that
        // follows the rename changes that timestamp again.
        var previousPath = renamedFrom ?? current.Path;
        var entryIndex = _saveEntries.FindIndex(entry => entry.Path == previousPath);
        if (entryIndex >= 0)
        {
            _saveEntries[entryIndex] = SaveFileEntry.From(current.Path);
        }

        // The profile's on-disk version fields are re-stamped by the write,
        // and CharacterEditor.Save now re-reads them — but the General tab
        // still holds controls built from the pre-save view, so it would go
        // on showing the old format version until a reload (found in
        // review).
        RebuildTab<GeneralTabModule>(current);

        RefreshSidebarLabels();
        RefreshStatusBar();
        RefreshTitle();
        RefreshToolbar();
        return true;
    }

    /// <summary>
    /// Offers to rename <paramref name="editor"/>'s file to match its
    /// (session-changed) player name, mirroring how a renamed save file's
    /// stem is presented in-game — the inverse
    /// direction, driven by an in-app name edit instead of a manual file
    /// rename. No-ops silently if the name now matches the file's stem
    /// again (e.g. typed away and back), or if the file is currently
    /// backup-shaped (<see cref="SaveFileClassifier.IsBackupFile"/>) — a
    /// backup's name diverging from the player name is structural, not
    /// something to "fix", and never offering it there
    /// keeps that always a deliberate, explicit action the user takes
    /// themselves, never a side effect Norn performs for them. Fires
    /// regardless of whether the file's name was already out of sync before
    /// this session touched anything — offering is still the right call even
    /// then, just worded so it doesn't presuppose the
    /// file was in sync a moment ago (see the dialog text below). Surfaces
    /// *why* automatically isn't possible via <see cref="MessageDialog"/>
    /// rather than silently skipping — an illegal character or a name
    /// collision is exactly the kind of thing a user would otherwise have to
    /// discover by trying it manually in Explorer/a file manager. Declining,
    /// an invalid name, or a collision all fall through to an ordinary
    /// content-only save; renaming is never required for the name edit
    /// itself to be saved.
    /// </summary>
    /// <returns>The file's path before the rename, or <c>null</c> if no
    /// rename happened (name unchanged, a backup, declined, invalid, a
    /// collision, or <see cref="Settings.OfferFileRename"/> is off) — the
    /// caller uses this to know whether <c>_saveEntries</c> needs its entry
    /// replaced.</returns>
    private async Task<string?> TryOfferFileRename(CharacterEditor editor)
    {
        if (!SettingsStore.Current.OfferFileRename)
        {
            return null;
        }

        var newStem = editor.View.Meta.PlayerName;
        var currentStem = Path.GetFileNameWithoutExtension(editor.Path);
        if (newStem == currentStem || SaveFileClassifier.IsBackupFile(Path.GetFileName(editor.Path)))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(editor.Path)!;
        var extension = Path.GetExtension(editor.Path);

        var validationError = SaveFileRenaming.ValidationError(newStem);
        if (validationError is not null)
        {
            await MessageDialog.Show(this, "Can't rename file",
                $"The file can't be renamed to match \"{newStem}\": {validationError}");
            return null;
        }

        if (!SaveFileRenaming.IsNameAvailable(directory, newStem, extension, Path.GetFileName(editor.Path)))
        {
            await MessageDialog.Show(this, "Can't rename file",
                $"A file named \"{newStem}{extension}\" already exists in the save directory.");
            return null;
        }

        // Deliberately not "Also rename..." — that phrasing presupposes the
        // file's name was in sync until this exact edit, which isn't always
        // true (a file can already have carried an unrelated name before
        // this session ever touched it). "Rename..." reads correctly either
        // way, without claiming a continuity that may not be there.
        var confirmed = await ConfirmDialog.Ask(this, "Rename file?",
            $"Rename the file to match the character's name?\n\n"
            + $"\"{Path.GetFileName(editor.Path)}\" → \"{newStem}{extension}\"");
        if (!confirmed)
        {
            return null;
        }

        var oldPath = editor.Path;
        var newPath = Path.Combine(directory, newStem + extension);
        try
        {
            editor.RenameFile(newPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await MessageDialog.Show(this, "Rename failed",
                $"Couldn't rename the file: {ex.Message}\n\nYour edits will still be saved under the original name.");
            return null;
        }

        _sessions.Rekey(oldPath, newPath);
        _statusBar.ShowMessage($"File renamed to {newStem}{extension}");
        return oldPath;
    }

    /// <summary>
    /// Shared by the toolbar's Revert button and both dirty-guard dialogs.
    /// Unlike <see cref="SaveCurrentAsync"/>, this also rebuilds the currently
    /// displayed tab content via <see cref="LoadSelected"/>: <c>Revert()</c>
    /// reloads the profile in place, so a stale edited value (e.g. a
    /// half-typed player name) would otherwise linger in its control. When
    /// called from a dialog branch mid-file-switch, that rebuild is
    /// immediately superseded by the switch's own <see cref="LoadSelected"/>
    /// call — a harmless redundant Build(), not a correctness issue.
    /// </summary>
    /// <summary>
    /// Returns whether the revert actually happened.
    /// <see cref="CharacterEditor.Revert"/> returns <c>false</c> when the
    /// file on disk no longer loads (deleted, or now outside the supported
    /// version range), deliberately leaving the session untouched so the
    /// caller can decide what to show — but this method used to discard that
    /// result and reload the same cached editor anyway, so a failed reload
    /// looked identical to a successful one while the pending edits quietly
    /// survived (found in review). Callers mid-close or mid-file-switch need
    /// the answer, since "discard my changes" not having happened is a
    /// reason to stop rather than proceed.
    /// </summary>
    private async Task<bool> RevertCurrent()
    {
        if (!TryGetCurrentEditor(out var current))
        {
            return true;
        }

        bool reverted;
        try
        {
            reverted = current.Revert();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reverted = false;
        }

        if (!reverted)
        {
            await MessageDialog.Show(this, "Couldn't reload",
                $"{Path.GetFileName(current.Path)} could no longer be read from disk, so your changes were kept "
                + "rather than discarded. The file may have been deleted, replaced, or saved by a newer version of Valheim.");
            return false;
        }

        RefreshSidebarLabels();
        LoadSelected(_selectedPath);
        return true;
    }

    /// <summary>
    /// Builds the whole replacement tab set before committing any of it to
    /// the window (found in review). This used to build and assign each tab
    /// in turn, with only the <em>open</em> wrapped in a try/catch — so a
    /// module that threw while building (a real case: two inventory items at
    /// the same grid position) left the tabs before it showing the
    /// newly-selected character and the tabs after it still showing the
    /// previous one, in the same window, with the global handler logging the
    /// exception and the app carrying on. Constructing first and swapping
    /// after means a build failure leaves the window exactly as it was,
    /// showing a load-failure message instead of a half-switched mix.
    /// </summary>
    private void LoadSelected(string path)
    {
        CharacterEditor? editor;
        string? error = null;
        IncompatibleVersion? incompatible = null;
        try
        {
            editor = _sessions.GetOrOpen(path, out incompatible);
        }
        catch (Exception ex)
        {
            editor = null;
            error = ex.Message;
        }

        var built = new Control[TabModules.All.Count];
        try
        {
            for (var i = 0; i < TabModules.All.Count; i++)
            {
                built[i] = editor is not null
                    ? TabModules.All[i].Build(editor, OnSessionEdited, _statusBar.ShowMessage)
                    : BuildFailureContent(DescribeLoadFailure(path, error, incompatible));
            }
        }
        catch (Exception ex)
        {
            // A module failed to build. Fall back to the same
            // failure-message content in every tab, so the window is
            // internally consistent rather than partly switched. A fresh
            // instance per tab, not one shared instance: a control has a
            // single parent in Avalonia, so reusing one would leave it
            // rendered in whichever tab was assigned last and blank in the
            // rest.
            editor = null;
            var message = $"Failed to display {Path.GetFileName(path)}: {ex.Message}";
            for (var i = 0; i < built.Length; i++)
            {
                built[i] = BuildFailureContent(message);
            }
        }

        for (var i = 0; i < built.Length; i++)
        {
            ((TabItem)_tabs.Items[i]!).Content = built[i];
        }

        _builtStatistics = editor?.View.Statistics;

        RefreshStatusBar();
        RefreshTitle();
        RefreshToolbar();
    }

    private static TextBlock BuildFailureContent(string message) => new()
    {
        Text = message,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(16),
    };

    /// <summary>
    /// The text shown in place of tab content when <see cref="LoadSelected"/>
    /// couldn't open a file. Three distinct cases, most to least specific:
    /// an out-of-range profile version (<paramref name="incompatible"/> tells
    /// us the exact version and which direction it missed by — almost always
    /// a Valheim update on the too-new side), some other exception during
    /// load (<paramref name="error"/>), or — reachable only if a future
    /// refusal path stops setting either — a flat fallback.
    /// </summary>
    private static string DescribeLoadFailure(string path, string? error, IncompatibleVersion? incompatible)
    {
        if (incompatible is { } version)
        {
            return version.TooNew
                ? $"This save is version {version.FoundVersion}, newer than any Valheim version this "
                  + $"build of Norn understands (up to {version.SupportedMax}). This is almost certainly "
                  + "a recent Valheim update — Norn will need updating to open it. Please don't file an "
                  + "issue for this specifically; check for a newer Norn release first."
                : $"This save is version {version.FoundVersion}, older than any version Norn supports "
                  + $"(from {version.SupportedMin} onward). It may be from a very old Valheim release, "
                  + "or the file may not be a Valheim character save at all.";
        }

        return error is null
            ? "This save is outside the supported version range."
            : $"Failed to open {Path.GetFileName(path)}: {error}";
    }

    private void ShowEmptyState()
    {
        // Two distinct empty states: zero files found at all needs to say
        // why (cloud-synced installs have nothing in the local-only
        // directory Norn scans — see SaveDirectoryShim), not just prompt for
        // a selection that isn't possible yet.
        var message = _saveEntries.Count == 0
            ? "No local save files found.\n\nNorn only supports local (non-cloud) saves. If your character is "
              + "cloud-synced, use Valheim's own character management to move it to local storage first."
            : "Select a save file to begin.";

        for (var i = 0; i < TabModules.All.Count; i++)
        {
            var tabItem = (TabItem)_tabs.Items[i]!;
            tabItem.Content = BuildEmptyStateContent(message);
        }

        _builtStatistics = null;

        RefreshStatusBar();
        RefreshTitle();
        RefreshToolbar();
    }

    private static Control BuildEmptyStateContent(string message)
    {
        var text = new TextBlock
        {
            Text = message,
            FontSize = 20,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420,
        };

        var grid = new Grid();
        grid.Children.Add(text);
        return grid;
    }

    private void OnSessionEdited()
    {
        RebuildStatisticsTabIfStale();
        RefreshSidebarLabels();
        RefreshStatusBar();
        RefreshTitle();
        RefreshToolbar();
    }

    /// <summary>
    /// Statistics is the one tab whose DTO can be changed by an edit made on
    /// a different tab (<see cref="CharacterEditor.ClearUsedCheats"/>, fired
    /// from General, also replaces <see cref="CharacterView.Statistics"/>).
    /// <c>onEdited</c> is deliberately chrome-only and never
    /// rebuilds tab content, which left the Statistics tab's "Cheats" counter
    /// showing a stale value until the file was reloaded — a known
    /// limitation recorded on <see cref="CharacterEditor.ClearUsedCheats"/>.
    /// Rebuilding on every <c>onEdited</c> unconditionally would reintroduce
    /// the exact per-keystroke rebuild cost the targeted update eliminated for
    /// player-name edits, so this only rebuilds when
    /// <see cref="CharacterView.Statistics"/> is a genuinely new reference —
    /// every other mutator's targeted <c>with</c> expression carries the same
    /// DTO instance forward untouched (verified via <c>Assert.Same</c> for
    /// the other five DTOs on a player-name edit).
    /// </summary>
    /// <summary>Rebuilds one tab in place, leaving every other tab's
    /// already-built controls alone — the targeted-rebuild shape
    /// <see cref="RebuildStatisticsTabIfStale"/> established, generalized
    /// once a second and third caller appeared (late world-identity
    /// enrichment, and post-save metadata refresh).</summary>
    private void RebuildTab<TModule>(CharacterEditor editor)
        where TModule : ITabModule
    {
        for (var i = 0; i < TabModules.All.Count; i++)
        {
            if (TabModules.All[i] is not TModule module)
            {
                continue;
            }

            ((TabItem)_tabs.Items[i]!).Content = module.Build(editor, OnSessionEdited, _statusBar.ShowMessage);
            break;
        }
    }

    private void RebuildStatisticsTabIfStale()
    {
        if (!TryGetCurrentEditor(out var editor))
        {
            return;
        }

        if (ReferenceEquals(editor.View.Statistics, _builtStatistics))
        {
            return;
        }

        for (var i = 0; i < TabModules.All.Count; i++)
        {
            if (TabModules.All[i] is not StatisticsTabModule module)
            {
                continue;
            }

            ((TabItem)_tabs.Items[i]!).Content = module.Build(editor, OnSessionEdited, _statusBar.ShowMessage);
            break;
        }

        _builtStatistics = editor.View.Statistics;
    }

    private static Control BuildSidebarRow(SaveFileEntry entry, bool dirty)
    {
        var text = new TextBlock { Text = entry.FileName + (dirty ? "*" : "") };

        // Always shown, not just when the name is clipped: the tooltip
        // carries information the row itself never displays at all (full
        // path, modified time, backup status), not just the overflow of a
        // name that's already visible — a truncation-conditional tooltip
        // was tried and was itself the reported issue, not a requirement.
        var tip = $"{entry.Path}\nModified: {entry.LastModified:g}";
        if (entry.IsBackup)
        {
            tip += "\nGame-generated backup";
        }

        ToolTip.SetTip(text, tip);

        return text;
    }

    private void RefreshSidebarLabels()
    {
        // CLI mode (a single explicitly-opened file) always shows that file
        // regardless of the backup filter — filtering makes no sense when
        // there is exactly one file and the user named it explicitly.
        var showBackups = _cliMode || _listControls.ShowBackups;
        _visible = SaveFileListView.Apply(_saveEntries, _listControls.SearchText, _listControls.SortKey, _listControls.Descending, showBackups);

        // Guard starts before touching ItemsSource, not after: reassigning it
        // implicitly deselects the ListBox (SelectedIndex briefly becomes -1),
        // which fires SelectionChanged on its own. Guarding only the explicit
        // SelectedIndex restore below left that implicit event unguarded,
        // which read as "switch to nothing" and fired the dirty-guard dialog
        // on every edit — found by actually running the app, not by a test.
        _switchingProgrammatically = true;
        _sidebar.ItemsSource = _visible;
        _sidebar.SelectedIndex = IndexOfVisible(_selectedPath);
        _switchingProgrammatically = false;
    }

    private int IndexOfVisible(string? path)
    {
        if (path is null)
        {
            return -1;
        }

        for (var i = 0; i < _visible.Count; i++)
        {
            if (_visible[i].Path == path)
            {
                return i;
            }
        }

        return -1;
    }

    private void RefreshStatusBar()
    {
        if (_selectedPath is null)
        {
            _statusBar.Show(null, false);
            return;
        }

        _statusBar.Show(_selectedPath, CurrentIsDirty);
    }

    private void RefreshToolbar()
    {
        _toolbar.SetDirty(CurrentIsDirty);
    }

    private void RefreshTitle()
    {
        if (TryGetCurrentEditor(out var editor))
        {
            var name = editor.View.Meta.PlayerName;
            Title = editor.IsDirty ? $"{name}* - {AppInfo.Name}" : $"{name} - {AppInfo.Name}";
        }
        else
        {
            Title = AppInfo.Name;
        }
    }
}
