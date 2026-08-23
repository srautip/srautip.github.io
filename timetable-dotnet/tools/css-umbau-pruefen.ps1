<#
.SYNOPSIS
Belegt, dass ein Umbau NUR das CSS beruehrt hat.

.DESCRIPTION
Der schaerfste verfuegbare Nachweis fuer einen reinen CSS-Umbau. Ein
Byte-Vergleich der ganzen Datei scheidet aus - der <style>-Block aendert
sich ja absichtlich. Aber alles DAVOR und DANACH darf sich nicht um ein
Byte bewegen: Markup, das eingebettete JSON und die rund 2200 Zeilen
Inline-JS. Das sind ~95 % der Datei.

Faellt der Vergleich, ist man ins Markup oder ins JS gerutscht - genau
die Fehlerklasse, die man bei einem CSS-Umbau am wenigsten erwartet und
die kein Playwright-Test zuverlaessig meldet (er prueft Verhalten, nicht
Herkunft).

Vor dem Umbau einmal mit -Anlegen die Referenz erzeugen, danach nach
jedem Schritt ohne -Anlegen pruefen.

.EXAMPLE
powershell -File tools\css-umbau-pruefen.ps1 -Referenz C:\tmp\s0 -Anlegen
powershell -File tools\css-umbau-pruefen.ps1 -Referenz C:\tmp\s0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Referenz,
    [switch]$Anlegen,
    [string]$TestsRoot
)

$ErrorActionPreference = 'Stop'

# NICHT als Parameter-Default: $PSScriptRoot ist dort in Windows
# PowerShell 5.1 noch leer, wenn das Skript per -File gestartet wird.
if (-not $TestsRoot) {
    $hier = Split-Path -Parent $MyInvocation.MyCommand.Path
    $TestsRoot = Join-Path $hier '..\tests'
}
$TestsRoot = (Resolve-Path $TestsRoot).Path

if ($Anlegen -and -not (Test-Path $Referenz)) {
    New-Item -ItemType Directory -Force $Referenz | Out-Null
}
if (-not (Test-Path $Referenz)) {
    throw "Referenzordner '$Referenz' fehlt. Erst mit -Anlegen erzeugen (VOR dem Umbau!)."
}

function Get-RumpfHash {
    param([string]$Pfad)
    # Bewusst ReadAllText mit fester Kodierung statt Get-Content: das
    # erhaelt Zeilenenden und BOM-Verhalten unveraendert, und nur so ist
    # der Vergleich wirklich byteweise.
    $inhalt = [IO.File]::ReadAllText($Pfad, [Text.Encoding]::UTF8)
    $start = $inhalt.IndexOf('<style>')
    $ende = $inhalt.IndexOf('</style>')
    if ($start -lt 0 -or $ende -lt $start) {
        throw "$Pfad : kein <style>-Block gefunden - Vorlage unerwartet aufgebaut."
    }
    $rumpf = $inhalt.Substring(0, $start) + $inhalt.Substring($ende + 8)
    $bytes = [Text.Encoding]::UTF8.GetBytes($rumpf)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return @{
            Hash  = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '')
            Laenge = $rumpf.Length
            CssLaenge = $ende - $start
        }
    } finally { $sha.Dispose() }
}

$seiten = Get-ChildItem -Path $TestsRoot -Recurse -Filter '*.html' |
          Where-Object { $_.Name -eq 'stundentafel.html' -or $_.Name -eq 'klassenbildung.html' }
if (-not $seiten) { throw "Keine generierten Viewer-Seiten unter '$TestsRoot'." }

$fehler = 0
foreach ($seite in $seiten) {
    $kurz = $seite.FullName.Substring($TestsRoot.Length).TrimStart('\', '/')
    $marke = (Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $seite.FullName))) + "_" + $seite.Name + ".sha"
    $ablage = Join-Path $Referenz $marke
    $ist = Get-RumpfHash -Pfad $seite.FullName

    if ($Anlegen) {
        $ist.Hash | Out-File -FilePath $ablage -Encoding ascii -NoNewline
        "REF  {0,-52} Rumpf {1,9:N0}  CSS {2,7:N0}" -f $kurz, $ist.Laenge, $ist.CssLaenge | Write-Host
        continue
    }

    if (-not (Test-Path $ablage)) {
        Write-Host "WARN $kurz - keine Referenz hinterlegt, uebersprungen" -ForegroundColor Yellow
        continue
    }
    $soll = (Get-Content $ablage -Raw).Trim()
    if ($soll -eq $ist.Hash) {
        "OK   {0,-52} Rumpf {1,9:N0}  CSS {2,7:N0}" -f $kurz, $ist.Laenge, $ist.CssLaenge | Write-Host
    } else {
        $fehler++
        Write-Host ("FAIL {0,-52} Rumpf {1,9:N0}" -f $kurz, $ist.Laenge) -ForegroundColor Red
        Write-Host "     Ausserhalb von <style> hat sich etwas geaendert - das ist bei" -ForegroundColor Yellow
        Write-Host "     einem reinen CSS-Umbau ein Fehler (Markup oder Inline-JS beruehrt)." -ForegroundColor Yellow
    }
}

if ($Anlegen) { Write-Host "Referenz abgelegt unter $Referenz" -ForegroundColor Green; exit 0 }
if ($fehler -gt 0) { Write-Host "$fehler Seite(n) ausserhalb des CSS veraendert." -ForegroundColor Red; exit 1 }
Write-Host "Alle Seiten: ausserhalb von <style> unveraendert." -ForegroundColor Green
exit 0
