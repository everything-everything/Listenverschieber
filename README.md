# Listenverschieber

**Version 3.11** · Windows-Werkzeug zum Sortieren, Verschieben, Kopieren, Umbenennen und Durchsuchen großer Dateibestände.

Alle Arbeitsschritte laufen ausschließlich lokal ab — es werden keine Daten übertragen.

---

## Funktionen

Die Anwendung ist in vier Registerkarten gegliedert:

| Registerkarte | Zweck |
| --- | --- |
| **Unvollständige Dateien** | Prüft, ob zu jeder Datei die erwarteten Begleitdateien vorhanden sind, und listet unvollständige Gruppen auf. |
| **Listenverschieber** | Verschiebt oder kopiert Dateien anhand einer Liste von Namen oder Kennungen. |
| **Dateien umbenennen** | Benennt Dateien anhand ihres *Inhalts* um, z. B. mit einem Datum aus einer Textdatei (TXT, INI, LOG, CSV, XML, JSON), PDF-Datei oder Office-Datei (DOCX, XLSX). |
| **Inhaltssuche** | Durchsucht den Text *innerhalb* von Dateien und kann die Treffer kopieren oder verschieben. |

### Highlights

- **Vorschau vor jeder Änderung** — jede verändernde Aktion lässt sich vorab als Prüf- oder Vorschaulauf ausführen.
- **Inhaltsbasiertes Umbenennen** — Beispiel: In einer Textdatei (TXT, INI, LOG, CSV, XML, JSON) steht `Datum=01.04.2025`, im Dateinamen `IMG_20261112_WA0086_12345` wird der Datumsabschnitt zu `20250401`. Ebenso lesbar sind PDF- und Office-Dateien (DOCX, XLSX).
- **Abschnittswahl in Klartext** — statt regulärer Ausdrücke: *nur Ziffern*, *nur Buchstaben*, *Buchstaben und/oder Ziffern*, *beliebiger Inhalt*; eigenes Muster nur für Sonderfälle.
- **Vorwärts-/Rückwärtszählung** der Namensabschnitte, praktisch bei schwankender Abschnittsanzahl.
- **Gleichnamige Dateien mitumbenennen** — `Beleg.pdf` und `Beleg.ini` bleiben gepaart.
- **Mehrzeilige Inhaltssuche** mit drei Suchmodi und den Platzhaltern `*` und `?`.
- **Konfliktbehandlung** beim Verschieben/Kopieren inklusive Hashvergleich zur Prüfung auf identische Dateien.
- **Asynchrone Verarbeitung** mit Fortschrittsanzeige und jederzeitigem Abbruch.
- **Integrierte Hilfe** (F1) mit Themenfilter und Export des kompletten Handbuchs als Textdatei.

---

## Unterstützte Dateiformate

Für die inhaltsbasierten Funktionen muss der Text der Datei lesbar sein.

**Direkt lesbar:** TXT, INI, LOG, CSV, XML, JSON

**Über Bibliotheken:**
- PDF (durchsuchbar) — via PdfPig
- DOCX — via DocumentFormat.OpenXml
- XLSX — via DocumentFormat.OpenXml

**Nicht unterstützt:** gescannte PDFs ohne Texterkennung, die Altformate DOC und XLS sowie passwortgeschützte Dateien.

---

## Systemvoraussetzungen

| Punkt | Wert |
| --- | --- |
| Betriebssystem | Windows 10 (1809) oder neuer, Windows 11, Windows Server 2019+ |
| Architektur | x64 (64 Bit), x86 (32 Bit) oder ARM64 |
| Laufzeitumgebung | .NET 8 Desktop Runtime (nur bei den `framework`-Paketen) |
| Arbeitsspeicher | mind. 256 MB frei; bei großen PDF-Beständen mehr empfohlen |
| Festplatte | ca. 15 MB (`framework`) bzw. ca. 165 MB (`standalone`) |
| Rechte | Lese- und Schreibrechte auf den verwendeten Verzeichnissen |
| Netzwerk | nicht erforderlich, die Anwendung arbeitet vollständig lokal |

---

## Technik

- **Sprache:** C# 12
- **Plattform:** .NET 8 (`net8.0-windows`)
- **Oberfläche:** WPF, ergänzt um Windows Forms (nur für den Ordnerauswahl-Dialog)
- **Projektformat:** SDK-Style

### NuGet-Pakete

| Paket | Version | Lizenz | Zweck |
| --- | --- | --- | --- |
| PdfPig | 0.1.10 | Apache-2.0 | Textextraktion aus durchsuchbaren PDF-Dateien |
| DocumentFormat.OpenXml | 3.1.0 | MIT | Textextraktion aus DOCX und XLSX |
| System.Text.Encoding.CodePages | 9.0.9 | MIT | Unterstützung älterer Zeichensätze (z. B. Windows-1252) |

Alle eingesetzten Komponenten stehen unter permissiven Lizenzen und dürfen auch in Unternehmen verwendet werden. Die vollständigen Lizenztexte der Fremdkomponenten stehen in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) und liegen jedem Release bei.

### Aufbau

Die Oberfläche liegt in `MainWindow.xaml`. Der zugehörige Code ist auf mehrere Teilklassen verteilt; Fachlogik ohne Oberflächenbezug steckt in eigenen Klassen.

```
MainWindow.xaml.cs          Registerkarten 1 und 2, Konfiguration, Menü
MainWindow.Umbenennen.cs    Registerkarte 3
MainWindow.Inhaltssuche.cs  Registerkarte 4
DateiInhaltsLeser.cs        Textextraktion aus allen unterstützten Formaten
UmbenennungsLogik.cs        Abschnittserkennung und Namensbildung
InhaltsSuche.cs             Trefferlogik inkl. Platzhalter und Mehrzeilensuche
PfadKonfiguration.cs        Persistenz aller Einstellungen als JSON
KonfliktDialog.xaml.cs      Behandlung von Namenskonflikten
ProgrammInfo.cs             Zentrale Programm-, Autoren- und Lizenzangaben
HilfeInhalte.cs             Inhalte des Hilfe-Fensters
```

---

## Download und Installation

Fertige Pakete stehen unter [Releases](https://github.com/everything-everything/Listenverschieber/releases) bereit. Es ist keine Installation nötig: ZIP entpacken und `Listenverschieber.exe` starten.

| Paket | Download | Entpackt | Voraussetzung |
| --- | --- | --- | --- |
| `win-x64-standalone` | 61 MB | 152 MB | keine — läuft sofort |
| `win-x86-standalone` | 57 MB | 141 MB | keine — für 32-Bit-Windows |
| `win-arm64-standalone` | 57 MB | 164 MB | keine — für ARM-Geräte |
| `win-x64-framework` | 4 MB | 13 MB | .NET 8 Desktop Runtime |
| `win-x86-framework` | 4 MB | 13 MB | .NET 8 Desktop Runtime |

**Welches Paket?** Im Zweifelsfall `win-x64-standalone` — das läuft ohne weitere Voraussetzungen auf jedem aktuellen 64-Bit-Windows. `win-x86` ist für ältere 32-Bit-Systeme gedacht und läuft auch unter 64-Bit-Windows. Die schlanken `framework`-Pakete lohnen sich, wenn die [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) ohnehin installiert ist, etwa in verwalteten Firmenumgebungen.

### Prüfsummen

Jedem Release liegt die Datei `SHA256SUMS.txt` mit den SHA-256-Prüfsummen aller ZIP-Dateien bei. So lässt sich nachweisen, dass ein Download unverändert ist:

```powershell
Get-FileHash .\Listenverschieber-3.11-win-x64-standalone.zip -Algorithm SHA256
```

Stimmt der angezeigte Wert mit dem Eintrag in `SHA256SUMS.txt` überein, ist die Datei identisch mit dem veröffentlichten Paket.

### Hinweis zu Virenscannern

Die Pakete enthalten bewusst keine selbstentpackende Ein-Datei-EXE, sondern die Programmdateien offen nebeneinander. Selbstentpackende Dateien werden von einzelnen Scannern gelegentlich fälschlich gemeldet, weil sie sich beim Start selbst auspacken. Da die Anwendung nicht mit einem kostenpflichtigen Zertifikat signiert ist, kann Windows SmartScreen beim ersten Start dennoch eine Warnung zeigen; über *Weitere Informationen → Trotzdem ausführen* lässt sie sich starten. Wer sichergehen möchte, prüft die Prüfsumme oder baut die Anwendung selbst aus dem Quelltext.

---

## Erstellen und Starten

```powershell
git clone https://github.com/everything-everything/Listenverschieber.git
cd Listenverschieber
dotnet build
dotnet run --project Listenverschieber
```

Alternativ `Listenverschieber.slnx` in Visual Studio 2022 oder neuer öffnen und mit F5 starten.

### Veröffentlichen als eigenständige EXE

Alle Release-Pakete auf einmal erzeugen:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

Die fertigen ZIP-Dateien liegen anschließend im Ordner `release`, zusammen mit `SHA256SUMS.txt`. Einzelne Variante von Hand:

```powershell
dotnet publish Listenverschieber -c Release -r win-x64 --self-contained true
```

---

## Einstellungen

Alle Pfade und Optionen sämtlicher Registerkarten werden als JSON gespeichert und beim nächsten Start automatisch geladen. Die Datei lässt sich auf andere Arbeitsplätze kopieren; wird sie gelöscht, startet die Anwendung wieder mit den Standardwerten.

---

## Hinweise

- Vor umfangreichen Verschiebe-, Umbenenn- oder Löschaktionen eine **Datensicherung** anlegen. Umbenennen lässt sich nicht automatisch rückgängig machen.
- Bei Netzlaufwerken möglichst **UNC-Pfade** (`\\Server\Freigabe\Ordner`) statt verbundener Laufwerksbuchstaben verwenden.
- Jede Registerkarte führt ein eigenes **Protokoll**, das bei der Fehlersuche hilft.

---

## Lizenz

[MIT-Lizenz](LICENSE.txt) — © 2026 [everything-everything](https://github.com/everything-everything)

Das Programm darf kostenlos genutzt, verändert und weitergegeben werden, auch kommerziell. Die Weiterentwicklung durch andere ist ausdrücklich erwünscht. Einzige Bedingung ist, dass Copyright-Hinweis und Lizenztext erhalten bleiben.

Die Software wird ohne jede Gewährleistung bereitgestellt. Der Autor haftet nicht für Datenverluste oder Schäden, die aus der Verwendung entstehen.

---

## Mitwirkende KI

Bei der Entwicklung hat **GitHub Copilot (Claude)** mitgewirkt — unter anderem bei Entwurf, Implementierung und Dokumentation. Konzept, Anforderungen, fachliche Vorgaben und die Prüfung der Ergebnisse stammen vom Autor.
