using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Listenverschieber
{
    /// <summary>
    /// Benutzerfreundliche Beschreibung, wie der zu ersetzende Abschnitt aussieht.
    /// </summary>
    public enum AbschnittMusterTyp
    {
        NurZiffern = 0,
        NurBuchstaben = 1,
        BuchstabenUndZiffern = 2,
        Beliebig = 3,
        EigenesMuster = 4
    }

    /// <summary>
    /// Optionen fuer die inhaltsbasierte Umbenennung.
    /// </summary>
    public class UmbenennungsOptionen
    {
        /// <summary>Schluessel im Dateiinhalt, z.B. "Datum=" oder "Datum".</summary>
        public string Suchschluessel { get; set; } = "Datum=";

        /// <summary>Trennzeichen im Dateinamen, z.B. "_".</summary>
        public string Trennzeichen { get; set; } = "_";

        /// <summary>true = Abschnitt automatisch anhand des Quellformats bestimmen.</summary>
        public bool AbschnittAuto { get; set; } = true;

        /// <summary>1-basierte Abschnittsnummer, wenn AbschnittAuto = false.</summary>
        public int AbschnittNummer { get; set; } = 4;

        /// <summary>true = Abschnitte von hinten zählen (Abschnitt 1 = letzter Abschnitt).</summary>
        public bool AbschnittVonHinten { get; set; } = false;

        /// <summary>.NET-Datumsmuster des Werts im Dateiinhalt, z.B. "dd.MM.yyyy".</summary>
        public string QuellFormatInhalt { get; set; } = "dd.MM.yyyy";

        /// <summary>.NET-Datumsmuster des Abschnitts im Dateinamen, z.B. "yyyyMMdd".</summary>
        public string QuellFormatDateiname { get; set; } = "yyyyMMdd";

        /// <summary>Zielformat fuer den neuen Abschnitt im Dateinamen.</summary>
        public string ZielFormatDateiname { get; set; } = "yyyyMMdd";

        /// <summary>Wenn true, wird der Wert als Datum interpretiert und umformatiert.</summary>
        public bool AlsDatumFormatieren { get; set; } = true;

        /// <summary>Regulärer Ausdruck zur Auto-Erkennung des Abschnitts im Text-Modus.</summary>
        public string AutoMuster { get; set; } = @"^\d+$";

        /// <summary>Benutzerfreundliche Beschreibung des Abschnitts (Text-Modus).</summary>
        public AbschnittMusterTyp MusterTyp { get; set; } = AbschnittMusterTyp.NurZiffern;

        /// <summary>Optionale genaue Zeichenanzahl des Abschnitts (0 = beliebig).</summary>
        public int MusterLaenge { get; set; } = 0;

        /// <summary>
        /// Baut aus MusterTyp und MusterLaenge den passenden regulären Ausdruck.
        /// </summary>
        public string EffektivesMuster()
        {
            if (MusterTyp == AbschnittMusterTyp.EigenesMuster)
            {
                return AutoMuster;
            }

            string zeichenklasse = MusterTyp switch
            {
                AbschnittMusterTyp.NurZiffern => "[0-9]",
                AbschnittMusterTyp.NurBuchstaben => "[A-Za-z\u00c4\u00d6\u00dc\u00e4\u00f6\u00fc\u00df]",
                AbschnittMusterTyp.BuchstabenUndZiffern => "[A-Za-z0-9\u00c4\u00d6\u00dc\u00e4\u00f6\u00fc\u00df]",
                _ => "."
            };

            string anzahl = MusterLaenge > 0 ? $"{{{MusterLaenge}}}" : "+";
            return $"^{zeichenklasse}{anzahl}$";
        }

        /// <summary>Klartext-Beschreibung des Musters für Meldungen.</summary>
        public string MusterBeschreibung()
        {
            if (MusterTyp == AbschnittMusterTyp.EigenesMuster)
            {
                return $"Muster '{AutoMuster}'";
            }

            string art = MusterTyp switch
            {
                AbschnittMusterTyp.NurZiffern => "nur Ziffern",
                AbschnittMusterTyp.NurBuchstaben => "nur Buchstaben",
                AbschnittMusterTyp.BuchstabenUndZiffern => "Buchstaben und/oder Ziffern",
                _ => "beliebiger Inhalt"
            };

            return MusterLaenge > 0 ? $"{art}, genau {MusterLaenge} Zeichen" : art;
        }
    }

    /// <summary>
    /// Ein Eintrag der Umbenennungs-Vorschau.
    /// </summary>
    public class UmbenennungsEintrag
    {
        public string BasisName { get; set; } = "";
        public string NeuerBasisName { get; set; } = "";
        public string GefundenerWert { get; set; } = "";
        public string Quelldatei { get; set; } = "";
        public string Status { get; set; } = "";
        public string AnzeigePfad { get; set; } = "";
        public string Ordner { get; set; } = "";
        public bool Umbenennbar { get; set; }
        public List<string> BetroffeneDateien { get; set; } = new List<string>();

        public string BetroffeneDateienAnzeige => string.Join(", ", BetroffeneDateien.Select(Path.GetFileName));
    }

    /// <summary>
    /// Extrahiert Werte aus Dateiinhalten und bildet daraus neue Dateinamen.
    /// </summary>
    public static class UmbenennungsLogik
    {
        /// <summary>
        /// Sucht im Text den Wert hinter dem Suchschluessel (z.B. "Datum=01.04.2025" -> "01.04.2025").
        /// </summary>
        public static string? WertAusInhalt(string inhalt, string suchschluessel)
        {
            if (string.IsNullOrWhiteSpace(inhalt) || string.IsNullOrWhiteSpace(suchschluessel))
            {
                return null;
            }

            // Schluessel ohne abschliessendes Trennzeichen, damit "Datum=" und "Datum" beide gehen
            string schluessel = suchschluessel.Trim();
            string trenner = "";
            if (schluessel.Length > 0 && !char.IsLetterOrDigit(schluessel[^1]))
            {
                trenner = schluessel[^1].ToString();
                schluessel = schluessel[..^1].Trim();
            }

            string muster = Regex.Escape(schluessel)
                             + @"\s*"
                             + (trenner.Length > 0 ? Regex.Escape(trenner) : "[:=]")
                             + @"\s*(?<wert>[^\r\n;]+)";

            var treffer = Regex.Match(inhalt, muster, RegexOptions.IgnoreCase);
            if (!treffer.Success)
            {
                return null;
            }

            return treffer.Groups["wert"].Value.Trim();
        }

        /// <summary>
        /// Wandelt den gefundenen Wert in das Zielformat des Dateinamens um.
        /// </summary>
        public static string? WertFormatieren(string wert, UmbenennungsOptionen optionen, out string? fehler)
        {
            fehler = null;
            if (string.IsNullOrWhiteSpace(wert))
            {
                fehler = "Leerer Wert";
                return null;
            }

            if (!optionen.AlsDatumFormatieren)
            {
                return wert.Trim();
            }

            var kultur = CultureInfo.InvariantCulture;
            var formate = new[] { optionen.QuellFormatInhalt };

            if (!DateTime.TryParseExact(wert.Trim(), formate, kultur, DateTimeStyles.None, out var datum)
                && !DateTime.TryParse(wert.Trim(), new CultureInfo("de-DE"), DateTimeStyles.None, out datum))
            {
                fehler = $"Wert '{wert}' passt nicht zum Format '{optionen.QuellFormatInhalt}'";
                return null;
            }

            return datum.ToString(optionen.ZielFormatDateiname, kultur);
        }

        /// <summary>
        /// Ersetzt den Ziel-Abschnitt im Dateinamen durch den neuen Wert.
        /// Gibt null zurueck, wenn kein passender Abschnitt gefunden wurde.
        /// </summary>
        public static string? NeuenNamenBilden(string basisName, string neuerWert, UmbenennungsOptionen optionen, out string? fehler)
        {
            fehler = null;
            string trennzeichen = string.IsNullOrEmpty(optionen.Trennzeichen) ? "_" : optionen.Trennzeichen;
            var abschnitte = basisName.Split(new[] { trennzeichen }, StringSplitOptions.None);

            if (abschnitte.Length < 2)
            {
                fehler = $"Dateiname enthaelt kein Trennzeichen '{trennzeichen}'";
                return null;
            }

            int index = -1;

            if (optionen.AbschnittAuto)
            {
                // Ersten Abschnitt suchen, der sich mit dem Quellformat des Dateinamens als Datum lesen laesst
                for (int i = 0; i < abschnitte.Length; i++)
                {
                    if (AbschnittPasst(abschnitte[i], optionen))
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    fehler = optionen.AlsDatumFormatieren
                        ? $"Kein Abschnitt im Format '{optionen.QuellFormatDateiname}' gefunden"
                        : $"Kein Abschnitt gefunden mit: {optionen.MusterBeschreibung()}";
                    return null;
                }
            }
            else
            {
                index = optionen.AbschnittVonHinten
                    ? abschnitte.Length - optionen.AbschnittNummer
                    : optionen.AbschnittNummer - 1;

                if (index < 0 || index >= abschnitte.Length)
                {
                    string richtung = optionen.AbschnittVonHinten ? "von hinten" : "von vorne";
                    fehler = $"Abschnitt {optionen.AbschnittNummer} ({richtung}) existiert nicht ({abschnitte.Length} Abschnitte)";
                    return null;
                }
            }

            if (abschnitte[index] == neuerWert)
            {
                fehler = "Bereits korrekt";
                return null;
            }

            abschnitte[index] = neuerWert;
            return string.Join(trennzeichen, abschnitte);
        }

        private static bool AbschnittPasst(string abschnitt, UmbenennungsOptionen optionen)
        {
            if (!optionen.AlsDatumFormatieren)
            {
                string muster = optionen.EffektivesMuster();
                if (string.IsNullOrWhiteSpace(muster))
                {
                    return false;
                }

                try
                {
                    return Regex.IsMatch(abschnitt.Trim(), muster, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    // Ungueltiges Muster - kein Treffer
                    return false;
                }
            }

            // Ein Abschnitt kann noch Zusatztext enthalten (z.B. "20261112"), daher exaktes Parsen
            return DateTime.TryParseExact(
                abschnitt.Trim(),
                optionen.QuellFormatDateiname,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }
    }
}
