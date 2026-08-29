using System.Text;
using System.Text.RegularExpressions;

namespace Listenverschieber
{
    /// <summary>
    /// Verknuepfung der eingegebenen Suchzeilen.
    /// </summary>
    public enum InhaltsSuchModus
    {
        /// <summary>Die Zeilen muessen direkt aufeinanderfolgend im Text stehen.</summary>
        ZeilenblockInFolge = 0,

        /// <summary>Alle Zeilen muessen vorkommen, die Reihenfolge ist egal.</summary>
        AlleBegriffe = 1,

        /// <summary>Mindestens eine der Zeilen muss vorkommen.</summary>
        MindestensEinBegriff = 2
    }

    /// <summary>
    /// Prueft, ob der Textinhalt einer Datei den gesuchten Kriterien entspricht.
    /// </summary>
    public static class InhaltsSuche
    {
        /// <summary>
        /// Zerlegt die Benutzereingabe in einzelne Suchzeilen (Leerzeilen werden verworfen).
        /// </summary>
        public static List<string> SuchzeilenLesen(string eingabe)
        {
            if (string.IsNullOrWhiteSpace(eingabe))
            {
                return new List<string>();
            }

            return eingabe
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(z => z.Trim())
                .Where(z => z.Length > 0)
                .ToList();
        }

        /// <summary>
        /// Prueft den Dateiinhalt gegen die Suchkriterien.
        /// </summary>
        /// <param name="inhalt">Der extrahierte Textinhalt der Datei.</param>
        /// <param name="suchzeilen">Die einzelnen Suchzeilen.</param>
        /// <param name="modus">Verknuepfung der Suchzeilen.</param>
        /// <param name="grossKleinBeachten">true = Gross-/Kleinschreibung ist relevant.</param>
        /// <param name="platzhalter">true = * und ? werden als Platzhalter interpretiert.</param>
        /// <param name="trefferText">Die erste passende Fundstelle (fuer die Anzeige).</param>
        public static bool Passt(
            string inhalt,
            List<string> suchzeilen,
            InhaltsSuchModus modus,
            bool grossKleinBeachten,
            bool platzhalter,
            out string trefferText)
        {
            trefferText = "";

            if (string.IsNullOrEmpty(inhalt) || suchzeilen.Count == 0)
            {
                return false;
            }

            var vergleich = grossKleinBeachten
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            var zeilen = inhalt
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(z => z.Trim())
                .ToList();

            return modus switch
            {
                InhaltsSuchModus.ZeilenblockInFolge =>
                    ZeilenblockSuchen(zeilen, suchzeilen, vergleich, platzhalter, out trefferText),
                InhaltsSuchModus.AlleBegriffe =>
                    AlleBegriffeSuchen(inhalt, zeilen, suchzeilen, vergleich, platzhalter, out trefferText),
                _ =>
                    EinBegriffSuchen(inhalt, zeilen, suchzeilen, vergleich, platzhalter, out trefferText)
            };
        }

        /// <summary>
        /// Sucht die Suchzeilen als zusammenhaengenden Block aufeinanderfolgender Zeilen.
        /// Leerzeilen im Dokument zwischen den Treffern werden uebersprungen.
        /// </summary>
        private static bool ZeilenblockSuchen(
            List<string> zeilen,
            List<string> suchzeilen,
            StringComparison vergleich,
            bool platzhalter,
            out string trefferText)
        {
            trefferText = "";

            // Nur nicht-leere Zeilen betrachten, damit Leerzeilen den Block nicht zerreissen
            var relevanteZeilen = new List<(int Nummer, string Text)>();
            for (int i = 0; i < zeilen.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(zeilen[i]))
                {
                    relevanteZeilen.Add((i + 1, zeilen[i]));
                }
            }

            for (int start = 0; start + suchzeilen.Count <= relevanteZeilen.Count; start++)
            {
                bool alleTreffen = true;

                for (int versatz = 0; versatz < suchzeilen.Count; versatz++)
                {
                    if (!ZeileTrifft(relevanteZeilen[start + versatz].Text, suchzeilen[versatz], vergleich, platzhalter))
                    {
                        alleTreffen = false;
                        break;
                    }
                }

                if (alleTreffen)
                {
                    var sb = new StringBuilder();
                    sb.Append($"Zeile {relevanteZeilen[start].Nummer}: ");
                    sb.Append(string.Join(" | ", relevanteZeilen
                        .Skip(start)
                        .Take(suchzeilen.Count)
                        .Select(z => z.Text)));
                    trefferText = sb.ToString();
                    return true;
                }
            }

            return false;
        }

        private static bool AlleBegriffeSuchen(
            string inhalt,
            List<string> zeilen,
            List<string> suchzeilen,
            StringComparison vergleich,
            bool platzhalter,
            out string trefferText)
        {
            trefferText = "";
            var treffer = new List<string>();

            foreach (var suchzeile in suchzeilen)
            {
                if (!BegriffGefunden(inhalt, zeilen, suchzeile, vergleich, platzhalter, out string fund))
                {
                    return false;
                }
                treffer.Add(fund);
            }

            trefferText = string.Join(" | ", treffer);
            return true;
        }

        private static bool EinBegriffSuchen(
            string inhalt,
            List<string> zeilen,
            List<string> suchzeilen,
            StringComparison vergleich,
            bool platzhalter,
            out string trefferText)
        {
            foreach (var suchzeile in suchzeilen)
            {
                if (BegriffGefunden(inhalt, zeilen, suchzeile, vergleich, platzhalter, out trefferText))
                {
                    return true;
                }
            }

            trefferText = "";
            return false;
        }

        /// <summary>
        /// Prueft, ob ein einzelner Begriff irgendwo im Text vorkommt.
        /// </summary>
        private static bool BegriffGefunden(
            string inhalt,
            List<string> zeilen,
            string suchbegriff,
            StringComparison vergleich,
            bool platzhalter,
            out string trefferText)
        {
            trefferText = "";

            if (!platzhalter)
            {
                int index = inhalt.IndexOf(suchbegriff, vergleich);
                if (index < 0)
                {
                    return false;
                }

                trefferText = AusschnittUm(inhalt, index, suchbegriff.Length);
                return true;
            }

            // Mit Platzhaltern zeilenweise pruefen
            var regex = PlatzhalterRegex(suchbegriff, vergleich, verankert: false);
            for (int i = 0; i < zeilen.Count; i++)
            {
                var treffer = regex.Match(zeilen[i]);
                if (treffer.Success)
                {
                    trefferText = $"Zeile {i + 1}: {zeilen[i]}";
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Prueft eine einzelne Dokumentzeile gegen eine Suchzeile.
        /// </summary>
        private static bool ZeileTrifft(string zeile, string suchzeile, StringComparison vergleich, bool platzhalter)
        {
            if (!platzhalter)
            {
                return zeile.Contains(suchzeile, vergleich);
            }

            return PlatzhalterRegex(suchzeile, vergleich, verankert: false).IsMatch(zeile);
        }

        /// <summary>
        /// Wandelt einen Suchbegriff mit * und ? in einen regulaeren Ausdruck um.
        /// </summary>
        private static Regex PlatzhalterRegex(string suchbegriff, StringComparison vergleich, bool verankert)
        {
            string muster = Regex.Escape(suchbegriff)
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

            if (verankert)
            {
                muster = "^" + muster + "$";
            }

            var optionen = vergleich == StringComparison.Ordinal
                ? RegexOptions.None
                : RegexOptions.IgnoreCase;

            return new Regex(muster, optionen);
        }

        /// <summary>
        /// Liefert einen kurzen Textausschnitt rund um die Fundstelle.
        /// </summary>
        private static string AusschnittUm(string inhalt, int index, int laenge)
        {
            const int rand = 40;
            int start = Math.Max(0, index - rand);
            int ende = Math.Min(inhalt.Length, index + laenge + rand);

            string ausschnitt = inhalt[start..ende]
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            // Mehrfache Leerzeichen zusammenfassen
            ausschnitt = Regex.Replace(ausschnitt, @"\s{2,}", " ");

            if (start > 0)
            {
                ausschnitt = "..." + ausschnitt;
            }
            if (ende < inhalt.Length)
            {
                ausschnitt += "...";
            }

            return ausschnitt;
        }
    }
}
