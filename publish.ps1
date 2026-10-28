<#
.SYNOPSIS
    Baut die App und legt das Ergebnis für GitHub Pages ins Repository-Root.

.DESCRIPTION
    GitHub Pages liefert dieses Repository unter https://<user>.github.io/Darts/ aus.
    Das Skript veröffentlicht die Blazor-App, setzt den <base href> auf den Pages-Pfad,
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

Write-Host "Tests ausführen ..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests/DartsApp.Core.Tests") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Tests fehlgeschlagen." }

Write-Host "Veröffentlichen ..." -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src/DartsApp") -c Release -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen." }

$site = Join-Path $out "wwwroot"

# Base-Pfad für GitHub Pages setzen
$index = Join-Path $site "index.html"
(Get-Content $index -Raw -Encoding utf8).Replace('<base href="/" />', "<base href=""$BasePath"" />") |
    Set-Content $index -Encoding utf8 -NoNewline

# 404.html: Anzahl der Pfadsegmente des Base-Pfads eintragen
$segments = ($BasePath.Trim("/").Split("/", [StringSplitOptions]::RemoveEmptyEntries)).Count
$notFound = Join-Path $site "404.html"
(Get-Content $notFound -Raw -Encoding utf8).Replace("var segmentCount = 1;", "var segmentCount = $segments;") |
    Set-Content $notFound -Encoding utf8 -NoNewline

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

Write-Host "Fertig. Base-Pfad: $BasePath – jetzt committen und pushen." -ForegroundColor Green
