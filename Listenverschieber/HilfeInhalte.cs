using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Listenverschieber
{
    /// <summary>Ein einzelnes Hilfethema mit Titel und Absätzen.</summary>
    public sealed class HilfeThema
    {
        public string Titel { get; init; } = string.Empty;

        /// <summary>Absätze des Themas. Zeilen mit "## " werden als Zwischenüberschrift dargestellt.</summary>
        public IReadOnlyList<string> Absaetze { get; init; } = Array.Empty<string>();

        public string AlsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Titel);
            sb.AppendLine(new string('=', Titel.Length));
            sb.AppendLine();
            foreach (var absatz in Absaetze)
            {
                sb.AppendLine(absatz.StartsWith("## ", StringComparison.Ordinal) ? absatz[3..] : absatz);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Stellt sämtliche Hilfeinhalte bereit: technische Informationen, Systemvoraussetzungen
    /// und die Bedienungsanleitung der einzelnen Registerkarten.
    /// </summary>
    public static class HilfeInhalte
    {
        public static IReadOnlyList<HilfeThema> Alle() => new[]
        {
            Ueberblick(),
            Info(),
            TechnischeInformationen(),
            Systemvoraussetzungen(),
            TabUnvollstaendigeDateien(),
            TabListenverschieber(),
            TabUmbenennen(),
            TabInhaltssuche(),
            Dateiformate(),
            Exportieren(),
            Konfiguration(),
            Problembehandlung()
        };

        public static string AllesAlsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ProgrammInfo.Name} - Handbuch (Version {ProgrammInfo.Version})");
            sb.AppendLine($"Erstellt am {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine();
            foreach (var thema in Alle())
            {
                sb.AppendLine(thema.AlsText());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static HilfeThema Ueberblick() => new()
        {
            Titel = "Überblick",
            Absaetze = new[]
            {
                $"{ProgrammInfo.Name} ist ein Windows-Werkzeug zum Sortieren, Verschieben, Kopieren, Umbenennen und Durchsuchen großer Dateibestände. Alle Arbeitsschritte laufen ausschließlich lokal ab; es werden keine Daten übertragen.",
                "## Die vier Registerkarten",
                "1. Unvollständige Dateien - prüft, ob zu jeder Datei die erwarteten Begleitdateien vorhanden sind, und listet unvollständige Gruppen auf.",
                "2. Listenverschieber - verschiebt oder kopiert Dateien anhand einer Liste von Namen oder Kennungen.",
                "3. Dateien umbenennen - benennt Dateien anhand ihres Inhalts um, zum Beispiel mit einem Datum aus einer Text- oder PDF-Datei.",
                "4. Inhaltssuche - durchsucht den Text innerhalb von Dateien und kann die Treffer kopieren oder verschieben.",
                "## Grundsätzliche Arbeitsweise",
                "Vor jeder verändernden Aktion steht ein Prüf- oder Vorschaulauf zur Verfügung. Nutzen Sie diesen immer zuerst: Sie sehen dann genau, welche Datei wohin verschoben oder wie sie benannt würde, ohne dass etwas geändert wird.",
                "Wichtig: Legen Sie vor umfangreichen Verschiebe-, Umbenenn- oder Löschaktionen eine Datensicherung an.",
                "## Hilfetexte übernehmen",
                "Der angezeigte Text lässt sich mit der Maus markieren und über Strg+C in die Zwischenablage kopieren. Die Schaltfläche 'Thema kopieren' übernimmt das gesamte Thema auf einmal, 'Als Textdatei speichern' legt das vollständige Handbuch als Datei ab."
            }
        };

        private static HilfeThema Info() => new()
        {
            Titel = "Info über das Programm",
            Absaetze = new[]
            {
                $"Programm: {ProgrammInfo.Name}",
                $"Version: {ProgrammInfo.Version}",
                $"Autor: {ProgrammInfo.Autor}",
                $"GitHub-Profil: {ProgrammInfo.GitHubProfil}",
                $"Projektseite: {ProgrammInfo.GitHubProjekt}",
                "## Mitwirkende KI",
                ProgrammInfo.KiHinweis,
                "## Lizenz",
                $"{ProgrammInfo.Lizenz} - © {ProgrammInfo.CopyrightJahr} {ProgrammInfo.Autor}",
                ProgrammInfo.LizenzKurz,
                "## Lizenzen der verwendeten Komponenten",
                string.Join(Environment.NewLine, ProgrammInfo.NugetPakete.Select(p => $"• {p.Paket} {p.Version} - {p.Lizenz}")),
                "Alle eingesetzten Komponenten stehen unter permissiven Lizenzen (MIT bzw. Apache 2.0) und dürfen auch in Unternehmen eingesetzt werden.",
                "Die vollständigen Lizenztexte der Fremdkomponenten liegen dem Programm in der Datei THIRD-PARTY-NOTICES.txt bei.",
                "## Haftungsausschluss",
                "Die Software wird ohne jede Gewährleistung bereitgestellt. Der Autor haftet nicht für Datenverluste oder Schäden, die aus der Verwendung entstehen."
            }
        };

        private static HilfeThema TechnischeInformationen() => new()
        {
            Titel = "Technische Informationen",
            Absaetze = new[]
            {
                "## Womit wurde das Programm geschrieben?",
                string.Join(Environment.NewLine, ProgrammInfo.Technologie.Select(t => $"• {t.Bereich}: {t.Wert}")),
                "## Verwendete NuGet-Pakete",
                string.Join(Environment.NewLine + Environment.NewLine,
                    ProgrammInfo.NugetPakete.Select(p => $"• {p.Paket} (Version {p.Version}, Lizenz {p.Lizenz})\n   Zweck: {p.Zweck}")),
                "## Erweiterungen und eingebaute Funktionsbausteine",
                "• DateiInhaltsLeser - liest Text aus TXT, INI, LOG, CSV, XML, JSON, PDF, DOCX und XLSX.",
                "• UmbenennungsLogik - ermittelt aus dem Dateiinhalt den gewünschten Abschnitt und bildet den neuen Dateinamen.",
                "• InhaltsSuche - vergleicht Suchbegriffe mit dem Dateiinhalt, inklusive Platzhalter und Mehrzeilensuche.",
                "• PfadKonfiguration - speichert alle Einstellungen aller Registerkarten als JSON.",
                "• KonfliktDialog - behandelt Namenskonflikte beim Verschieben und Kopieren.",
                "## Aufbau der Anwendung",
                "Die Oberfläche liegt in MainWindow.xaml. Der zugehörige Programmcode ist auf mehrere Teilklassen (partial classes) verteilt: MainWindow.xaml.cs für die ersten beiden Registerkarten, MainWindow.Umbenennen.cs für das Umbenennen und MainWindow.Inhaltssuche.cs für die Inhaltssuche. Fachlogik ohne Oberflächenbezug liegt in eigenen Klassen, damit sie unabhängig getestet und wiederverwendet werden kann.",
                "## Verarbeitung im Hintergrund",
                "Lang laufende Vorgänge - besonders die Inhaltssuche - werden asynchron ausgeführt und lassen sich jederzeit abbrechen. Der Fortschritt wird während des Laufs angezeigt, die Oberfläche bleibt bedienbar.",
                "## Zeichensätze",
                "Über System.Text.Encoding.CodePages werden auch ältere Zeichensätze wie Windows-1252 unterstützt, damit Umlaute in alten Textdateien korrekt gelesen werden."
            }
        };

        private static HilfeThema Systemvoraussetzungen() => new()
        {
            Titel = "Systemvoraussetzungen",
            Absaetze = new[]
            {
                string.Join(Environment.NewLine, ProgrammInfo.Systemvoraussetzungen.Select(s => $"• {s.Punkt}: {s.Wert}")),
                "## Welches Paket ist das richtige?",
                "Das Programm wird in mehreren Ausführungen bereitgestellt. Die Bezeichnung des Pakets nennt zuerst die Architektur und danach die Art der Auslieferung:",
                "• standalone - enthält die .NET-Laufzeitumgebung bereits. Nach dem Entpacken sofort startklar, benötigt aber rund 165 MB Speicherplatz. Im Zweifelsfall diese Ausführung wählen.",
                "• framework - benötigt nur rund 15 MB, setzt aber voraus, dass die .NET 8 Desktop Runtime auf dem Rechner installiert ist. Sinnvoll in verwalteten Umgebungen, in denen .NET ohnehin verteilt wird.",
                "• win-x64 für übliche 64-Bit-Systeme, win-x86 für ältere 32-Bit-Systeme, win-arm64 für Geräte mit ARM-Prozessor.",
                "Die 32-Bit-Ausführung läuft auch auf 64-Bit-Windows. Umgekehrt gilt das nicht.",
                "## Installation",
                "Eine Installation ist nicht erforderlich. Das heruntergeladene Archiv wird entpackt, danach lässt sich Listenverschieber.exe unmittelbar starten. Zum Entfernen genügt es, den Ordner zu löschen.",
                "## Hinweise",
                "Das Programm ist eine reine Windows-Desktopanwendung und läuft nicht unter macOS oder Linux.",
                "Für Netzlaufwerke gilt: Verwenden Sie möglichst UNC-Pfade (\\\\Server\\Freigabe\\Ordner) statt verbundener Laufwerksbuchstaben, damit die Pfade auch nach einem Neustart gültig bleiben.",
                "Bei sehr großen PDF-Beständen steigt der Speicherbedarf während der Textextraktion. Grenzen Sie in diesem Fall die Dateiendungen ein oder verarbeiten Sie die Bestände in Teilmengen."
            }
        };

        private static HilfeThema TabUnvollstaendigeDateien() => new()
        {
            Titel = "Registerkarte: Unvollständige Dateien",
            Absaetze = new[]
            {
                "Diese Registerkarte prüft, ob zu einem Vorgang alle erwarteten Dateien vorliegen. Dateien, die zwar zusammengehören, aber unvollständig sind, werden aufgelistet und können exportiert werden.",
                "## Vorgehen",
                "1. Quellpfad auswählen.",
                "2. Zu prüfende Dateiendungen festlegen.",
                "3. Prüflauf starten.",
                "4. Ergebnis in der Liste kontrollieren und bei Bedarf exportieren.",
                "## Hinweis",
                "Die Zusammengehörigkeit wird über den Dateinamen ohne Endung ermittelt. Dateien mit gleichem Namen und unterschiedlicher Endung gelten als eine Gruppe."
            }
        };

        private static HilfeThema TabListenverschieber() => new()
        {
            Titel = "Registerkarte: Listenverschieber",
            Absaetze = new[]
            {
                "Hier verschieben oder kopieren Sie Dateien anhand einer Liste. Die Liste kann aus einer Datei geladen oder direkt eingegeben werden.",
                "## Vorgehen",
                "1. Quell- und Zielpfad festlegen und mit \"Pfade speichern\" sichern.",
                "2. Liste der gesuchten Namen oder Kennungen bereitstellen.",
                "3. Falls die Kennung nur ein Teil des Dateinamens ist, das passende Trennzeichen angeben.",
                "4. Suchlauf ausführen und das Ergebnis prüfen.",
                "5. Erst danach Kopieren oder Verschieben starten.",
                "## Namenskonflikte",
                "Existiert im Zielordner bereits eine Datei gleichen Namens, erscheint der Konfliktdialog. Zur Auswahl stehen Überspringen, Überschreiben, Quelle umbenennen und Ziel umbenennen. Zusätzlich wird über einen Hashvergleich angezeigt, ob die Dateien tatsächlich identisch sind.",
                "## Doppelte Einträge",
                "Mehrfach vorkommende Listeneinträge werden erkannt und gemeldet, damit dieselbe Datei nicht mehrfach verarbeitet wird."
            }
        };

        private static HilfeThema TabUmbenennen() => new()
        {
            Titel = "Registerkarte: Dateien umbenennen",
            Absaetze = new[]
            {
                "Diese Registerkarte benennt Dateien anhand ihres Inhalts um. Typischer Fall: In einer Textdatei (TXT, INI, LOG, CSV, XML, JSON) steht \"Datum=01.04.2025\", und im Dateinamen IMG_20261112_WA0086_12345 soll der Datumsabschnitt durch 20250401 ersetzt werden. Der Wert kann ebenso aus einer PDF-Datei oder einer Office-Datei (DOCX, XLSX) stammen.",
                "## Vorgehen",
                "1. Quellpfad wählen und den Dateityp festlegen. Die Auswahlliste enthält die gebräuchlichen Vorgaben; alternativ lassen sich eigene Endungen semikolon-getrennt eintippen, z. B. txt;pdf;docx.",
                "2. Festlegen, aus welcher Datei der Wert gelesen wird und mit welchem Schlüssel oder Suchtext er beginnt.",
                "3. Betriebsart wählen: Datumsmodus (Wert wird als Datum erkannt und neu formatiert) oder Textmodus (Wert wird unverändert übernommen).",
                "4. Trennzeichen des Dateinamens angeben, meist der Unterstrich.",
                "5. Zielabschnitt bestimmen - entweder automatisch über ein Muster oder als fester Abschnitt.",
                "6. Vorschau erzeugen und jede Zeile kontrollieren.",
                "7. Umbenennen ausführen.",
                "## Abschnittswahl in Klartext",
                "Statt regulärer Ausdrücke stehen verständliche Optionen zur Verfügung: nur Ziffern, nur Buchstaben, Buchstaben und/oder Ziffern, beliebiger Inhalt sowie - für Sonderfälle - ein eigenes Muster. Zusätzlich lässt sich eine erwartete Länge angeben, damit zum Beispiel nur der achtstellige Datumsblock getroffen wird.",
                "## Richtung der Zählung",
                "Beim festen Abschnitt legen Sie fest, ob vorwärts (vom Anfang des Namens) oder rückwärts (vom Ende des Namens) gezählt wird. Rückwärts ist praktisch, wenn die Anzahl der vorderen Abschnitte schwankt.",
                "## Datumsformat",
                "Das Ausgabeformat ist frei wählbar und folgt der .NET-Schreibweise, zum Beispiel yyyyMMdd für 20250401 oder dd.MM.yyyy für 01.04.2025.",
                "## Gleichnamige Dateien mitumbenennen",
                "Ist diese Option aktiv, werden alle Dateien mit gleichem Namen und anderer Endung mit umbenannt. So bleiben zusammengehörige Dateien wie Beleg.pdf und Beleg.txt weiterhin gepaart.",
                "## Wichtiger Hinweis",
                "Umbenennen lässt sich nicht automatisch rückgängig machen. Prüfen Sie deshalb immer zuerst die Vorschau."
            }
        };

        private static HilfeThema TabInhaltssuche() => new()
        {
            Titel = "Registerkarte: Inhaltssuche",
            Absaetze = new[]
            {
                "Die Inhaltssuche arbeitet wie der Listenverschieber, sucht aber nicht im Dateinamen, sondern im Text innerhalb der Dateien.",
                "## Vorgehen",
                "1. Suchpfad und Zielpfad festlegen.",
                "2. Dateityp festlegen, der durchsucht werden soll.",
                "3. Suchtext eingeben - auch mehrzeilig möglich.",
                "4. Suchmodus wählen.",
                "5. Suchlauf starten und die Trefferliste prüfen.",
                "6. Treffer anschließend kopieren oder verschieben.",
                "## Suchmodi",
                "• Aufeinanderfolgende Zeilen: Der eingegebene Block muss genau so und in dieser Reihenfolge in der Datei vorkommen.",
                "• Alle Begriffe: Jede eingegebene Zeile muss irgendwo in der Datei vorkommen, die Reihenfolge ist egal.",
                "• Mindestens ein Begriff: Es genügt, wenn eine der eingegebenen Zeilen gefunden wird.",
                "## Platzhalter",
                "Der Stern (*) steht für beliebig viele Zeichen, das Fragezeichen (?) für genau ein Zeichen. Beispiel: Rechnung_2025* findet alle Rechnungen aus dem Jahr 2025, Beleg_?.pdf trifft Beleg_1 bis Beleg_9.",
                "## Groß- und Kleinschreibung",
                "Standardmäßig wird die Schreibweise nicht unterschieden. Über die entsprechende Option lässt sich exakte Beachtung einschalten.",
                "## Trefferanzeige",
                "In der Ergebnisliste wird zu jeder Datei ein Textausschnitt rund um die Fundstelle angezeigt, damit sich der Treffer schnell einordnen lässt.",
                "## Abbrechen und Fortschritt",
                "Der Suchlauf zeigt seinen Fortschritt an und kann jederzeit abgebrochen werden. Bereits abgeschlossene Dateioperationen bleiben dabei erhalten.",
                "## Namenskonflikte",
                "Für das Kopieren und Verschieben legen Sie vorab fest, wie mit bereits vorhandenen Zieldateien umgegangen wird. Beim automatischen Umbenennen wird ein Zähler an den Dateinamen angehängt."
            }
        };

        private static HilfeThema Dateiformate() => new()
        {
            Titel = "Unterstützte Dateiformate",
            Absaetze = new[]
            {
                "Für die inhaltsbasierten Funktionen (Umbenennen und Inhaltssuche) muss der Text der Datei lesbar sein.",
                "## Dateityp auswählen",
                "In beiden Registerkarten wird der Dateityp über eine Auswahlliste festgelegt. Sie enthält die gebräuchlichen Vorgaben, etwa 'Nur PDF-Dateien' oder 'Textdateien'. Das Feld ist zugleich beschreibbar: Wer eine ungewöhnliche Zusammenstellung braucht, tippt die Endungen einfach semikolon-getrennt ein, zum Beispiel txt;pdf;docx. Punkt und Sternchen davor sind erlaubt, aber nicht nötig - txt, .txt und *.txt führen zum selben Ergebnis.",
                "Der Eintrag 'Alle durchsuchbaren Dateien (*.*)' erfasst sämtliche unterstützten Dateitypen auf einmal. Das ist hilfreich, wenn im Vorfeld nicht bekannt ist, in welchem Format die gesuchte Angabe vorliegt. Nicht lesbare Dateien werden dabei automatisch übersprungen.",
                "## Direkt lesbare Textformate",
                "TXT, INI, LOG, CSV, XML und JSON werden unmittelbar als Text gelesen.",
                "## Weitere Formate",
                "• PDF - Textextraktion über PdfPig. Voraussetzung ist ein durchsuchbares PDF.",
                "• DOCX - Word-Dokumente über DocumentFormat.OpenXml.",
                "• XLSX - Excel-Arbeitsmappen über DocumentFormat.OpenXml.",
                "## Nicht unterstützt",
                "Gescannte PDF-Dateien ohne Texterkennung enthalten nur Bilddaten und liefern keinen durchsuchbaren Text. Hierfür wäre eine vorgelagerte OCR-Verarbeitung nötig. Ebenso werden die alten Formate DOC und XLS sowie passwortgeschützte Dateien nicht gelesen."
            }
        };

        private static HilfeThema Exportieren() => new()
        {
            Titel = "Listen exportieren",
            Absaetze = new[]
            {
                "Über 'Datei > Exportieren' lassen sich die Ergebnisse eines Laufs als Textdatei oder als CSV sichern.",
                "## Welche Listen stehen zur Verfügung?",
                "Es werden immer drei zueinander passende Listen angeboten. Welche das sind, richtet sich nach der zuletzt ausgeführten Aktion:",
                "• Nach einem Suchlauf: 'Gefundene Dateien', 'Nicht gefundene Dateien' und 'Alle Dateien'.",
                "• Nach dem Kopieren: 'Kopierte Dateien', 'Nicht kopierte Dateien' und 'Alle Dateien'.",
                "• Nach dem Verschieben: 'Verschobene Dateien', 'Nicht verschobene Dateien' und 'Alle Dateien'.",
                "'Alle Dateien' fasst beide Listen zu einer gemeinsamen Ausgabe zusammen.",
                "Zusätzlich lassen sich das Suchprotokoll, das Kopierprotokoll und das komplette Protokoll ausgeben. Listen ohne Einträge sind ausgegraut.",
                "## Namen am Trennzeichen kürzen",
                "Häufig enthalten Dateinamen mehr Angaben als für die Weiterverarbeitung nötig sind. Mit dieser Option wird der Name vor dem Export an einem Trennzeichen zerlegt, und es werden nur die gewünschten Abschnitte übernommen.",
                "• Trennzeichen: das Zeichen, an dem zerlegt wird, meist der Unterstrich.",
                "• Abschnitte behalten: wie viele Abschnitte übernommen werden.",
                "• Richtung: vorwärts zählt vom Anfang des Namens, rückwärts vom Ende. Rückwärts ist praktisch, wenn die Anzahl der vorderen Abschnitte schwankt.",
                "• Dateiendung vorher entfernen: schneidet die Endung ab, bevor zerlegt wird.",
                "Ein Beispiel wird im Dialog laufend mitgerechnet, sodass sich die Einstellung sofort überprüfen lässt. Enthält ein Name das Trennzeichen nicht, bleibt er unverändert.",
                "## Format und Kodierung",
                "TXT schreibt eine Zeile je Eintrag. CSV ergänzt eine Kopfzeile mit Dateiname und Zeitstempel. Für Protokolle steht nur TXT zur Verfügung; das Kürzen ist dort ebenfalls deaktiviert, da es sich nicht um reine Dateinamen handelt.",
                "Als Kodierung stehen ANSI (Windows-1252) und UTF-8 zur Wahl. ANSI passt zu älteren Programmen, UTF-8 gibt Umlaute und Sonderzeichen zuverlässig wieder."
            }
        };

        private static HilfeThema Konfiguration() => new()
        {
            Titel = "Einstellungen und Konfigurationsdatei",
            Absaetze = new[]
            {
                "Alle Pfade und Optionen sämtlicher Registerkarten werden in einer JSON-Datei gespeichert und beim nächsten Start automatisch geladen.",
                "## Speicherort",
                "%AppData%\\Roaming\\Listenverschieber\\config.json",
                "Auf diesem Rechner entspricht das dem Pfad: " + AnonymisierterKonfigurationsPfad(),
                "## Speichern und Laden",
                "Über die Schaltflächen zum Speichern und Laden der Pfade werden die aktuellen Einstellungen gesichert beziehungsweise erneut eingelesen.",
                "## Übertragung auf andere Rechner",
                "Die Konfigurationsdatei lässt sich kopieren, um dieselben Einstellungen an einem anderen Arbeitsplatz zu verwenden. Achten Sie dabei darauf, dass die enthaltenen Pfade auch dort existieren.",
                "## Zurücksetzen",
                "Wird die Konfigurationsdatei gelöscht, startet das Programm wieder mit den Standardwerten."
            }
        };

        /// <summary>
        /// Liefert den Pfad der Konfigurationsdatei, wobei der Windows-Benutzername
        /// durch "Profil" ersetzt wird. So laesst sich der Hilfetext weitergeben,
        /// ohne den Namen des Anwenders preiszugeben.
        /// </summary>
        private static string AnonymisierterKonfigurationsPfad()
        {
            var pfad = ProgrammInfo.KonfigurationsPfad;
            var benutzer = Environment.UserName;

            return string.IsNullOrEmpty(benutzer)
                ? pfad
                : pfad.Replace($"\\{benutzer}\\", "\\Profil\\", StringComparison.OrdinalIgnoreCase);
        }

        private static HilfeThema Problembehandlung() => new()
        {
            Titel = "Problembehandlung",
            Absaetze = new[]
            {
                "## Es werden keine Treffer gefunden",
                "Prüfen Sie zuerst die angegebenen Dateiendungen, danach den Suchmodus. Bei PDF-Dateien kann es sich um ein gescanntes Dokument ohne Textebene handeln.",
                "## Umlaute werden falsch dargestellt",
                "Ältere Textdateien nutzen mitunter den Zeichensatz Windows-1252. Dieser wird unterstützt; bei ungewöhnlichen Zeichensätzen kann die Datei vorab in UTF-8 konvertiert werden.",
                "## Zugriff verweigert",
                "Die Datei ist entweder in einem anderen Programm geöffnet oder es fehlen Schreibrechte im Zielordner. Schließen Sie das andere Programm und prüfen Sie die Berechtigungen des Ordners.",
                "## Der Vorgang dauert sehr lange",
                "Die Textextraktion aus PDF-, Word- und Excel-Dateien ist deutlich aufwendiger als das Lesen reiner Textdateien. Schränken Sie die Dateiendungen ein oder verarbeiten Sie den Bestand in Teilmengen.",
                "## Pfad wird nicht gefunden",
                "Bei Netzlaufwerken UNC-Pfade verwenden. Sehr lange Pfade können ebenfalls Probleme bereiten; kürzen Sie in diesem Fall die Ordnerstruktur.",
                "## Das Protokoll hilft weiter",
                "Jede Registerkarte führt ein eigenes Protokoll. Dort steht zu jedem Vorgang, welche Datei verarbeitet, übersprungen oder mit Fehler abgebrochen wurde."
            }
        };
    }
}
