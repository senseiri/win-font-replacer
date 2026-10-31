$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'Cannot find the .NET Framework 4.x C# compiler (csc.exe).'
}

$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$sources = Get-ChildItem -Path $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$output = Join-Path $dist 'WinFontReplacer.exe'

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug-',
    '/codepage:65001',
    ('/win32manifest:' + (Join-Path $src 'App.manifest')),
    ('/win32icon:' + (Join-Path $src 'app.ico')),
    ('/out:' + $output),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Xml.dll',
    '/r:System.Xml.Linq.dll',
    '/r:System.Security.dll'
) + $sources

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed. csc exit code: $LASTEXITCODE"
}

Copy-Item -Force (Join-Path $root 'README.md') (Join-Path $dist 'README.md') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'LICENSE') (Join-Path $dist 'LICENSE') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'CHANGELOG.md') (Join-Path $dist 'CHANGELOG.md') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'SECURITY.md') (Join-Path $dist 'SECURITY.md') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'assets\icon-256.png') (Join-Path $dist 'icon.png') -ErrorAction SilentlyContinue

$distAssets = Join-Path $dist 'assets'
New-Item -ItemType Directory -Force -Path $distAssets | Out-Null
Copy-Item -Force (Join-Path $root 'assets\screenshot-main.png') (Join-Path $distAssets 'screenshot-main.png') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'assets\screenshot-dialog.png') (Join-Path $distAssets 'screenshot-dialog.png') -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $root 'assets\screenshot-effect.png') (Join-Path $distAssets 'screenshot-effect.png') -ErrorAction SilentlyContinue

$hash = Get-FileHash -Algorithm SHA256 $output
$checksumPath = Join-Path $dist 'SHA256SUMS.txt'
("SHA256  WinFontReplacer.exe`r`n{0}  WinFontReplacer.exe" -f $hash.Hash) |
    Set-Content -Path $checksumPath -Encoding ASCII

Write-Host ''
Write-Host ("Built: {0}" -f $output)
Write-Host ("Size:  {0:N0} bytes" -f (Get-Item $output).Length)
Write-Host ("SHA256: {0}" -f $hash.Hash)
