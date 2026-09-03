# Signiert die Programmdateien mit einem eigenen Zertifikat.
#
# Hintergrund
# -----------
# Unsignierte Dateien haben bei Virenscannern keinerlei Vertrauensbonus.
# Ein gekauftes Zertifikat (200-600 Euro/Jahr) ist die wirksamste Loesung,
# lohnt sich aber nicht fuer ein internes Werkzeug. Diese Zwischenloesung
# kostet nichts:
#
#   1. Einmalig ein eigenes Zertifikat erstellen (haelt 5 Jahre).
#   2. Das Zertifikat einmalig auf den Firmen- und Kundenrechnern als
#      vertrauenswuerdig hinterlegen (siehe -Exportieren).
#   3. Ab dann ist jede signierte Version dort sofort vertrauenswuerdig,
#      auch nach einem Update. Genau das loest das wiederkehrende Problem.
#
# Ohne Schritt 2 wirkt die Signatur nur eingeschraenkt - sie zeigt aber
# immerhin einen gleichbleibenden Herausgeber und verhindert unbemerkte
# Aenderungen an den Dateien.
#
# Aufruf:
#   .\sign-release.ps1 -Erstellen              Zertifikat einmalig anlegen
#   .\sign-release.ps1 -Exportieren            Zertifikat zum Verteilen ausgeben
#   .\sign-release.ps1 -Sichern                Sicherungskopie des Schluessels
#   .\sign-release.ps1                         Dateien im publish-Ordner signieren

[CmdletBinding()]
param(
    [switch]$Erstellen,
    [switch]$Exportieren,
    [switch]$Sichern,
    [string]$Herausgeber = 'Listenverschieber (internes Werkzeug)',
    [string]$Ordner
)

$ErrorActionPreference = 'Stop'
$wurzel = $PSScriptRoot
$betreff = "CN=$Herausgeber"

function Hole-Zertifikat {
    Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $betreff -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

# --- Zertifikat einmalig erstellen -----------------------------------------
if ($Erstellen) {
    if (Hole-Zertifikat) {
        Write-Host 'Es besteht bereits ein gueltiges Zertifikat.' -ForegroundColor Yellow
        return
    }

    $zert = New-SelfSignedCertificate `
        -Subject $betreff `
        -Type CodeSigningCert `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5)

    Write-Host 'Zertifikat erstellt.' -ForegroundColor Green
    Write-Host "  Herausgeber: $($zert.Subject)"
    Write-Host "  Fingerabdruck: $($zert.Thumbprint)"
    Write-Host "  Gueltig bis: $($zert.NotAfter.ToString('dd.MM.yyyy'))"
    Write-Host ''
    Write-Host 'Naechster Schritt: .\sign-release.ps1 -Exportieren' -ForegroundColor Cyan
    return
}

# --- Oeffentlichen Teil zum Verteilen ausgeben ------------------------------
if ($Exportieren) {
    $zert = Hole-Zertifikat
    if (-not $zert) { throw 'Kein Zertifikat vorhanden. Zuerst -Erstellen aufrufen.' }

    $datei = Join-Path $wurzel 'Listenverschieber-Herausgeber.cer'
    Export-Certificate -Cert $zert -FilePath $datei -Force | Out-Null

    Write-Host "Zertifikat ausgegeben: $datei" -ForegroundColor Green
    Write-Host ''
    Write-Host 'So wird es auf einem Rechner als vertrauenswuerdig hinterlegt'
    Write-Host '(einmalig, als Administrator):'
    Write-Host ''
    Write-Host '  Import-Certificate -FilePath .\Listenverschieber-Herausgeber.cer ' -NoNewline -ForegroundColor Cyan
    Write-Host '`' -ForegroundColor Cyan
    Write-Host '      -CertStoreLocation Cert:\LocalMachine\TrustedPublisher' -ForegroundColor Cyan
    Write-Host ''
    Write-Host 'Nur der oeffentliche Teil wird ausgegeben - der private'
    Write-Host 'Schluessel bleibt auf diesem Rechner und darf nie weitergegeben werden.'
    return
}

# --- Sicherungskopie des privaten Schluessels -------------------------------
if ($Sichern) {
    $zert = Hole-Zertifikat
    if (-not $zert) { throw 'Kein Zertifikat vorhanden. Zuerst -Erstellen aufrufen.' }

    Write-Host 'Sicherung des privaten Schluessels' -ForegroundColor Cyan
    Write-Host ''
    Write-Host 'Der private Schluessel haengt am Windows-Benutzerprofil. Geht das'
    Write-Host 'Profil verloren (Neuinstallation, Rechnerwechsel, Defekt), kann'
    Write-Host 'ohne Sicherung keine neue Version mehr signiert werden - dann'
    Write-Host 'muesste auf jedem Rechner ein neues Zertifikat hinterlegt werden.'
    Write-Host ''

    $kennwort = Read-Host 'Kennwort fuer die Sicherungsdatei' -AsSecureString
    if ($kennwort.Length -eq 0) { throw 'Ohne Kennwort wird keine Sicherung erstellt.' }

    $datei = Join-Path $wurzel 'Listenverschieber-Zertifikat-Sicherung.pfx'
    Export-PfxCertificate -Cert $zert -FilePath $datei -Password $kennwort | Out-Null

    Write-Host ''
    Write-Host "Sicherung erstellt: $datei" -ForegroundColor Green
    Write-Host ''
    Write-Host 'Diese Datei enthaelt den privaten Schluessel.' -ForegroundColor Yellow
    Write-Host 'Sie gehoert an einen sicheren Ort ausserhalb dieses Rechners'
    Write-Host '(z. B. verschluesselter USB-Stick) und darf nicht ins Git-Ablage'
    Write-Host 'oder an Dritte gelangen. Kennwort getrennt davon aufbewahren.'
    Write-Host ''
    Write-Host 'Zurueckholen auf einem neuen Rechner:'
    Write-Host '  Import-PfxCertificate -FilePath .\Listenverschieber-Zertifikat-Sicherung.pfx `' -ForegroundColor Cyan
    Write-Host '      -CertStoreLocation Cert:\CurrentUser\My -Password (Read-Host -AsSecureString)' -ForegroundColor Cyan
    return
}

# --- Dateien signieren ------------------------------------------------------
$zert = Hole-Zertifikat
if (-not $zert) { throw 'Kein Zertifikat vorhanden. Zuerst -Erstellen aufrufen.' }

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*\x64\*' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe nicht gefunden (Windows SDK erforderlich).' }

if (-not $Ordner) {
    $Ordner = Get-ChildItem (Join-Path $wurzel 'publish') -Directory -ErrorAction SilentlyContinue |
              Sort-Object Name -Descending |
              Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Ordner -or -not (Test-Path $Ordner)) { throw 'Kein publish-Ordner gefunden. Zuerst build-release.ps1 aufrufen.' }

# Nur die selbst erstellten Dateien signieren - die Microsoft-Laufzeit
# ist bereits von Microsoft signiert und darf nicht veraendert werden.
#
# Das Startprogramm wird bewusst mitsigniert. Die Messungen auf
# VirusTotal schwankten, ergaben zuletzt aber eindeutig:
#
#   Startprogramm signiert     keine Meldung
#   Startprogramm unsigniert   Trojan:Win32/Wacatac.B!ml (Microsoft),
#                              Malicious (SecureAge)
#
# Ein frueherer Durchlauf hatte das Gegenteil gezeigt - damals fehlte
# allerdings die Einzelinstanz-Pruefung, die Prozesse aufzaehlt und den
# Verdachtswert der unsignierten Datei anhebt. Verlassen sollte man sich
# auf keines der beiden Ergebnisse: die !ml-Verdikte stammen aus
# Cloud-Modellen, die sich laufend aendern. Vor einer Veroeffentlichung
# lohnt daher ein erneuter Test.
$dateien = Get-ChildItem $Ordner -Recurse -Include 'LV_Start.exe', 'Listenverschieber.exe', 'Listenverschieber.dll' -File

if (-not $dateien) { throw 'Keine zu signierenden Dateien gefunden.' }

Write-Host "Signiere $($dateien.Count) Dateien..." -ForegroundColor Cyan

& $signtool.FullName sign `
    /sha1 $zert.Thumbprint `
    /fd SHA256 `
    /tr http://timestamp.digicert.com `
    /td SHA256 `
    /q `
    $dateien.FullName

if ($LASTEXITCODE -ne 0) { throw 'Signieren fehlgeschlagen.' }

Write-Host 'Signiert und mit Zeitstempel versehen.' -ForegroundColor Green

# Der Hinweis gilt nur beim direkten Aufruf. Beim Aufruf aus build-release.ps1
# wird ohnehin erst nach dem Signieren gepackt.
if (-not $PSBoundParameters.ContainsKey('Ordner')) {
    Write-Host ''
    Write-Host 'Hinweis: Die Pakete muessen nach dem Signieren neu gepackt werden,'
    Write-Host 'damit die Signatur in den ZIP-Dateien enthalten ist.'
}
