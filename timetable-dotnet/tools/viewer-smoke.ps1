<#
.SYNOPSIS
Headless-Smoke-Test der Self-contained-Viewer (arc42 8.10).

.DESCRIPTION
Die Windows-Portierung des in CLAUDE.md beschriebenen Pruefrezepts. Das
Original zeigt auf `/opt/pw-browsers/` - einen Linux-Pfad aus der
Sandbox-Entwicklungsumgebung, den es unter Windows nicht gibt. Hier
uebernimmt Edge dieselbe Rolle: `--headless --dump-dom` fuehrt das
Inline-JS aus und gibt das fertige DOM zurueck.

Geprueft wird, dass die Seite ueberhaupt LEBT - dass also das inline
eingebettete JSON gelesen, ausgewertet und in DOM verwandelt wurde. Eine
Vorlage, deren JS beim Laden wirft, liefert ein leeres Geruest; genau das
faellt hier auf.

Fuer Interaktionstests (Drag & Drop, Pins) ein kleines Skript vor
</body> injizieren, das Events dispatcht und Ergebnisse in
document.title schreibt - dieselbe Technik wie im Linux-Original.

.EXAMPLE
pwsh tools/viewer-smoke.ps1
powershell -File tools\viewer-smoke.ps1 -TestsRoot tests
#>
[CmdletBinding()]
param(
    [string]$TestsRoot,
    [int]$ZeitbudgetMs = 8000
)

$ErrorActionPreference = 'Stop'

# NICHT als Parameter-Default: $PSScriptRoot ist dort in Windows
# PowerShell 5.1 noch leer, wenn das Skript per -File gestartet wird.
if (-not $TestsRoot) {
    $hier = Split-Path -Parent $MyInvocation.MyCommand.Path
    $TestsRoot = Join-Path $hier '..\tests'
}
$TestsRoot = (Resolve-Path $TestsRoot).Path

function Finde-Edge {
    $kandidaten = @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
    )
    foreach ($k in $kandidaten) { if (Test-Path $k) { return $k } }
    throw "msedge.exe nicht gefunden. Ohne Browser gibt es keinen Smoke-Test - die Viewer-Aenderung waere dann ungeprueft."
}

# Erwartete Kennzeichen je Vorlage: ein Element, das NUR entsteht, wenn
# das Inline-JS gelaufen ist. Reine Textsuche im Rohtext wuerde auch bei
# totem JS anschlagen.
$Erwartungen = @{
    'klassenbildung.html' = @{ Muster = 'id="board"'; Karten = 'class="[^"]*karte' }
    'stundentafel.html'   = @{ Muster = '<table';     Karten = '<tr' }
}

$edge = Finde-Edge
Write-Host "Edge: $edge"

$seiten = Get-ChildItem -Path $TestsRoot -Recurse -Filter '*.html' |
          Where-Object { $Erwartungen.ContainsKey($_.Name) }
if (-not $seiten) { throw "Keine Viewer-Seiten unter '$TestsRoot' gefunden - erst 'run'/'klassen' laufen lassen." }

$fehler = 0
foreach ($seite in $seiten) {
    $erwartung = $Erwartungen[$seite.Name]
    $profil = Join-Path $env:TEMP "edge-smoke-$([guid]::NewGuid().ToString('N').Substring(0,8))"
    try {
        $url = 'file:///' + ($seite.FullName -replace '\\', '/')
        $sw = [Diagnostics.Stopwatch]::StartNew()
        # Edge schreibt Diagnosezeilen nach stderr (z.B. "Edge LLM: Not
        # supported on non Desktop SKU"). Windows PowerShell 5.1 verpackt
        # native stderr-Ausgaben in ErrorRecords und macht daraus bei
        # ErrorActionPreference=Stop einen ABBRUCH - obwohl der Aufruf
        # erfolgreich war. Deshalb hier gezielt zurueckgestellt.
        $vorher = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $dom = & $edge --headless --disable-gpu --no-sandbox --log-level=3 `
                           --user-data-dir="$profil" `
                           --virtual-time-budget=$ZeitbudgetMs `
                           --dump-dom $url 2>$null | Out-String
        } finally {
            $ErrorActionPreference = $vorher
        }
        $sw.Stop()

        $treffer = ([regex]::Matches($dom, $erwartung.Karten)).Count
        $lebt = ($dom -match $erwartung.Muster) -and ($treffer -gt 0)

        $status = if ($lebt) { 'OK  ' } else { 'FAIL' }
        $kurz = $seite.FullName.Substring($TestsRoot.Length).TrimStart('\', '/')
        "{0} {1,-52} {2,7:N1}s  DOM {3,9:N0}  Elemente {4,6:N0}" -f `
            $status, $kurz, $sw.Elapsed.TotalSeconds, $dom.Length, $treffer | Write-Host

        if (-not $lebt) {
            $fehler++
            Write-Host "     erwartet: $($erwartung.Muster) plus mindestens ein '$($erwartung.Karten)'" -ForegroundColor Yellow
        }
    } finally {
        Remove-Item -Recurse -Force $profil -ErrorAction SilentlyContinue
    }
}

if ($fehler -gt 0) {
    Write-Host "$fehler Seite(n) haben ihr Inline-JS nicht ausgefuehrt." -ForegroundColor Red
    exit 1
}
Write-Host "Alle Viewer-Seiten leben." -ForegroundColor Green
exit 0
