using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Listenverschieber
{
    /// <summary>
    /// Ein indizierter Dateieintrag: Kenndaten der Datei sowie die aus ihrem
    /// Inhalt gewonnenen Schluessel/Wert-Paare und die Inhaltspruefsumme.
    /// </summary>
    public sealed class IndexEintrag
    {
        public string Pfad { get; set; } = "";
        public long Groesse { get; set; }
        public long GeaendertUtcTicks { get; set; }

        /// <summary>Pruefsumme ueber den normalisierten Textinhalt.</summary>
        public string Pruefsumme { get; set; } = "";

        /// <summary>Alle erkannten Schluessel/Wert-Paare (Schluessel bereits normalisiert).</summary>
        public Dictionary<string, string> Paare { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Dateiname => Path.GetFileName(Pfad);
    }

    /// <summary>Fortschrittsmeldung des Indexaufbaus.</summary>
    public sealed class IndexFortschritt
    {
        public int Verarbeitet { get; set; }
        public int Gesamt { get; set; }
        public int NeuGelesen { get; set; }
        public string AktuelleDatei { get; set; } = "";
    }

    /// <summary>
    /// Inhaltsindex eines Verzeichnisses.
    ///
    /// Der Index haelt zu jeder indizierten Datei die erkannten Schluessel/Wert-Paare
    /// (z.B. "Datum=14.07.2026", "Belegnummer=5227390") sowie eine Pruefsumme ueber
    /// den gesamten Textinhalt. Damit laesst sich zu einer Quelldatei sehr schnell
    /// die passende Partnerdatei finden, ohne das Zielverzeichnis erneut zu lesen.
    ///
    /// Bewusst ohne Datenbank: Die Ablage erfolgt als kompakte Binaerdatei, die
    /// Nachschlagetabellen werden nach dem Laden im Speicher aufgebaut. Das spart
    /// eine zusaetzliche native Abhaengigkeit und ist fuer die hier benoetigte
    /// exakte Suche schneller als eine SQL-Abfrage.
    /// </summary>
    public sealed class InhaltsIndex
    {
        /// <summary>Kennung am Dateianfang, erkennt fremde oder beschaedigte Dateien.</summary>
        private const string Kennung = "LVIDX";

        /// <summary>
        /// Formatversion. Wird sie erhoeht, verwirft das Laden aeltere Dateien,
        /// statt sie falsch zu deuten - der Index wird dann neu aufgebaut.
        /// </summary>
        private const int Formatversion = 1;

        private readonly List<IndexEintrag> eintraege = new();

        /// <summary>Nachschlagetabelle "schluessel=wert" -> Eintraege. Wird nicht mitgespeichert.</summary>
        private readonly Dictionary<string, List<IndexEintrag>> paarSuche =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Nachschlagetabelle Pruefsumme -> Eintraege. Wird nicht mitgespeichert.</summary>
        private readonly Dictionary<string, List<IndexEintrag>> pruefsummenSuche =
            new(StringComparer.OrdinalIgnoreCase);

        public string Verzeichnis { get; private set; } = "";
        public string Endungen { get; private set; } = "";
        public bool MitUnterordnern { get; private set; }
        public DateTime ErstelltUtc { get; private set; }
        public int Anzahl => eintraege.Count;

        public IReadOnlyList<IndexEintrag> Eintraege => eintraege;

        #region Ablageort

        private static string? indexOrdnerZwischenspeicher;

        /// <summary>
        /// Ablageordner der Indexdateien.
        ///
        /// Fuer den portablen Betrieb liegt der Index im Unterordner "Index"
        /// direkt beim Programm. Nur wenn dort nicht geschrieben werden darf -
        /// etwa bei einer Installation unter "Programme" oder beim Start von
        /// einem schreibgeschuetzten Datentraeger - wird auf den Ordner neben
        /// der Konfiguration ausgewichen, damit die Anwendung nutzbar bleibt.
        /// </summary>
        public static string IndexOrdner
        {
            get
            {
                if (indexOrdnerZwischenspeicher != null)
                {
                    return indexOrdnerZwischenspeicher;
                }

                string beimProgramm = Path.Combine(AppContext.BaseDirectory, "Index");

                if (OrdnerBeschreibbar(beimProgramm))
                {
                    indexOrdnerZwischenspeicher = beimProgramm;
                }
                else
                {
                    indexOrdnerZwischenspeicher = Path.Combine(
                        Path.GetDirectoryName(PfadKonfiguration.ConfigFilePath) ?? Path.GetTempPath(),
                        "Index");
                }

                return indexOrdnerZwischenspeicher;
            }
        }

        /// <summary>
        /// Prueft, ob im angegebenen Ordner tatsaechlich geschrieben werden kann.
        /// Die Schreibrechte werden durch einen echten Schreibversuch ermittelt,
        /// weil eine reine Rechtepruefung unter Windows nicht zuverlaessig ist.
        /// </summary>
        private static bool OrdnerBeschreibbar(string ordner)
        {
            try
            {
                Directory.CreateDirectory(ordner);
                string probe = Path.Combine(ordner, $"schreibtest_{Guid.NewGuid():N}.tmp");
                File.WriteAllBytes(probe, Array.Empty<byte>());
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Ermittelt den Dateinamen des Index fuer ein Verzeichnis. Der Pfad wird
        /// normalisiert und gehasht, damit mehrere Zielverzeichnisse nebeneinander
        /// eigene Indexdateien behalten.
        /// </summary>
        public static string IndexDateiFuer(string verzeichnis)
        {
            string normal = (verzeichnis ?? "")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();

            byte[] roh = SHA256.HashData(Encoding.UTF8.GetBytes(normal));
            string kurz = Convert.ToHexString(roh, 0, 8);

            return Path.Combine(IndexOrdner, $"index_{kurz}.bin");
        }

        #endregion

        #region Inhalt zerlegen

        /// <summary>
        /// Zerlegt einen Textinhalt in Schluessel/Wert-Paare. Erkannt werden
        /// Zeilen der Form "Name=Wert" und "Name: Wert", wie sie in INI-Dateien
        /// und in den meisten Begleitdateien vorkommen.
        ///
        /// Abschnittsueberschriften ("[Abschnitt]") und Kommentarzeilen werden
        /// uebersprungen. Kommt ein Schluessel mehrfach vor, gilt der erste Wert.
        /// </summary>
        public static Dictionary<string, string> PaareAusInhalt(string inhalt)
        {
            var ergebnis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(inhalt))
            {
                return ergebnis;
            }

            foreach (var rohzeile in inhalt.Split('\n'))
            {
                string zeile = rohzeile.Trim('\r', ' ', '\t');
                if (zeile.Length == 0 || zeile[0] == ';' || zeile[0] == '#' || zeile[0] == '[')
                {
                    continue;
                }

                int trenner = zeile.IndexOf('=');
                if (trenner < 0)
                {
                    trenner = zeile.IndexOf(':');
                }

                if (trenner <= 0 || trenner >= zeile.Length - 1)
                {
                    continue;
                }

                string schluessel = zeile.Substring(0, trenner).Trim();
                string wert = zeile.Substring(trenner + 1).Trim();

                if (schluessel.Length == 0 || wert.Length == 0)
                {
                    continue;
                }

                schluessel = SchluesselNormalisieren(schluessel);
                if (schluessel.Length == 0)
                {
                    continue;
                }

                if (!ergebnis.ContainsKey(schluessel))
                {
                    ergebnis[schluessel] = wert;
                }
            }

            return ergebnis;
        }

        /// <summary>
        /// Vereinheitlicht einen Schluesselnamen. Damit ist es gleichgueltig, ob
        /// der Nutzer "Datum", "Datum=" oder "Datum:" eingibt.
        /// </summary>
        public static string SchluesselNormalisieren(string schluessel)
            => (schluessel ?? "").Trim().TrimEnd('=', ':', ' ', '\t').Trim();

        /// <summary>
        /// Bildet eine Pruefsumme ueber den Textinhalt. Zeilenenden und
        /// Randleerzeichen werden vorher vereinheitlicht, damit dieselbe
        /// Information trotz unterschiedlicher Zeilenumbrueche als gleich gilt.
        /// </summary>
        public static string PruefsummeAusInhalt(string inhalt)
        {
            if (string.IsNullOrEmpty(inhalt))
            {
                return "";
            }

            var aufbereitet = new StringBuilder();
            foreach (var rohzeile in inhalt.Split('\n'))
            {
                string zeile = rohzeile.Trim('\r', ' ', '\t');
                if (zeile.Length == 0)
                {
                    continue;
                }
                aufbereitet.Append(zeile).Append('\n');
            }

            byte[] roh = SHA256.HashData(Encoding.UTF8.GetBytes(aufbereitet.ToString()));
            return Convert.ToHexString(roh);
        }

        /// <summary>Normalisiert ein Schluessel/Wert-Paar zum Nachschlageschluessel.</summary>
        private static string PaarSchluessel(string schluessel, string wert)
            => SchluesselNormalisieren(schluessel) + "\u0001" + (wert ?? "").Trim();

        #endregion

        #region Aufbau

        /// <summary>
        /// Baut den Index fuer ein Verzeichnis auf oder aktualisiert ihn.
        ///
        /// Bei <paramref name="vollstaendigNeu"/> = false wird ein vorhandener
        /// Index geladen; Dateien mit unveraenderter Groesse und Aenderungszeit
        /// werden uebernommen, statt erneut gelesen zu werden. Dadurch dauert
        /// jeder weitere Durchlauf nur noch einen Bruchteil.
        /// </summary>
        public static InhaltsIndex Aufbauen(
            string verzeichnis,
            HashSet<string> endungen,
            string endungsText,
            bool mitUnterordnern,
            bool vollstaendigNeu,
            IProgress<IndexFortschritt>? fortschritt,
            CancellationToken abbruch)
        {
            var index = new InhaltsIndex
            {
                Verzeichnis = verzeichnis,
                Endungen = endungsText,
                MitUnterordnern = mitUnterordnern,
                ErstelltUtc = DateTime.UtcNow
            };

            // Bestehenden Index als Grundlage nutzen, damit nur Geaendertes neu gelesen wird
            var bekannt = new Dictionary<string, IndexEintrag>(StringComparer.OrdinalIgnoreCase);
            if (!vollstaendigNeu)
            {
                var alt = Laden(verzeichnis);
                if (alt != null
                    && string.Equals(alt.Endungen, endungsText, StringComparison.OrdinalIgnoreCase)
                    && alt.MitUnterordnern == mitUnterordnern)
                {
                    foreach (var eintrag in alt.eintraege)
                    {
                        bekannt[eintrag.Pfad] = eintrag;
                    }
                }
            }

            var suchOption = mitUnterordnern ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] dateien;
            try
            {
                dateien = Directory.GetFiles(verzeichnis, "*", suchOption)
                    .Where(f => EndungsAuswahl.Passt(f, endungen))
                    .ToArray();
            }
            catch (Exception)
            {
                return index;
            }

            int verarbeitet = 0;
            int neuGelesen = 0;
            var gesammelt = new List<IndexEintrag>(dateien.Length);
            var sperre = new object();

            var optionen = new ParallelOptions
            {
                CancellationToken = abbruch,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            };

            Parallel.ForEach(dateien, optionen, datei =>
            {
                IndexEintrag? eintrag = null;
                bool warNeu = false;

                try
                {
                    var info = new FileInfo(datei);

                    if (bekannt.TryGetValue(datei, out var vorhanden)
                        && vorhanden.Groesse == info.Length
                        && vorhanden.GeaendertUtcTicks == info.LastWriteTimeUtc.Ticks)
                    {
                        // Unveraendert - uebernehmen ohne erneutes Lesen
                        eintrag = vorhanden;
                    }
                    else
                    {
                        var inhalt = DateiInhaltsLeser.LiesText(datei, out _);
                        if (inhalt != null)
                        {
                            eintrag = new IndexEintrag
                            {
                                Pfad = datei,
                                Groesse = info.Length,
                                GeaendertUtcTicks = info.LastWriteTimeUtc.Ticks,
                                Pruefsumme = PruefsummeAusInhalt(inhalt),
                                Paare = PaareAusInhalt(inhalt)
                            };
                            warNeu = true;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Nicht lesbare Dateien werden stillschweigend ausgelassen
                }

                lock (sperre)
                {
                    if (eintrag != null)
                    {
                        gesammelt.Add(eintrag);
                        if (warNeu)
                        {
                            neuGelesen++;
                        }
                    }

                    verarbeitet++;
                    if (fortschritt != null && (verarbeitet % 25 == 0 || verarbeitet == dateien.Length))
                    {
                        fortschritt.Report(new IndexFortschritt
                        {
                            Verarbeitet = verarbeitet,
                            Gesamt = dateien.Length,
                            NeuGelesen = neuGelesen,
                            AktuelleDatei = Path.GetFileName(datei)
                        });
                    }
                }
            });

            index.eintraege.AddRange(gesammelt);
            index.TabellenAufbauen();
            return index;
        }

        /// <summary>Baut die Nachschlagetabellen aus den Eintraegen auf.</summary>
        private void TabellenAufbauen()
        {
            paarSuche.Clear();
            pruefsummenSuche.Clear();

            foreach (var eintrag in eintraege)
            {
                foreach (var paar in eintrag.Paare)
                {
                    string schluessel = PaarSchluessel(paar.Key, paar.Value);
                    if (!paarSuche.TryGetValue(schluessel, out var liste))
                    {
                        liste = new List<IndexEintrag>();
                        paarSuche[schluessel] = liste;
                    }
                    liste.Add(eintrag);
                }

                if (eintrag.Pruefsumme.Length > 0)
                {
                    if (!pruefsummenSuche.TryGetValue(eintrag.Pruefsumme, out var pListe))
                    {
                        pListe = new List<IndexEintrag>();
                        pruefsummenSuche[eintrag.Pruefsumme] = pListe;
                    }
                    pListe.Add(eintrag);
                }
            }
        }

        #endregion

        #region Nachschlagen

        /// <summary>
        /// Sucht alle Eintraege, die saemtliche uebergebenen Schluessel/Wert-Paare
        /// enthalten. Es wird mit der seltensten Bedingung begonnen, damit die
        /// Schnittmenge moeglichst klein startet.
        /// </summary>
        public List<IndexEintrag> FindeNachPaaren(IReadOnlyDictionary<string, string> gesucht)
        {
            if (gesucht.Count == 0)
            {
                return new List<IndexEintrag>();
            }

            List<IndexEintrag>? kleinste = null;
            foreach (var paar in gesucht)
            {
                if (!paarSuche.TryGetValue(PaarSchluessel(paar.Key, paar.Value), out var liste))
                {
                    return new List<IndexEintrag>();
                }

                if (kleinste == null || liste.Count < kleinste.Count)
                {
                    kleinste = liste;
                }
            }

            var ergebnis = new List<IndexEintrag>();
            foreach (var kandidat in kleinste!)
            {
                bool allePassen = true;
                foreach (var paar in gesucht)
                {
                    if (!kandidat.Paare.TryGetValue(SchluesselNormalisieren(paar.Key), out var wert)
                        || !string.Equals(wert.Trim(), paar.Value.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        allePassen = false;
                        break;
                    }
                }

                if (allePassen && !ergebnis.Contains(kandidat))
                {
                    ergebnis.Add(kandidat);
                }
            }

            return ergebnis;
        }

        /// <summary>Sucht alle Eintraege mit identischem Inhalt.</summary>
        public List<IndexEintrag> FindeNachPruefsumme(string pruefsumme)
        {
            if (string.IsNullOrEmpty(pruefsumme) || !pruefsummenSuche.TryGetValue(pruefsumme, out var liste))
            {
                return new List<IndexEintrag>();
            }
            return new List<IndexEintrag>(liste);
        }

        #endregion

        #region Speichern und Laden

        /// <summary>Schreibt den Index als kompakte Binaerdatei.</summary>
        public void Speichern()
        {
            string ziel = IndexDateiFuer(Verzeichnis);
            Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);

            using var strom = new FileStream(ziel, FileMode.Create, FileAccess.Write, FileShare.None);
            using var schreiber = new BinaryWriter(strom, Encoding.UTF8);

            schreiber.Write(Kennung);
            schreiber.Write(Formatversion);
            schreiber.Write(Verzeichnis);
            schreiber.Write(Endungen);
            schreiber.Write(MitUnterordnern);
            schreiber.Write(ErstelltUtc.Ticks);
            schreiber.Write(eintraege.Count);

            foreach (var eintrag in eintraege)
            {
                schreiber.Write(eintrag.Pfad);
                schreiber.Write(eintrag.Groesse);
                schreiber.Write(eintrag.GeaendertUtcTicks);
                schreiber.Write(eintrag.Pruefsumme);
                schreiber.Write(eintrag.Paare.Count);

                foreach (var paar in eintrag.Paare)
                {
                    schreiber.Write(paar.Key);
                    schreiber.Write(paar.Value);
                }
            }
        }

        /// <summary>
        /// Laedt den Index eines Verzeichnisses. Gibt null zurueck, wenn keine
        /// Datei vorliegt oder sie nicht zum erwarteten Format passt.
        /// </summary>
        public static InhaltsIndex? Laden(string verzeichnis)
        {
            string quelle = IndexDateiFuer(verzeichnis);
            if (!File.Exists(quelle))
            {
                return null;
            }

            try
            {
                using var strom = new FileStream(quelle, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var leser = new BinaryReader(strom, Encoding.UTF8);

                if (leser.ReadString() != Kennung || leser.ReadInt32() != Formatversion)
                {
                    return null;
                }

                var index = new InhaltsIndex
                {
                    Verzeichnis = leser.ReadString(),
                    Endungen = leser.ReadString(),
                    MitUnterordnern = leser.ReadBoolean(),
                    ErstelltUtc = new DateTime(leser.ReadInt64(), DateTimeKind.Utc)
                };

                int anzahl = leser.ReadInt32();
                for (int i = 0; i < anzahl; i++)
                {
                    var eintrag = new IndexEintrag
                    {
                        Pfad = leser.ReadString(),
                        Groesse = leser.ReadInt64(),
                        GeaendertUtcTicks = leser.ReadInt64(),
                        Pruefsumme = leser.ReadString()
                    };

                    int paarAnzahl = leser.ReadInt32();
                    for (int p = 0; p < paarAnzahl; p++)
                    {
                        string schluessel = leser.ReadString();
                        string wert = leser.ReadString();
                        eintrag.Paare[schluessel] = wert;
                    }

                    index.eintraege.Add(eintrag);
                }

                index.TabellenAufbauen();
                return index;
            }
            catch (Exception)
            {
                // Beschaedigte oder unvollstaendige Datei - Index wird neu aufgebaut
                return null;
            }
        }

        /// <summary>Loescht die Indexdatei eines Verzeichnisses.</summary>
        public static void Loeschen(string verzeichnis)
        {
            try
            {
                string datei = IndexDateiFuer(verzeichnis);
                if (File.Exists(datei))
                {
                    File.Delete(datei);
                }
            }
            catch (Exception)
            {
                // Ein nicht loeschbarer Index ist kein Grund zum Abbruch
            }
        }

        #endregion
    }
}
