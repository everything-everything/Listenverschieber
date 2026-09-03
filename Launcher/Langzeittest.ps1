# Langzeittest der Startvarianten.
#
# Startet eine Variante wiederholt und protokolliert, ob die Anwendung
# hochkommt und ob Dateien in Quarantaene verschoben werden.
#
# Aufruf:
#   .\Langzeittest.ps1 -Ordner <Paketordner> -Durchgaenge 30 -Name Batch
#
# Hintergrund: Virenscanner mit verhaltensbasierter Erkennung greifen
# nicht beim ersten Start, sondern erst nachdem sich ein Muster
# wiederholt hat. Einzelne erfolgreiche Starts sagen daher wenig aus.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Ordner,
    [int]$Durchgaenge = 30,
    [string]$Name = 'Test',
    [int]$Wartezeit = 16,
    [int]$Pause = 5
)

$ErrorActionPreference = 'Stop'

$start = Join-Path $Ordner 'Listenverschieber.exe'
if (-not (Test-Path $start)) {
    $start = Join-Path $Ordner 'Listenverschieber.cmd'
}
$anwendung = Join-Path $Ordner 'Programm\Listenverschieber.exe'

$protokoll = Join-Path $Ordner "..\Langzeittest-$Name.txt"
$zeilen = @("Langzeittest $Name", "Startdatei: $(Split-Path $start -Leaf)", "Beginn: $(Get-Date -Format 'HH:mm:ss')", '')

$erfolg = 0
$fehlstart = 0

for ($i = 1; $i -le $Durchgaenge; $i++) {
    if (-not (Test-Path $anwendung)) {
        $zeilen += "$i ANWENDUNG IN QUARANTAENE"
        break
    }
    if (-not (Test-Path $start)) {
        $zeilen += "$i STARTDATEI IN QUARANTAENE"
        break
    }

    Start-Process $start
    Start-Sleep $Wartezeit

    $lauf = Get-Process Listenverschieber -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -like '*Programm*' }

    if ($lauf) {
        $erfolg++
        $lauf | Stop-Process -Force
    } else {
        $fehlstart++
        $zeilen += "$i nicht gestartet"
    }

    Start-Sleep $Pause
}

$zeilen += ''
$zeilen += "Erfolgreich:  $erfolg von $Durchgaenge"
$zeilen += "Fehlstarts:   $fehlstart"
$zeilen += "Startdatei noch vorhanden:  $(Test-Path $start)"
$zeilen += "Anwendung noch vorhanden:   $(Test-Path $anwendung)"
$zeilen += "Ende: $(Get-Date -Format 'HH:mm:ss')"

Set-Content -Path $protokoll -Value $zeilen -Encoding UTF8
$zeilen | Select-Object -Last 6
