# App-Icons

`generate-icons.mjs` erzeugt die Quellgrafiken als SVG (echte Feldreihenfolge der Dartscheibe, Dart in T20):

| Datei | Verwendung |
|---|---|
| `icon.svg` | App-Icon mit Hintergrund → `icon-512.png`, `icon-192.png`, `apple-touch-icon.png` |
| `icon-maskable.svg` | Android-Icon mit Rand für runde Masken → `icon-maskable-512.png` |
| `favicon.svg` | Vereinfachtes Brett ohne Hintergrund → `favicon.svg`, `favicon.png` (32 px), Logo in der App-Leiste |

## Neu erzeugen

```bash
node tools/icons/generate-icons.mjs tools/icons
```

Die PNGs werden mit Headless-Chrome aus den SVGs gerendert (PowerShell, im Repository-Root):

```powershell
$chrome = "C:\Program Files\Google\Chrome\Application\chrome.exe"
$src = (Resolve-Path tools/icons).Path
$dst = (Resolve-Path src/DartsApp/wwwroot).Path
$jobs = @(
    @("icon.svg", 512, "icon-512.png"), @("icon.svg", 192, "icon-192.png"), @("icon.svg", 180, "apple-touch-icon.png"),
    @("icon-maskable.svg", 512, "icon-maskable-512.png"), @("favicon.svg", 32, "favicon.png")
)
foreach ($j in $jobs) {
    $html = Join-Path $env:TEMP "icon-$($j[2]).html"
    "<html><body style='margin:0;overflow:hidden'><img src='file:///$src/$($j[0])' width='$($j[1])' height='$($j[1])' style='display:block'></body></html>" | Set-Content $html
    & $chrome --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 `
        --user-data-dir="$env:TEMP\icon-chrome" --window-size="$($j[1]),$($j[1])" --screenshot="$dst\$($j[2])" "file:///$html"
}
Copy-Item tools/icons/favicon.svg src/DartsApp/wwwroot/favicon.svg
```
