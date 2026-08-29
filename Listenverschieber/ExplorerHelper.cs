using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Listenverschieber
{
    /// <summary>
    /// Hilfsklasse für Explorer-Funktionen
    /// </summary>
    public static class ExplorerHelper
    {
        /// <summary>
        /// Öffnet eine Datei im Windows Explorer und markiert sie
        /// </summary>
        public static void OeffneImExplorer(string dateipfad)
        {
            if (string.IsNullOrWhiteSpace(dateipfad))
                return;

            try
            {
                if (File.Exists(dateipfad))
                {
                    // Öffne Explorer und markiere die Datei
                    Process.Start("explorer.exe", $"/select,\"{dateipfad}\"");
                }
                else if (Directory.Exists(dateipfad))
                {
                    // Öffne das Verzeichnis
                    Process.Start("explorer.exe", $"\"{dateipfad}\"");
                }
                else
                {
                    // Versuche das übergeordnete Verzeichnis zu öffnen
                    string? verzeichnis = Path.GetDirectoryName(dateipfad);
                    if (!string.IsNullOrEmpty(verzeichnis) && Directory.Exists(verzeichnis))
                    {
                        Process.Start("explorer.exe", $"\"{verzeichnis}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Fehler beim Öffnen im Explorer:\n\n{ex.Message}",
                    "Fehler",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Extrahiert einen Dateinamen aus einer Log-Nachricht
        /// </summary>
        public static string? ExtrahiereDateinameAusLog(string logZeile)
        {
            if (string.IsNullOrWhiteSpace(logZeile))
                return null;

            // Entferne Zeitstempel am Anfang (Format: HH:mm:ss - )
            var ohneZeitstempel = Regex.Replace(logZeile, @"^\d{2}:\d{2}:\d{2}\s*-\s*", "");

            // Suche nach Dateinamen in verschiedenen Formaten
            var patterns = new[]
            {
                @"(?:Verschoben|Kopiert|Gefunden|Zurückgeschoben|Nicht gefunden):\s+(.+?)(?:\s+\(|$)",
                @"\[Suchlauf\].*?:\s+(.+?)(?:\s+\(|$)",
                @"Übersprungen.*?:\s+(.+?)(?:\s+\(|$)",
                @"FEHLER.*?:\s+(.+?)(?:\s+-|$)",
                @"(?:^|:\s+)([^:]+\.[a-zA-Z0-9]{2,5})(?:\s|$)"  // Generischer Dateiname mit Endung
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(ohneZeitstempel, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var dateiname = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(dateiname))
                        return dateiname;
                }
            }

            return null;
        }

        /// <summary>
        /// Findet den vollständigen Pfad zu einer Datei in den angegebenen Verzeichnissen
        /// </summary>
        public static string? FindeDatei(string dateiname, params string[] suchpfade)
        {
            if (string.IsNullOrWhiteSpace(dateiname))
                return null;

            // Wenn bereits ein vollständiger Pfad, prüfe direkt
            if (Path.IsPathRooted(dateiname) && File.Exists(dateiname))
                return dateiname;

            // Durchsuche alle angegebenen Pfade
            foreach (var pfad in suchpfade.Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)))
            {
                var vollstaendigerPfad = Path.Combine(pfad, dateiname);
                if (File.Exists(vollstaendigerPfad))
                    return vollstaendigerPfad;

                // Suche auch in Move-Unterverzeichnis
                var movePfad = Path.Combine(pfad, "Move", dateiname);
                if (File.Exists(movePfad))
                    return movePfad;
            }

            return null;
        }
    }
}
