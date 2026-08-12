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
