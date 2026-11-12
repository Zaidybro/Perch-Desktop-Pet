$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$out = Join-Path $root 'outputs'
$sources = @(
    'build.ps1', 'test.ps1', 'package.ps1', 'README.md', 'RELEASE.txt', 'LICENSE', '.gitignore',
    'work/Perch.cs', 'work/Avatar.cs', 'work/PixelMochi.cs', 'work/Tools.cs',
    'work/Model.cs', 'work/Theme.cs', 'work/Companion.cs', 'work/FocusLogic.cs',
    'work/Activity.cs', 'work/BrowserNudge.cs', 'work/SmokeMochi.cs'
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-ReleaseZip($name, $entries) {
    $path = Join-Path $out $name
    if (-not ([IO.Path]::GetFullPath($path).StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw 'Package path escaped the workspace.'
    }
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $entries) {
            $file = Join-Path $root $entry.Source
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing release file: $file" }
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $file, $entry.Name, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $zip.Dispose() }
    Write-Host "Packed $path"
}

New-ReleaseZip 'Mochi-Windows-v0.1.0.zip' @(
    @{ Source = 'outputs/Perch.exe'; Name = 'Perch.exe' },
    @{ Source = 'RELEASE.txt'; Name = 'README.txt' },
    @{ Source = 'LICENSE'; Name = 'LICENSE' }
)
New-ReleaseZip 'Mochi-source-v0.1.0.zip' @($sources | ForEach-Object {
    @{ Source = $_; Name = $_.Replace('\', '/') }
})
