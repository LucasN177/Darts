<#
.SYNOPSIS
    Baut die App und legt das Ergebnis fuer GitHub Pages ins Repository-Root.

.DESCRIPTION
    GitHub Pages liefert dieses Repository unter https://<user>.github.io/Darts/ aus.
    Das Skript veroeffentlicht die Blazor-App, setzt den <base href> auf den Pages-Pfad,
    ersetzt den alten Build im Root und legt .nojekyll an (sonst ignoriert Pages "_framework").

.EXAMPLE
    ./publish.ps1
    ./publish.ps1 -BasePath /
#>
param(
    [string]$BasePath = "/Darts/"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root ".publish"

if (-not $BasePath.EndsWith("/")) { $BasePath += "/" }
if (-not $BasePath.StartsWith("/")) { $BasePath = "/" + $BasePath }

Write-Host "Tests ausfuehren ..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests/DartsApp.Core.Tests") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Tests fehlgeschlagen." }

Write-Host "Veroeffentlichen ..." -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src/DartsApp") -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen." }

$site = Join-Path $out "wwwroot"

$utf8 = New-Object System.Text.UTF8Encoding($false)
$swAssets = Join-Path $site "service-worker-assets.js"

# Ersetzt Text in einer veroeffentlichten Datei und aktualisiert ihren Hash im
# Service-Worker-Manifest (sonst schlaegt die Integritaetspruefung des Offline-Caches fehl).
function Update-PublishedFile([string]$name, [string]$search, [string]$replace) {
    $path = Join-Path $site $name
    $content = [IO.File]::ReadAllText($path, $utf8)
    if (-not $content.Contains($search)) { throw "'$search' nicht in $name gefunden." }
    [IO.File]::WriteAllText($path, $content.Replace($search, $replace), $utf8)

    $sha = [Security.Cryptography.SHA256]::Create()
    $hash = "sha256-" + [Convert]::ToBase64String($sha.ComputeHash([IO.File]::ReadAllBytes($path)))
    $manifest = [IO.File]::ReadAllText($swAssets, $utf8)
    $pattern = '"hash": "sha256-[^"]+",(\s*)"url": "' + [regex]::Escape($name) + '"'
    $manifest = [regex]::Replace($manifest, $pattern, { param($m) '"hash": "' + $hash + '",' + $m.Groups[1].Value + '"url": "' + $name + '"' })
    [IO.File]::WriteAllText($swAssets, $manifest, $utf8)
}

# Base-Pfad fuer GitHub Pages setzen
Update-PublishedFile "index.html" '<base href="/" />' "<base href=""$BasePath"" />"

# 404.html: Anzahl der Pfadsegmente des Base-Pfads eintragen
$segments = ($BasePath.Trim("/").Split("/", [StringSplitOptions]::RemoveEmptyEntries)).Count
Update-PublishedFile "404.html" "var segmentCount = 1;" "var segmentCount = $segments;"

# Komprimierte Varianten werden von GitHub Pages nicht genutzt
Get-ChildItem $site -Recurse -Include *.br, *.gz | Remove-Item -Force

Write-Host "Alten Build im Root ersetzen ..." -ForegroundColor Cyan
$oldItems = @(
    "_framework", "_content", "css", "wwwroot",
    "index.html", "404.html", "appsettings.json", "manifest.webmanifest",
    "service-worker.js", "service-worker-assets.js", "icon-192.png", "icon-512.png",
    "web.config", "DartsApp.styles.css"
)
foreach ($item in $oldItems) {
    $path = Join-Path $root $item
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}

Copy-Item (Join-Path $site "*") $root -Recurse -Force
New-Item -ItemType File -Path (Join-Path $root ".nojekyll") -Force | Out-Null
Remove-Item $out -Recurse -Force

Write-Host "Fertig. Base-Pfad: $BasePath - jetzt committen und pushen." -ForegroundColor Green
