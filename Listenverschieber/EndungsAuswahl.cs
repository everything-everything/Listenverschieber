using System.IO;

namespace Listenverschieber
{
    /// <summary>
    /// Ein Eintrag der Dateityp-Auswahlliste.
    /// Eigene Klasse statt Tupel, weil WPF ueber DisplayMemberPath echte Eigenschaften benoetigt.
    /// </summary>
    public sealed class EndungsVorgabe
    {
        public EndungsVorgabe(string anzeige, string wert)
        {
            Anzeige = anzeige;
            Wert = wert;
        }

        /// <summary>Beschriftung in der Auswahlliste.</summary>
        public string Anzeige { get; }

        /// <summary>Die dahinterliegende Endungsangabe, z.B. "txt;ini;pdf".</summary>
        public string Wert { get; }

        public override string ToString() => Anzeige;
    }

    /// <summary>
    /// Gemeinsame Auswertung der Dateiendungs-Eingabe fuer die Registerkarten
    /// "Dateien umbenennen" und "Inhaltssuche".
    ///
    /// Unterstuetzt sowohl vorgegebene Auswahleintraege (Dropdown) als auch
    /// freie Eingaben wie "ini;txt;pdf". Der Platzhalter "*.*" bzw. "*" steht
    /// fuer alle inhaltlich durchsuchbaren Dateitypen.
    /// </summary>
    public static class EndungsAuswahl
    {
        /// <summary>Kennzeichnet die Auswahl "alle durchsuchbaren Dateitypen".</summary>
        public const string AlleKennung = "*.*";

        /// <summary>
        /// Vorgegebene Eintraege fuer die Auswahlliste. Der erste Eintrag ist die
        /// Sammelauswahl, danach folgen Gruppen und einzelne Dateitypen.
        /// </summary>
        public static readonly EndungsVorgabe[] Vorgaben =
        {
            new("Alle durchsuchbaren Dateien (*.*)", AlleKennung),
            new("Textdateien (txt;ini;log;csv;xml;json)", "txt;ini;log;csv;xml;json"),
            new("Office-Dateien (pdf;docx;xlsx)", "pdf;docx;xlsx"),
            new("Nur Textdateien (txt)", "txt"),
            new("Nur INI-Dateien (ini)", "ini"),
            new("Nur PDF-Dateien (pdf)", "pdf"),
            new("Nur Word-Dateien (docx)", "docx"),
            new("Nur Excel-Dateien (xlsx)", "xlsx"),
            new("Nur Protokolldateien (log)", "log"),
            new("Nur CSV-Dateien (csv)", "csv")
        };

        /// <summary>
        /// Prueft, ob die Eingabe fuer alle durchsuchbaren Dateitypen steht.
        /// Erkannt werden "*", "*.*", "alle" und die leere Eingabe.
        /// </summary>
        public static bool IstAlle(string? eingabe)
        {
            var text = eingabe?.Trim() ?? string.Empty;
            return text.Length == 0
                || text == "*"
                || text == "*.*"
                || text == ".*"
                || text.Equals("alle", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Wandelt die Eingabe in eine Menge normalisierter Endungen um (jeweils mit fuehrendem Punkt).
        /// Bei der Sammelauswahl werden alle durchsuchbaren Endungen zurueckgegeben.
        /// </summary>
        public static HashSet<string> Auswerten(string? eingabe)
        {
            if (IstAlle(eingabe))
            {
                return DateiInhaltsLeser.UnterstuetzteEndungen.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            return eingabe!
                .Split(new[] { ';', ',', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalisieren)
                .Where(e => e.Length > 1)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Macht aus Eingaben wie "pdf", ".pdf", "*.pdf" oder " PDF " einheitlich ".pdf".
        /// </summary>
        private static string Normalisieren(string endung)
            => "." + endung.Trim().TrimStart('*').TrimStart('.').Trim().ToLowerInvariant();

        /// <summary>
        /// Ermittelt, ob eine Datei anhand ihrer Endung verarbeitet werden soll.
        /// </summary>
        public static bool Passt(string dateiPfad, HashSet<string> endungen)
            => endungen.Contains(Path.GetExtension(dateiPfad)) && DateiInhaltsLeser.IstDurchsuchbar(dateiPfad);

        /// <summary>
        /// Liefert einen lesbaren Hinweis zur aktuellen Auswahl, etwa fuer das Protokoll.
        /// </summary>
        public static string Beschreiben(string? eingabe)
        {
            if (IstAlle(eingabe))
            {
                return "alle durchsuchbaren Dateitypen";
            }

            var endungen = Auswerten(eingabe);
            return endungen.Count == 0
                ? "keine gültige Endung"
                : string.Join(", ", endungen.OrderBy(e => e));
        }
    }
}
