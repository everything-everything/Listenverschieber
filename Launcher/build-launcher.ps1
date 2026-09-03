# Uebersetzt das Startprogramm fuer x64, x86 und arm64.
#
# Aufruf:
#   .\build-launcher.ps1 -Rid win-x64 -Ziel <Ordner>
#
# Ergebnis: <Ziel>\LV_Start.exe
#
# Der Launcher ist bewusst in C++ geschrieben. NativeAOT beherrscht kein
# win-x86, und ein gewoehnliches .NET-Programm wuerde seine Laufzeit neben
# sich erwarten - die liegt im Paket aber im Unterordner "Programm".

[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Rid,

    [Parameter(Mandatory)]
    [string]$Ziel,

    # Launcher                     startet die Anwendung sofort und unsichtbar
    # Startfenster                 zeigt zuerst ein Fenster mit Fortschrittsbalken
    # StartfensterUeberBatch       wie Startfenster, startet aber ueber die Batch
    # StartfensterMitSchaltflaechen startet erst auf Klick, wahlweise direkt oder ueber die Batch
    [ValidateSet('Launcher', 'Startfenster', 'StartfensterUeberBatch',
                 'StartfensterMitSchaltflaechen')]
    [string]$Fassung = 'Launcher',

    # Bewusst nicht "Listenverschieber.exe": Eine EXE, die eine gleichnamige
    # EXE aus einem Unterordner startet, sieht nach einer Selbstkopie aus -
    # ein Muster, das Schadsoftware zur Verankerung nutzt. Der Name muss zu
    # den Angaben in Launcher.rc passen.
    [string]$Dateiname = 'LV_Start.exe'
)

$ErrorActionPreference = 'Stop'

$quelle = $PSScriptRoot
$architektur = @{ 'win-x64' = 'x64'; 'win-x86' = 'x86'; 'win-arm64' = 'arm64' }[$Rid]

# Entwicklungsumgebung suchen, um an den passenden Compiler zu kommen
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'vswhere.exe nicht gefunden - ist Visual Studio installiert?'
}

$vsPfad = & $vswhere -latest -prerelease -products * `
                     -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
                     -property installationPath
if (-not $vsPfad) {
    throw 'Keine Visual-Studio-Installation mit C++-Werkzeugen gefunden.'
}

$vcvars = Join-Path $vsPfad "VC\Auxiliary\Build\vcvarsall.bat"
if (-not (Test-Path $vcvars)) {
    throw "vcvarsall.bat nicht gefunden: $vcvars"
}

New-Item -ItemType Directory -Path $Ziel -Force | Out-Null
$Ziel = (Resolve-Path $Ziel).Path

# In einem eigenen Arbeitsordner uebersetzen, damit die Zwischendateien
# (.obj, .res) nicht im Zielordner landen
$arbeit = Join-Path ([System.IO.Path]::GetTempPath()) ("launcher-{0}-{1}" -f $architektur, [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $arbeit -Force | Out-Null

try {
    $exe = Join-Path $Ziel $Dateiname

    # Auf einem x64-Rechner uebersetzt "x64_arm64" fuer arm64, ohne dass
    # ein arm64-Geraet noetig ist
    $zielArchitektur = if ($architektur -eq 'x64') { 'x64' } else { "x64_$architektur" }

    # Der Compiler-Aufruf steht bewusst in einer einzigen Zeile: die
    # Zeilenfortsetzung von cmd ist zu fehleranfaellig.
    #
    #   /O1     auf Groesse optimieren - die Startdatei soll klein bleiben
    #   /GS-    keine Stackpruefung noetig, der Code verarbeitet keine Eingaben
    #   /Brepro reproduzierbare Ausgabe, wie beim Hauptprogramm
    $aufruf = 'cl /nologo /O1 /GS- /EHsc /DUNICODE /D_UNICODE /DNDEBUG ' +
              "`"$quelle\$Fassung.cpp`" Launcher.res /Fe:`"$exe`" " +
              '/link /SUBSYSTEM:WINDOWS /Brepro /INCREMENTAL:NO ' +
              "/MANIFEST:EMBED /MANIFESTINPUT:`"$quelle\Launcher.manifest`""

    $befehle = @(
        "@echo off"
        "call `"$vcvars`" $zielArchitektur >nul"
        "if errorlevel 1 exit /b 1"
        "cd /d `"$arbeit`""
        "rc /nologo /fo Launcher.res `"$quelle\Launcher.rc`""
        "if errorlevel 1 exit /b 1"
        $aufruf
        "if errorlevel 1 exit /b 1"
    ) -join "`r`n"

    $skript = Join-Path $arbeit 'bauen.cmd'
    Set-Content -Path $skript -Value $befehle -Encoding OEM

    $ausgabe = & cmd.exe /c "`"$skript`"" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Uebersetzen fehlgeschlagen ($Rid):`r`n$($ausgabe -join [Environment]::NewLine)"
    }

    if (-not (Test-Path $exe)) {
        throw "Der Launcher wurde nicht erzeugt: $exe"
    }

    Get-Item $exe
}
finally {
    Remove-Item $arbeit -Recurse -Force -ErrorAction SilentlyContinue
}
