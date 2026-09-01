# Erstellt alle Release-Pakete des Listenverschiebers.
#
# Aufruf:
#   .\build-release.ps1              Version wird aus der Projektdatei gelesen
#   .\build-release.ps1 -Version 3.12
#
# Ergebnis: fertige ZIP-Dateien im Ordner "release", dazu SHA256SUMS.txt
# mit den Pruefsummen aller Pakete.
#
# Hinweis: Bewusst ohne PublishSingleFile. Eine selbstentpackende Ein-Datei-EXE
# wird von einzelnen Virenscannern faelschlich als Schadsoftware gemeldet, weil
# sie sich zur Laufzeit selbst auspackt. Die Programmdateien liegen daher offen
# nebeneinander im Paket.

[CmdletBinding()]
param(
    [string]$Version,
    [string]$AusgabeOrdner = 'release'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # unterdrueckt die Fortschrittsbalken

$wurzel  = $PSScriptRoot
$projekt = Join-Path $wurzel 'Listenverschieber\Listenverschieber.csproj'

if (-not (Test-Path $projekt)) {
    throw "Projektdatei nicht gefunden: $projekt"
}

# Version aus der Projektdatei lesen, falls nicht angegeben
if (-not $Version) {
    $xml = [xml](Get-Content $projekt)
    $Version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ }) -as [string]
    if (-not $Version) { throw 'Kein <Version>-Element in der Projektdatei gefunden.' }
    $Version = $Version.Trim()
}

# Anzeigeversion: aus 3.11.0 wird 3.11
$kurz = ($Version -split '\.')[0..1] -join '.'

$zielWurzel = Join-Path $wurzel $AusgabeOrdner
$tempWurzel = Join-Path $wurzel 'publish'

Write-Host "Listenverschieber $kurz wird veroeffentlicht..." -ForegroundColor Cyan
Write-Host ''

Remove-Item $zielWurzel, $tempWurzel -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $zielWurzel -Force | Out-Null

# Zu erstellende Pakete:
#   Selbstenthaltend  = laeuft ohne installiertes .NET, dafuer gross
#   Framework         = klein, setzt .NET 8 Desktop Runtime voraus
$varianten = @(
    @{ Rid = 'win-x64';   Selbst = $true;  Name = 'win-x64-standalone' }
    @{ Rid = 'win-x86';   Selbst = $true;  Name = 'win-x86-standalone' }
    @{ Rid = 'win-arm64'; Selbst = $true;  Name = 'win-arm64-standalone' }
    @{ Rid = 'win-x64';   Selbst = $false; Name = 'win-x64-framework' }
    @{ Rid = 'win-x86';   Selbst = $false; Name = 'win-x86-framework' }
)

$ergebnis = @()

foreach ($v in $varianten) {
    $paketName = "Listenverschieber-$kurz-$($v.Name)"
    $ordner = Join-Path $tempWurzel $paketName
    Write-Host ("  {0,-24} wird gebaut..." -f $v.Name) -NoNewline

    $argumente = @(
        'publish', $projekt
        '-c', 'Release'
        '-r', $v.Rid
        '-o', $ordner
        '--self-contained', $v.Selbst.ToString().ToLower()
        '--nologo'
        '-v', 'quiet'
    )

    & dotnet @argumente | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen fuer $($v.Name)" }

    # Debug-Symbole gehoeren nicht ins Auslieferungspaket
    Remove-Item (Join-Path $ordner '*.pdb') -Force -ErrorAction SilentlyContinue

    # Erweiterungen in eigene Unterordner einsortieren.
    #
    # Hinweis: Die EXE sucht ihren .NET-Host (hostfxr, hostpolicy, coreclr,
    # clrjit, System.Private.CoreLib) fest im eigenen Verzeichnis. Diese
    # Dateien bleiben deshalb oben liegen. Die NuGet-Bibliotheken werden zur
    # Laufzeit von ErweiterungsLader.cs aus "Erweiterungen" nachgeladen.
    $erweiterungen = @(
        @{ Paket = 'PdfPig';    Muster = 'UglyToad.PdfPig*.dll' }
        @{ Paket = 'OpenXml';   Muster = 'DocumentFormat.OpenXml*.dll' }
        @{ Paket = 'Kodierung'; Muster = 'System.Text.Encoding.CodePages.dll' }
    )

    foreach ($e in $erweiterungen) {
        $treffer = @(Get-ChildItem (Join-Path $ordner $e.Muster) -File -ErrorAction SilentlyContinue)
        if ($treffer.Count -eq 0) { continue }

        $unterordner = Join-Path $ordner "Erweiterungen\$($e.Paket)"
        New-Item -ItemType Directory -Path $unterordner -Force | Out-Null
        $treffer | Move-Item -Destination $unterordner -Force
    }

    # Die Dateien liegen im ZIP in einem Unterordner, damit sie sich beim
    # Entpacken nicht ueber ein bestehendes Verzeichnis verteilen.
    $zip = Join-Path $zielWurzel "$paketName.zip"
    Compress-Archive -Path $ordner -DestinationPath $zip -Force

    $dateien = @(Get-ChildItem $ordner -File -Recurse)
    $ergebnis += [pscustomobject]@{
        Paket       = Split-Path $zip -Leaf
        Dateien     = $dateien.Count
        'Entpackt_MB' = [math]::Round((($dateien | Measure-Object Length -Sum).Sum) / 1MB, 1)
        'ZIP_MB'      = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    }

    Write-Host ' fertig' -ForegroundColor Green
}

# Lizenzhinweise zusaetzlich einzeln beilegen, damit sie ohne Entpacken lesbar sind
Copy-Item (Join-Path $wurzel 'LICENSE.txt')             $zielWurzel -Force
Copy-Item (Join-Path $wurzel 'THIRD-PARTY-NOTICES.txt') $zielWurzel -Force

# Pruefsummen erzeugen, damit sich die Echtheit der Pakete nachweisen laesst
$pruefdatei = Join-Path $zielWurzel 'SHA256SUMS.txt'
$kopf = @(
    "Listenverschieber $kurz - SHA-256 Pruefsummen"
    "Erstellt am $(Get-Date -Format 'dd.MM.yyyy HH:mm')"
    ''
    'Pruefen unter Windows (PowerShell):'
    '    Get-FileHash .\<Dateiname>.zip -Algorithm SHA256'
    ''
    'Stimmt der angezeigte Wert mit dem hier hinterlegten ueberein,'
    'ist die Datei unveraendert.'
    ''
)
$zeilen = Get-ChildItem (Join-Path $zielWurzel '*.zip') | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.Name
}
Set-Content -Path $pruefdatei -Value ($kopf + $zeilen) -Encoding UTF8

Write-Host ''
$ergebnis | Format-Table -AutoSize
Write-Host "Pruefsummen: $pruefdatei" -ForegroundColor Cyan
Write-Host "Pakete liegen in: $zielWurzel" -ForegroundColor Cyan
