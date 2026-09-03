# Erstellt alle Release-Pakete des Listenverschiebers.
#
# Aufruf:
#   .\build-release.ps1              Version wird aus der Projektdatei gelesen
#   .\build-release.ps1 -Version 3.12
#   .\build-release.ps1 -OhneSignatur      signiert die Dateien nicht
#   .\build-release.ps1 -MitStartprogramm  legt zusaetzlich LV_Start.exe bei
#
# Ergebnis: fertige ZIP-Dateien im Ordner "release", dazu SHA256SUMS.txt
# mit den Pruefsummen aller Pakete.
#
# Die Programmdateien werden signiert, bevor sie gepackt werden - sonst
# enthielten die ZIP-Dateien die unsignierten Fassungen. Fehlt ein
# Zertifikat, wird das Signieren mit einem Hinweis uebersprungen.
#
# Hinweis: Bewusst ohne PublishSingleFile. Eine selbstentpackende Ein-Datei-EXE
# wird von einzelnen Virenscannern faelschlich als Schadsoftware gemeldet, weil
# sie sich zur Laufzeit selbst auspackt. Die Programmdateien liegen daher offen
# nebeneinander im Paket.

[CmdletBinding()]
param(
    [string]$Version,
    [string]$AusgabeOrdner = 'release',
    [switch]$OhneSignatur,
    [switch]$MitStartprogramm
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # unterdrueckt die Fortschrittsbalken

$wurzel  = $PSScriptRoot
$projekt = Join-Path $wurzel 'Listenverschieber\Listenverschieber.csproj'

if (-not (Test-Path $projekt)) {
    throw "Projektdatei nicht gefunden: $projekt"
}

# Version aus der Projektdatei lesen, falls nicht angegeben.
# Bevorzugt wird die InformationalVersion, weil dort die eigentliche
# Anzeigeversion steht und auch Buchstaben erlaubt sind (z. B. "3.11a").
if (-not $Version) {
    $xml = [xml](Get-Content $projekt)
    $Version = ($xml.Project.PropertyGroup.InformationalVersion | Where-Object { $_ }) -as [string]
    if (-not $Version) {
        $Version = ($xml.Project.PropertyGroup.Version | Where-Object { $_ }) -as [string]
    }
    if (-not $Version) { throw 'Keine Version in der Projektdatei gefunden.' }
    $Version = $Version.Trim()
}

# Anzeigeversion. Eine reine Zahlenversion wie 3.11.1 wird auf 3.11 gekuerzt;
# eine bereits gesetzte Anzeigeversion wie "3.11a" bleibt unveraendert.
if ($Version -match '^\d+\.\d+\.\d+') {
    $kurz = ($Version -split '\.')[0..1] -join '.'
} else {
    $kurz = $Version
}

$zielWurzel = Join-Path $wurzel $AusgabeOrdner
# Eindeutiger Arbeitsordner, damit Reste eines vorherigen Laufs nicht stoeren
$tempWurzel = Join-Path $wurzel ('publish\{0}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

Write-Host "Listenverschieber $kurz wird veroeffentlicht..." -ForegroundColor Cyan

# Vorab pruefen, ob signiert werden kann. Fehlt das Zertifikat, wird ohne
# Signatur weitergebaut - das Release soll daran nicht scheitern.
$signSkript = $null
if (-not $OhneSignatur) {
    $pfad = Join-Path $wurzel 'sign-release.ps1'
    $hatZertifikat = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
                     Where-Object { $_.Subject -like 'CN=Listenverschieber*' -and $_.NotAfter -gt (Get-Date) }

    if ((Test-Path $pfad) -and $hatZertifikat) {
        $signSkript = $pfad
        Write-Host '  Die Programmdateien werden signiert.' -ForegroundColor DarkGray
    } else {
        Write-Host '  Ohne Signatur (kein Zertifikat vorhanden).' -ForegroundColor Yellow
    }
}

# Startprogramm mit Ladebalken - standardmaessig nicht im Paket.
#
# Microsoft Defender meldete zuletzt jede Architektur mit
# Trojan:Win32/Wacatac.B!ml, auch signiert und auch nach der Umbenennung
# von Listenverschieber.exe auf LV_Start.exe. Die Startdatei allein blieb
# durchweg unbeanstandet, weil dort cmd.exe der startende Prozess ist.
# Einzelheiten in Launcher\LIESMICH.md.
#
# Der Bau bleibt ueber -MitStartprogramm erreichbar, damit sich neue
# Fassungen weiter messen lassen.
$launcherSkript = $null
if ($MitStartprogramm) {
    $pfad = Join-Path $wurzel 'Launcher\build-launcher.ps1'
    if (Test-Path $pfad) {
        $launcherSkript = $pfad
        Write-Host '  Das Startprogramm wird mitgebaut.' -ForegroundColor DarkGray
    } else {
        Write-Host '  Ohne Startprogramm (build-launcher.ps1 fehlt).' -ForegroundColor Yellow
    }
}

Write-Host ''

Remove-Item $zielWurzel -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $wurzel 'publish') -Recurse -Force -ErrorAction SilentlyContinue
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

    # Ordnerstruktur aufraeumen:
    #   Listenverschieber.cmd   startet die Anwendung
    #   Programm\               Anwendung und .NET-Laufzeit
    #   Programm\Erweiterungen\ die NuGet-Bibliotheken (PdfPig, OpenXml, ...),
    #                           geordnet nach <Paket>\<Version>
    #
    # Die Laufzeit bleibt neben der Anwendung liegen. Sie laesst sich nicht
    # von ihr trennen: der Host kuerzt die Pfade aus der deps.json auf den
    # reinen Dateinamen, und "additionalProbingPaths" rechnet er gegen das
    # Arbeitsverzeichnis - der Start ueber eine Verknuepfung schlug damit
    # fehl. Stattdessen wandert weiter unten das ganze Gespann eine Ebene
    # tiefer, was der Host gar nicht bemerkt.
    #
    # Die NuGet-Bibliotheken werden dagegen erst spaeter geladen. Sie holt der
    # Resolver in App.xaml.cs aus "Erweiterungen", immer relativ zum
    # Programmverzeichnis und damit unabhaengig von der Startart.
    $abhaengigkeiten = Join-Path $ordner 'Listenverschieber.deps.json'
    $deps = Get-Content $abhaengigkeiten -Raw | ConvertFrom-Json
    $ziele = $deps.targets.PSObject.Properties |
             Where-Object { $_.Name -like '*/*' } |
             Select-Object -First 1

    foreach ($paket in $ziele.Value.PSObject.Properties) {
        # Das Programm selbst bleibt im Hauptverzeichnis
        if ($paket.Name -like 'Listenverschieber/*') { continue }

        # Die .NET-Laufzeit bleibt ebenfalls im Hauptverzeichnis
        if ($paket.Name -like 'runtimepack.*') { continue }

        $teile = $paket.Name -split '/'
        $basis = Join-Path $ordner "Erweiterungen\$($teile[0])\$($teile[1])"

        foreach ($abschnitt in @('runtime', 'native')) {
            $eintraege = $paket.Value.$abschnitt
            if (-not $eintraege) { continue }

            foreach ($relativ in $eintraege.PSObject.Properties.Name) {
                $datei = Split-Path $relativ -Leaf

                # Nur verwaltete Bibliotheken auslagern. Native Dateien laedt
                # das Betriebssystem, das den Unterordner nicht kennt.
                if ([System.IO.Path]::GetExtension($datei) -ne '.dll') { continue }

                $quelle = Join-Path $ordner $datei
                if (-not (Test-Path $quelle)) { continue }

                New-Item -ItemType Directory -Path $basis -Force | Out-Null
                Move-Item $quelle (Join-Path $basis $datei) -Force
            }
        }
    }

    # Kein "additionalProbingPaths" in der runtimeconfig.json: Diese Pfade
    # loest der Host relativ zum Arbeitsverzeichnis auf, weshalb der Start
    # ueber eine Verknuepfung fehlschlug. Das Nachladen uebernimmt stattdessen
    # der Resolver in App.xaml.cs.

    # Programmdateien in den Unterordner "Programm" verschieben und vorne
    # nur die Startdatei ablegen. Im Wurzelverzeichnis stehen dann nur
    # noch der Ordner, die Batch und die Lizenzhinweise.
    #
    # Die Laufzeit bleibt dabei unangetastet neben der Anwendung liegen -
    # nur eine Ebene tiefer. Der Host muss also nichts anders aufloesen,
    # woran alle frueheren Auslagerungsversuche gescheitert sind.
    $programmOrdner = Join-Path $ordner 'Programm'
    New-Item -ItemType Directory -Path $programmOrdner -Force | Out-Null

    Get-ChildItem $ordner -Force |
        Where-Object { $_.FullName -ne $programmOrdner } |
        ForEach-Object { Move-Item $_.FullName (Join-Path $programmOrdner $_.Name) -Force }

    # Die Startdatei bleibt die Grundlage: eine kleine, unbekannte EXE, die
    # eine andere EXE startet, entspricht dem Muster von Schadsoftware.
    # Microsoft Defender meldete daraufhin Wacatac.C!ml, und Norton verschob
    # die Anwendung in Quarantaene. Bei einer Batch ist der startende Prozess
    # dagegen cmd.exe - eine von Microsoft signierte Datei mit Reputation.
    Copy-Item (Join-Path $wurzel 'Launcher\Listenverschieber.cmd') `
              (Join-Path $ordner 'Listenverschieber.cmd') -Force

    # Lizenzhinweise gehoeren nach vorne, nicht in den Programmordner
    foreach ($hinweis in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.txt')) {
        $quelle = Join-Path $programmOrdner $hinweis
        if (Test-Path $quelle) { Move-Item $quelle (Join-Path $ordner $hinweis) -Force }
    }

    # Startprogramm mit Ladebalken daneben legen. Es startet die Anwendung
    # nicht selbst, sondern ueber die Startdatei oben - der Elternprozess
    # bleibt damit cmd.exe. In dieser Fassung meldeten die ML-Verfahren der
    # Virenscanner nichts, waehrend die Fassungen mit direktem Start
    # (Startfenster, StartfensterMitSchaltflaechen) Treffer erzeugten.
    #
    # Fehlen die C++-Werkzeuge, wird das Paket ohne Startprogramm gebaut -
    # die Startdatei allein genuegt zum Starten.
    if ($launcherSkript) {
        try {
            & $launcherSkript -Rid $v.Rid -Ziel $ordner `
                              -Fassung 'StartfensterUeberBatch' | Out-Null
        } catch {
            Write-Host ''
            Write-Host "  Startprogramm fuer $($v.Name) nicht gebaut: $($_.Exception.Message)" `
                       -ForegroundColor Yellow
        }
    }

    # Signieren muss vor dem Packen geschehen, sonst landen die unsignierten
    # Fassungen im ZIP. Das Signierskript arbeitet rekursiv und erfasst
    # damit sowohl das Startprogramm als auch die Anwendung im Unterordner.
    if ($signSkript) {
        & $signSkript -Ordner $ordner | Out-Null
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
