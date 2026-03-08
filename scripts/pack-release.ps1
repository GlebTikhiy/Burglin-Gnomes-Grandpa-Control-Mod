param(
    [string]$Version = "1.6.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "BurglinGnomesGrandpaMod\BurglinGnomesGrandpaMod.csproj"
$buildOut = Join-Path $repoRoot "BurglinGnomesGrandpaMod\bin\Debug\BurglinGnomesGrandpaMod.dll"
$distDir = Join-Path $repoRoot ("dist\v" + $Version)
$zipPath = Join-Path $distDir ("BurglinGnomesGrandpaMod-v" + $Version + ".zip")

if (Test-Path $distDir) {
    Remove-Item -Recurse -Force $distDir
}
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "Building project..."
dotnet build $project -v minimal | Out-Host

if (-not (Test-Path $buildOut)) {
    throw "Build output not found: $buildOut"
}

Copy-Item $buildOut (Join-Path $distDir "BurglinGnomesGrandpaMod.dll") -Force
Copy-Item (Join-Path $repoRoot "README.md") (Join-Path $distDir "README.md") -Force
Copy-Item (Join-Path $repoRoot "CHANGELOG.md") (Join-Path $distDir "CHANGELOG.md") -Force
Copy-Item (Join-Path $repoRoot "LICENSE") (Join-Path $distDir "LICENSE") -Force

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $distDir "BurglinGnomesGrandpaMod.dll"), (Join-Path $distDir "README.md"), (Join-Path $distDir "CHANGELOG.md"), (Join-Path $distDir "LICENSE") -DestinationPath $zipPath

$hashDll = (Get-FileHash (Join-Path $distDir "BurglinGnomesGrandpaMod.dll") -Algorithm SHA256).Hash
$hashZip = (Get-FileHash $zipPath -Algorithm SHA256).Hash
@(
    "SHA256  BurglinGnomesGrandpaMod.dll  $hashDll",
    "SHA256  $(Split-Path -Leaf $zipPath)  $hashZip"
) | Set-Content -Path (Join-Path $distDir "SHA256SUMS.txt") -Encoding UTF8

Write-Host "Prepared release artifacts:" -ForegroundColor Green
Get-ChildItem $distDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize



