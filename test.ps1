$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'UIAutomationClient', 'UIAutomationTypes') | ForEach-Object {
    $assembly = Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_*\$_") -Recurse -Filter "$_.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $assembly) { throw "Missing WPF assembly: $_" }
    '/reference:' + $assembly.FullName
}
$sources = @('Perch.cs', 'Avatar.cs', 'PixelMochi.cs', 'Tools.cs', 'Model.cs', 'Theme.cs', 'Companion.cs', 'FocusLogic.cs', 'Activity.cs', 'BrowserNudge.cs', 'SmokeMochi.cs') | ForEach-Object { Join-Path $PSScriptRoot "work\$_" }
$output = Join-Path $PSScriptRoot 'work\smoke\SmokeMochi.exe'
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
& $compiler /nologo /target:exe /main:Perch.SmokeMochi "/out:$output" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Smoke test failed.' }
