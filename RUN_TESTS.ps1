$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorJournalCoreTests.exe"
& $csc /nologo /target:exe /out:$out `
    (Join-Path $ScriptRoot "src\JournalModels.cs") `
    (Join-Path $ScriptRoot "src\JournalStore.cs") `
    (Join-Path $ScriptRoot "tests\JournalCoreTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal core tests did not compile." }
& $out
if ($LASTEXITCODE -ne 0) { throw "Journal core tests failed." }

# Structured Chronicle API queue behavior. A tiny local plugin-shaped test double lets this stay
# Unity/Lunaris-free while preserving the production public API source unchanged.
$apiOut = Join-Path $env:TEMP "ErenshorJournal.ApiTests.exe"
& $csc /nologo /target:exe /out:$apiOut `
    (Join-Path $ScriptRoot "src\JournalModels.cs") `
    (Join-Path $ScriptRoot "src\JournalApi.cs") `
    (Join-Path $ScriptRoot "tests\JournalApiTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal API tests did not compile." }
& $apiOut
if ($LASTEXITCODE -ne 0) { throw "Journal API tests failed." }

# Pure significance policy: first observation is baseline, raw XP changes cannot enter because the
# tracker accepts levels only, and only a level increase becomes one structured milestone.
$progressOut = Join-Path $env:TEMP "ErenshorJournal.ProgressionPolicyTests.exe"
& $csc /nologo /target:exe /out:$progressOut `
    (Join-Path $ScriptRoot "src\JournalProgressionPolicy.cs") `
    (Join-Path $ScriptRoot "tests\JournalProgressionPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal progression policy tests did not compile." }
& $progressOut
if ($LASTEXITCODE -ne 0) { throw "Journal progression policy tests failed." }

# Compile the reflection-only optional bridge WITHOUT Crafting Expanded. The test proves absence of
# the sibling is a normal no-op rather than a load/runtime requirement.
$optionalProgressOut = Join-Path $env:TEMP "ErenshorJournal.OptionalProgressionBridgeTests.exe"
& $csc /nologo /target:exe /out:$optionalProgressOut `
    (Join-Path $ScriptRoot "src\JournalProgressionPolicy.cs") `
    (Join-Path $ScriptRoot "src\OptionalProgressionBridge.cs") `
    (Join-Path $ScriptRoot "tests\OptionalProgressionBridgeTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Optional progression bridge tests did not compile." }
& $optionalProgressOut
if ($LASTEXITCODE -ne 0) { throw "Optional progression bridge tests failed." }

# Pure decision logic behind the GameData.PlayerTyping ownership handling - no UnityEngine or
# game assembly dependency, so this stays testable outside the game. See src/JournalTypingPolicy.cs.
$typingOut = Join-Path $env:TEMP "ErenshorJournalTypingPolicyTests.exe"
& $csc /nologo /target:exe /out:$typingOut `
    (Join-Path $ScriptRoot "src\JournalTypingPolicy.cs") `
    (Join-Path $ScriptRoot "tests\JournalTypingPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal typing policy tests did not compile." }
& $typingOut
if ($LASTEXITCODE -ne 0) { throw "Journal typing policy tests failed." }

# Pure per-character key composition - no UnityEngine or game assembly dependency. See
# src/JournalCharacterKey.cs (live GameData resolution lives in JournalCharacterIdentity.cs, not
# exercised here).
$characterKeyOut = Join-Path $env:TEMP "ErenshorJournalCharacterKeyTests.exe"
& $csc /nologo /target:exe /out:$characterKeyOut `
    (Join-Path $ScriptRoot "src\JournalCharacterKey.cs") `
    (Join-Path $ScriptRoot "tests\JournalCharacterKeyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal character key tests did not compile." }
& $characterKeyOut
if ($LASTEXITCODE -ne 0) { throw "Journal character key tests failed." }

# Legacy-data "first character claims it once" migration policy - file-based, no UnityEngine or
# game assembly dependency. See src/JournalLegacyMigration.cs.
$legacyOut = Join-Path $env:TEMP "ErenshorJournalLegacyMigrationTests.exe"
& $csc /nologo /target:exe /out:$legacyOut `
    (Join-Path $ScriptRoot "src\JournalLegacyMigration.cs") `
    (Join-Path $ScriptRoot "tests\JournalLegacyMigrationTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal legacy migration tests did not compile." }
& $legacyOut
if ($LASTEXITCODE -ne 0) { throw "Journal legacy migration tests failed." }

# Unity-free retained-UI visibility/fallback, action routing, strict bool mutation parsing, gesture cleanup, and normalized-position recovery policy.
$suiteUiOut = Join-Path $env:TEMP "ErenshorJournal.SuiteUiPolicyTests.exe"
& $csc /nologo /target:exe /out:$suiteUiOut `
    (Join-Path $ScriptRoot "src\SuiteUiPolicies.cs") `
    (Join-Path $ScriptRoot "tests\SuiteUiPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests did not compile." }
& $suiteUiOut
if ($LASTEXITCODE -ne 0) { throw "Suite UI policy tests failed." }

# Pure fast-entry formatting: clean timestamp markers and separator behavior without Unity.
$entryOut = Join-Path $env:TEMP "ErenshorJournal.EntryPolicyTests.exe"
& $csc /nologo /target:exe /out:$entryOut `
    (Join-Path $ScriptRoot "src\JournalEntryPolicy.cs") `
    (Join-Path $ScriptRoot "tests\JournalEntryPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal entry policy tests did not compile." }
& $entryOut
if ($LASTEXITCODE -ne 0) { throw "Journal entry policy tests failed." }

# Pure toolbar slot geometry: guards the retained Journal action row against Copy/Delete overlap.
$layoutOut = Join-Path $env:TEMP "ErenshorJournal.UiLayoutPolicyTests.exe"
& $csc /nologo /target:exe /out:$layoutOut `
    (Join-Path $ScriptRoot "src\JournalUiLayoutPolicy.cs") `
    (Join-Path $ScriptRoot "tests\JournalUiLayoutPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal UI layout policy tests did not compile." }
& $layoutOut
if ($LASTEXITCODE -ne 0) { throw "Journal UI layout policy tests failed." }

# Privacy/authority source guard: normal runtime logging must not interpolate character keys or
# exception messages that can contain local filesystem paths, and Journal stays network-free.
$journalPluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorJournalPlugin.cs") -Raw
$journalAllSource = (Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
if ($journalPluginSource -match 'Logging\.Log\w+\([^\r\n]*\+\s*key\b') { throw "Journal privacy guard failed: character key is included in a log call." }
if ($journalPluginSource -match 'Logging\.Log\w+\([^\r\n]*ex\.Message') { throw "Journal privacy guard failed: exception message may expose local paths." }
if ($journalAllSource -match 'LunarisPermission\.Network') { throw "Journal privacy guard failed: Journal requests network permission." }
if ($journalPluginSource -notmatch 'Instance\s*!=\s*null\s*&&\s*Instance\s*!=\s*this') { throw "Journal lifecycle guard failed: duplicate plugin initialization is not rejected." }
$readyTransitionMatch = [regex]::Match($journalPluginSource, 'private\s+bool\s+RefreshReadyState\(\)[\s\S]*?(?=private\s+void\s+EnsureCharacterContext)')
if (-not $readyTransitionMatch.Success -or $readyTransitionMatch.Value -notmatch 'else\s+if\s*\(_dirty\)\s*SaveNow\(\)') { throw "Journal lifecycle guard failed: dirty state is not saved on character unload when the panel is closed." }
Write-Host "PASS: Journal privacy/network source guard"

# Deep playable-state source guards: the local control surface must not expose character/tab
# identity, integration work is frame-bounded, and durable/backup recovery paths remain present.
$journalControlSource = Get-Content (Join-Path $ScriptRoot "src\JournalControlApi.cs") -Raw
$journalStoreSource = Get-Content (Join-Path $ScriptRoot "src\JournalStore.cs") -Raw
if ($journalControlSource -match 'state\.CharacterKey\s*=') { throw "Journal privacy guard failed: control state exposes character key." }
if ($journalControlSource -match 'state\.SelectedTabName\s*=') { throw "Journal privacy guard failed: control state exposes tab name." }
if ($journalPluginSource -notmatch 'MaximumChronicleIntegrationsPerFrame\s*=\s*32') { throw "Journal integration guard failed: per-frame Chronicle admission is not bounded." }
$legacyMigrationSource = Get-Content (Join-Path $ScriptRoot "src\JournalLegacyMigration.cs") -Raw
if ($legacyMigrationSource -notmatch 'FileMode\.CreateNew' -or $legacyMigrationSource -notmatch 'stream\.Flush\(true\)') { throw "Journal migration guard failed: legacy claim is not acquired durably before character copy." }
$claimAt = $legacyMigrationSource.IndexOf('FileMode.CreateNew')
$copyAt = $legacyMigrationSource.IndexOf('File.Copy(legacyPath, characterPath')
if ($claimAt -lt 0 -or $copyAt -lt 0 -or $claimAt -ge $copyAt) { throw "Journal migration guard failed: private legacy notes are copied before the one-time claim is acquired." }
if ($journalStoreSource -notmatch 'stream\.Flush\(true\)') { throw "Journal persistence guard failed: temporary file is not durably flushed before replacement." }
if ($journalStoreSource -notmatch 'TryLoadFile\(temp') { throw "Journal persistence guard failed: readable interrupted-save temporary files are not recoverable." }
if ($journalStoreSource -notmatch 'IsAtLeastAsNew\(temp, _path\)') { throw "Journal persistence guard failed: newer complete temp cannot recover a pre-replace crash over an older readable main." }
if ($journalStoreSource -notmatch 'TryLoadNewestRecovery' -or $journalStoreSource -notmatch 'IsAtLeastAsNew\(temp, backup\)') { throw "Journal persistence guard failed: recovery does not choose the newest validated backup/temp candidate." }
if ($journalStoreSource -notmatch 'File\.Replace\(temp, _path, backup, true\)') { throw "Journal persistence guard failed: atomic replace/backup path missing." }
# Automated-history routing guard: the timestamp helper remains manual-only, structured Chronicle
# APIs never mutate Tabs/Text, and optional progression reads LEVEL state only (never XP ticks).
$journalApiSource = Get-Content (Join-Path $ScriptRoot "src\JournalApi.cs") -Raw
$journalWindowSource = Get-Content (Join-Path $ScriptRoot "src\JournalWindow.cs") -Raw
$progressPolicySource = Get-Content (Join-Path $ScriptRoot "src\JournalProgressionPolicy.cs") -Raw
$optionalProgressSource = Get-Content (Join-Path $ScriptRoot "src\OptionalProgressionBridge.cs") -Raw
$retainedUiSource = Get-Content (Join-Path $ScriptRoot "src\RetainedUiKit.cs") -Raw
$modelsSource = Get-Content (Join-Path $ScriptRoot "src\JournalModels.cs") -Raw
if ($journalApiSource -match '\.Tabs\s*\[' -or $journalApiSource -match 'JournalTab') { throw "Journal routing guard failed: public Chronicle API can reach manual note/tab state." }
if ($optionalProgressSource -match '\.Tabs\s*\[' -or $optionalProgressSource -match 'AppendTimestampMarker') { throw "Journal routing guard failed: optional progression can reach manual note text." }
$timestampReferences = [regex]::Matches($journalAllSource, 'AppendTimestampMarker').Count
if ($timestampReferences -ne 2) { throw "Journal routing guard failed: timestamp helper is referenced outside its declaration and the manual JournalWindow action." }
if ($journalWindowSource -notmatch 'AddFixedButton\(row,\s*"NewEntry",\s*"Add Time"') { throw "Journal workflow guard failed: ambiguous manual New Entry label returned." }
if ($progressPolicySource -match 'CurrentXp|SmithingXp|XpAward') { throw "Journal significance guard failed: progression policy depends on raw XP state." }
if ($optionalProgressSource -match 'CurrentXp|SmithingXp|XpAward') { throw "Journal significance guard failed: optional progression observer reads raw XP." }
if ($modelsSource -notmatch 'AppendChronicleEvent' -or $modelsSource -notmatch 'EventId') { throw "Journal Chronicle guard failed: structured stable-id admission is missing." }
if ($journalApiSource -notmatch 'public const int ContractVersion\s*=\s*1' -or $journalApiSource -notmatch 'public const int EventContractVersion\s*=\s*2') { throw "Journal compatibility guard failed: legacy/v2 API version surfaces changed unexpectedly." }

# Journal-owned pointer containment must claim native UI-drag ownership on pointer-down, before the
# EventSystem drag threshold, and release it on pointer-up/disable/destroy paths.
$dragClass = [regex]::Match($retainedUiSource, 'internal sealed class SuiteDragHandler[\s\S]*?(?=internal sealed class SuiteResizeHandler)')
$resizeClass = [regex]::Match($retainedUiSource, 'internal sealed class SuiteResizeHandler[\s\S]*?(?=internal sealed class|\z)')
if (-not $dragClass.Success -or $dragClass.Value -notmatch 'OnPointerDown[\s\S]*?_gesture\.Press\(\)[\s\S]*?ClaimOwnership\(\)') { throw "Journal input guard failed: window/launcher drag does not claim ownership on pointer-down." }
if ($dragClass.Value -notmatch 'OnPointerUp[\s\S]*?EndDrag' -or $dragClass.Value -notmatch 'OnDisable[\s\S]*?EndDrag' -or $dragClass.Value -notmatch 'OnDestroy[\s\S]*?EndDrag' -or $dragClass.Value -notmatch 'private\s+void\s+EndDrag[\s\S]*?Release\(\)') { throw "Journal input guard failed: drag ownership cleanup path is incomplete." }
if (-not $resizeClass.Success -or $resizeClass.Value -notmatch 'OnPointerDown[\s\S]*?_gesture\.Press\(\)[\s\S]*?ClaimOwnership\(\)' -or $resizeClass.Value -notmatch 'OnPointerUp[\s\S]*?EndResize' -or $resizeClass.Value -notmatch 'OnDisable[\s\S]*?EndResize' -or $resizeClass.Value -notmatch 'OnDestroy[\s\S]*?EndResize') { throw "Journal input guard failed: resize ownership claim/cleanup path is incomplete." }
if ($journalPluginSource -notmatch 'CloseJournal\(\)[\s\S]*?UpdatePlayerTyping\(false\)') { throw "Journal typing guard failed: closing the panel does not release Journal typing ownership immediately." }
Write-Host "PASS: Journal deep persistence/privacy/history/input source guard"

# RC camera/gesture source contract. Runtime IL proof still executes fail-closed at Harmony prepare
# time; these checks prevent regressions back to global-flag-only or non-left gestures.
$cameraSource = Get-Content (Join-Path $ScriptRoot "src\JournalCameraUiPatch.cs") -Raw
$ownershipSource = Get-Content (Join-Path $ScriptRoot "src\JournalUiGestureOwnership.cs") -Raw
$retainedSource = Get-Content (Join-Path $ScriptRoot "src\RetainedUiKit.cs") -Raw
$pluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorJournalPlugin.cs") -Raw
if ($cameraSource -notmatch '\[HarmonyPatch\(typeof\(CameraController\),\s*"UsingUI"\)\]' -or
    $cameraSource -notmatch '\[HarmonyPrepare\]' -or $cameraSource -notmatch 'if\s*\(!__result\s*&&\s*JournalUiGestureOwnership\.OwnsPointerGesture\)') {
    throw "Journal camera guard failed: fail-closed monotonic UsingUI postfix missing."
}
foreach ($token in @('UIWindows','activeSelf','ModernControls','releaseMouse','GetAxis','DraggingUIElement')) {
    if ($cameraSource -notmatch [regex]::Escape($token)) { throw "Journal camera guard failed: native proof token missing: $token" }
}
if ($retainedSource -notmatch 'InputButton\.Left' -or $retainedSource -notmatch 'Input\.GetMouseButton\(0\)' -or
    $retainedSource -notmatch 'OnApplicationFocus' -or $retainedSource -notmatch 'OnApplicationPause') {
    throw "Journal gesture guard failed: left-only physical/focus/pause lifecycle missing."
}
if ($ownershipSource -notmatch 'ProcessOwnersKey' -or $ownershipSource -notmatch 'RestoreBaseline' -or
    $ownershipSource -match 'DraggingUIElement\s*=\s*false') {
    throw "Journal ownership guard failed: shared baseline restoration regressed."
}
if ($pluginSource -notmatch 'PluginVersion\s*=\s*"0\.1\.11"' -or $pluginSource -notmatch '_harmony\.PatchAll\(\)' -or
    $pluginSource -notmatch '_harmony\.UnpatchSelf\(\)') {
    throw "Journal camera lifecycle/version guard failed."
}
Write-Host "Journal RC camera/gesture source guards: PASS" -ForegroundColor Green

# Release polish source contract: header collapse uses the owned Image-chevron, never a TMP
# triangle glyph. Existing SuiteWindowChromePolicy tests cover the immediate body/height restore.
if ($journalWindowSource -notmatch 'AddVerticalChevron\(_collapseChevron,\s*!_collapsed\)' -or
    $retainedUiSource -notmatch 'internal\s+static\s+void\s+AddVerticalChevron') {
    throw "Journal release polish guard failed: glyph-safe collapse chevron is missing."
}
Write-Host "Journal release polish collapse-icon guard: PASS" -ForegroundColor Green
$launcherVisual = Get-Content (Join-Path $ScriptRoot "src\StandaloneLauncherVisual.cs") -Raw
$launcherSource = Get-Content (Join-Path $ScriptRoot "src\JournalLauncher.cs") -Raw
if ($launcherVisual -notmatch 'Width\s*=\s*154f' -or $launcherVisual -notmatch 'Height\s*=\s*32f' -or
    $launcherVisual -notmatch 'GripWidth\s*=\s*20f' -or $launcherVisual -notmatch '"GripDot"' -or
    $launcherSource -notmatch 'StyleGrip\(grip\)' -or $launcherSource -notmatch 'StyleButton\(button, _label\)') {
    throw "Journal Forgotten Roads launcher visual contract failed."
}
Write-Host "Journal Forgotten Roads launcher visual contract: PASS" -ForegroundColor Green

# Regression guard for the 0.1.9 launcher-grip drag defect: the grip must use FIXED anchors
# (AnchorBottomLeft), not a vertically stretched anchor. StyleGrip sets grip.sizeDelta to an
# ABSOLUTE (GripWidth, Height) size; on a stretched Y anchor that same assignment becomes an
# ADDITIVE offset on top of the parent-matched height, which previously doubled the grip's real
# height and, with pivot.y = 0, pushed the extra height entirely above the visible launcher.
if ($launcherSource -notmatch 'AnchorBottomLeft\(grip,\s*0f,\s*0f,\s*StandaloneLauncherVisual\.GripWidth,\s*Height\)') {
    throw "Journal launcher grip guard failed: grip is not built with AnchorBottomLeft's fixed anchors."
}
if ($launcherSource -match 'grip\.anchorMax\s*=\s*new Vector2\(0f,\s*1f\)') {
    throw "Journal launcher grip guard failed: grip reintroduces a vertically stretched anchor."
}
Write-Host "Journal launcher grip anchor regression guard: PASS" -ForegroundColor Green

# Shared right-side standalone-launcher column policy: pure geometry/slot math, no UnityEngine
# dependency. See src/StandaloneLauncherColumnPolicy.cs.
$columnOut = Join-Path $env:TEMP "ErenshorJournal.StandaloneLauncherColumnPolicyTests.exe"
& $csc /nologo /target:exe /out:$columnOut `
    (Join-Path $ScriptRoot "src\StandaloneLauncherColumnPolicy.cs") `
    (Join-Path $ScriptRoot "tests\StandaloneLauncherColumnPolicyTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Journal standalone launcher column policy tests did not compile." }
& $columnOut
if ($LASTEXITCODE -ne 0) { throw "Journal standalone launcher column policy tests failed." }

# Right-side default placement source contract: JournalLauncher must derive its default position
# from the shared column policy, not a hardcoded constant (the pre-existing 0.86/0.82 lower-right
# default this task replaces with a deterministic per-module column slot).
if ($launcherSource -notmatch 'StandaloneLauncherColumnPolicy\.DefaultX\(\)' -or
    $launcherSource -notmatch 'StandaloneLauncherColumnPolicy\.DefaultY\(StandaloneLauncherColumnPolicy\.SlotIndex\)') {
    throw "Journal launcher placement guard failed: launcher default is not derived from StandaloneLauncherColumnPolicy."
}
if ($launcherSource -match 'new RetainedPosition\([^,]+,[^,]+,\s*0\.86f,\s*0\.82f') {
    throw "Journal launcher placement guard failed: old hardcoded lower-right default constant is still present."
}
Write-Host "Journal right-side launcher placement source guard: PASS" -ForegroundColor Green

# UI workspace normalization pass: launcher label stays stable regardless of open state; open/
# active is a structural cue (accent bar), never a text suffix; default window position comes from
# the shared right-side workspace anchors, not the old dead-center 0.5/0.5 constant.
$journalWindowSource2 = Get-Content (Join-Path $ScriptRoot "src\JournalWindow.cs") -Raw
$launcherVisualSource2 = Get-Content (Join-Path $ScriptRoot "src\StandaloneLauncherVisual.cs") -Raw
if ($launcherSource -match '_label\.text\s*=[^;]*\[OPEN\]') {
    throw "Launcher open-state guard failed: JournalLauncher still injects a text-only [OPEN] suffix into its label."
}
if ($launcherSource -notmatch '_label\.text = "JOURNAL";') {
    throw "Launcher open-state guard failed: the JOURNAL label is no longer stable regardless of open state."
}
if ($launcherSource -notmatch '_openAccent\.SetActive\(open\)' -or $launcherSource -notmatch 'StandaloneLauncherVisual\.AddOpenAccent\(_panel\)') {
    throw "Launcher open-state guard failed: structural open/active accent is missing."
}
if ($launcherVisualSource2 -notmatch 'internal static GameObject AddOpenAccent') {
    throw "Launcher open-state guard failed: shared AddOpenAccent helper is missing from StandaloneLauncherVisual."
}
if ($journalWindowSource2 -match 'new RetainedPosition\(storedX, storedY, 0\.5f, 0\.5f, persist\)') {
    throw "Journal window default-position guard failed: old dead-center 0.5f/0.5f default constant is still wired in."
}
if ($journalWindowSource2 -notmatch 'ComputeDefaultPosition\(width, height, out defaultX, out defaultY\)' -or
    $journalWindowSource2 -notmatch 'StandaloneLauncherColumnPolicy\.DefaultPanelRightNormalized\(\)' -or
    $journalWindowSource2 -notmatch 'StandaloneLauncherColumnPolicy\.DefaultPanelTopNormalized\(\)') {
    throw "Journal window default-position guard failed: default position is not derived from the shared workspace anchors."
}
# Do NOT overwrite existing saved size: PersistWindowSize/WindowWidth/WindowHeight wiring must
# remain the sole size source, and this pass must not touch it.
$journalPluginSource = Get-Content (Join-Path $ScriptRoot "src\ErenshorJournalPlugin.cs") -Raw
if ($journalPluginSource -notmatch '_windowWidth\.Value, _windowHeight\.Value') {
    throw "Journal saved-size guard failed: window Initialize no longer sources width/height from persisted settings."
}
if ((Get-Content (Join-Path $ScriptRoot "src\JournalSettings.cs") -Raw) -notmatch 'WindowWidth = 720f') {
    throw "Journal saved-size guard failed: default WindowWidth setting changed unexpectedly."
}
Write-Host "Journal UI workspace normalization guard: PASS" -ForegroundColor Green
