$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $compiler)) { throw 'Windows .NET Framework compiler not found.' }
$references = @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'UIAutomationClient', 'UIAutomationTypes') | ForEach-Object {
    $assembly = Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_*\$_") -Recurse -Filter "$_.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $assembly) { throw "Missing WPF assembly: $_" }
    '/reference:' + $assembly.FullName
}
$sources = @('Perch.cs', 'Avatar.cs', 'PixelMochi.cs', 'Tools.cs', 'Model.cs', 'Theme.cs', 'Companion.cs', 'FocusLogic.cs', 'Activity.cs', 'BrowserNudge.cs') | ForEach-Object { Join-Path $PSScriptRoot "work\$_" }
$output = Join-Path $PSScriptRoot 'outputs\Perch.exe'
$pending = Join-Path $PSScriptRoot 'work\Perch-build.exe'
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
& $compiler /nologo /target:winexe "/out:$pending" $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if (Test-Path -LiteralPath $output) {
    $archive = Join-Path $PSScriptRoot ('work\archive\Perch-previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.exe')
    $root = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
    if (-not ([IO.Path]::GetFullPath($output).StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFullPath($archive).StartsWith($root, [StringComparison]::OrdinalIgnoreCase))) { throw 'Build paths outside workspace.' }
    New-Item -ItemType Directory -Force (Split-Path $archive) | Out-Null
    Move-Item -LiteralPath $output -Destination $archive
}
Copy-Item -LiteralPath $pending -Destination $output
Write-Host "Built $output"
