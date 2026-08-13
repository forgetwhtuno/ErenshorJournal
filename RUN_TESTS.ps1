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
