using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace Listenverschieber
{
    /// <summary>
    /// Tab 4, zweiter Teil: Zusammengehoerige Dateien ueber einen Inhaltsindex finden.
    ///
    /// Vorgehen: Das Zielverzeichnis wird einmalig eingelesen und als Index abgelegt.
    /// Danach wird zu jeder Datei im Suchpfad anhand der eingestellten Schluessel
    /// (oder des kompletten Inhalts) im Index die passende Partnerdatei nachgeschlagen.
    /// Das ist deutlich schneller, als das Zielverzeichnis bei jeder Suche erneut
    /// vollstaendig zu lesen.
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>Der zuletzt geladene oder aufgebaute Index des Zielverzeichnisses.</summary>
        private InhaltsIndex? inhIndex;

        /// <summary>
        /// Inhaltsindex des Zielordners, ausschliesslich fuer die Duplikaterkennung.
        ///
        /// Bewusst getrennt vom Suchindex: Der Zielordner ist kein Suchraum, seine
        /// Eintraege duerfen daher nie als Partnertreffer auftauchen. Da im Ziel im
        /// Regelfall nur Dateien hinzukommen, genuegt der inkrementelle Aufbau -
        /// unveraenderte Dateien werden anhand von Groesse und Aenderungszeit
        /// uebernommen, sodass jeder weitere Lauf nur die Neuzugaenge liest.
        /// </summary>
        private InhaltsIndex? inhZielIndex;

        /// <summary>Kennzeichnet, dass der Index nach einer Uebertragung nicht mehr aktuell ist.</summary>
        private bool inhIndexVeraltet;

        /// <summary>
        /// Indizierte Quelldateien, nachgeschlagen ueber den vollstaendigen Pfad.
        ///
        /// Auch die Quellseite wird indiziert, damit wiederholte Laeufe die Dateien
        /// nicht erneut lesen und auswerten muessen. Das lohnt sich besonders bei
        /// PDF-Dateien, deren Textextraktion teuer ist.
        /// </summary>
        private Dictionary<string, IndexEintrag> inhQuellEintraege =
            new(StringComparer.OrdinalIgnoreCase);

        #region Hilfen

        /// <summary>Liest die Endungsauswahl des Index - Listeneintrag oder freie Eingabe.</summary>
        private string InhIndexEndungenText()
        {
            if (cmbInhIndexEndungen.SelectedItem is EndungsVorgabe vorgabe
                && string.Equals(cmbInhIndexEndungen.Text, vorgabe.Anzeige, StringComparison.Ordinal))
            {
                return vorgabe.Wert;
            }
            return cmbInhIndexEndungen.Text;
        }

        /// <summary>Setzt die Endungsauswahl des Index.</summary>
        private void InhIndexEndungenSetzen(string? wert)
        {
            var text = string.IsNullOrWhiteSpace(wert) ? "ini" : wert.Trim();
            var treffer = EndungsAuswahl.Vorgaben
                .Select((v, i) => new { v, i })
                .FirstOrDefault(x => string.Equals(x.v.Wert, text, StringComparison.OrdinalIgnoreCase));

            if (treffer != null)
            {
                cmbInhIndexEndungen.SelectedIndex = treffer.i;
            }
            else
            {
                cmbInhIndexEndungen.SelectedIndex = -1;
                cmbInhIndexEndungen.Text = text;
            }
        }

        /// <summary>Die vom Nutzer angegebenen Vergleichsschluessel, einer je Zeile.</summary>
        private List<string> InhPaarSchluesselLesen()
        {
            return (txtInhPaarSchluessel.Text ?? "")
                .Split('\n')
                .Select(z => InhaltsIndex.SchluesselNormalisieren(z))
                .Where(z => z.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Das Verzeichnis, in dem die zugehoerigen Dateien gesucht werden.
        ///
        /// Bewusst ein eigenes Feld und nicht der Zielpfad: Der Zielpfad ist an
        /// die Einstellung "Move als Unterverzeichnis" gekoppelt und beschreibt,
        /// wohin kopiert oder verschoben wird. Das Durchsuchen ist davon unabhaengig.
        /// </summary>
        private string InhIndexPfadLesen() => (txtInhIndexPfad.Text ?? "").Trim();

        /// <summary>Aktualisiert die Statuszeile unter den Index-Knoepfen.</summary>
        private void InhIndexStatusAktualisieren()
        {
            if (inhIndex == null)
            {
                txtInhIndexStatus.Text =
                    "Kein Index geladen. Bitte zuerst 'Index aktualisieren' ausführen. " +
                    $"Ablage: {InhaltsIndex.IndexOrdner}";
                return;
            }

            string alter = inhIndex.ErstelltUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            string zusatz = inhIndexVeraltet
                ? " - durch die letzte Übertragung veraltet, bitte aktualisieren."
                : "";

            string zielTeil = inhZielIndex != null
                ? $" Duplikatindex Ziel: {inhZielIndex.Anzahl} Datei(en)."
                : "";

            txtInhIndexStatus.Text =
                $"Index: {inhIndex.Anzahl} Datei(en) aus '{inhIndex.Verzeichnis}' " +
                $"(Endungen: {inhIndex.Endungen}, Stand {alter}).{zusatz}{zielTeil} " +
                $"Ablage: {InhaltsIndex.IndexOrdner}";
        }

        /// <summary>Schaltet die Knoepfe des Paarbereichs.</summary>
        private void InhPaarButtonsAktivieren(bool aktiv)
        {
            btnInhIndexAktualisieren.IsEnabled = aktiv;
            btnInhIndexNeu.IsEnabled = aktiv;
            btnInhPaareSuchen.IsEnabled = aktiv;
            btnInhAbbrechen.IsEnabled = !aktiv;

            if (!aktiv)
            {
                btnInhPaareKopieren.IsEnabled = false;
                btnInhPaareVerschieben.IsEnabled = false;
            }
        }

        /// <summary>Gibt die Treffer zurueck, auf die sich eine Uebertragung bezieht.</summary>
        private List<InhaltsTreffer> InhPaarAuswahlLesen()
        {
            var ausgewaehlt = dgInhGefundeneDateien.SelectedItems
                .OfType<InhaltsTreffer>()
                .Where(t => !string.IsNullOrEmpty(t.PartnerPfad))
                .ToList();

            if (ausgewaehlt.Count > 0)
            {
                return ausgewaehlt;
            }

            // Ohne Auswahl gelten alle Treffer mit Partnerdatei
            return inhaltsTreffer.Where(t => !string.IsNullOrEmpty(t.PartnerPfad)).ToList();
        }

        #endregion

        #region Index aufbauen

        private void btnInhIndexPfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Verzeichnis auswählen, in dem die zugehörigen Dateien gesucht werden",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtInhIndexPfad.Text = dialog.SelectedPath;
                InhLog($"Durchsuchtes Verzeichnis gesetzt: {dialog.SelectedPath}");

                // Index des vorigen Verzeichnisses gilt nicht mehr
                inhIndex = null;
                inhZielIndex = null;
                inhIndexVeraltet = false;
                InhIndexStatusAktualisieren();
            }
        }

        private void btnInhIndexAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            _ = InhIndexAufbauenAsync(vollstaendigNeu: false);
        }

        private void btnInhIndexNeu_Click(object sender, RoutedEventArgs e)
        {
            var antwort = MessageBox.Show(
                "Der gespeicherte Index wird verworfen und das durchsuchte Verzeichnis vollständig neu eingelesen.\n\n" +
                "Bei vielen Dateien kann das einige Zeit dauern.\n\nFortfahren?",
                "Index neu aufbauen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (antwort != MessageBoxResult.Yes)
            {
                return;
            }

            _ = InhIndexAufbauenAsync(vollstaendigNeu: true);
        }

        private async Task InhIndexAufbauenAsync(bool vollstaendigNeu)
        {
            string indexPfad = InhIndexPfadLesen();
            if (string.IsNullOrWhiteSpace(indexPfad) || !Directory.Exists(indexPfad))
            {
                MessageBox.Show("Bitte ein gültiges 'Durchsuchtes Verzeichnis' angeben. Dort wird nach den zugehörigen Dateien gesucht.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string endungsText = InhIndexEndungenText();
            var endungen = EndungsAuswahl.Auswerten(endungsText);
            if (endungen.Count == 0)
            {
                MessageBox.Show("Bitte mindestens eine gültige Dateiendung für den Index angeben.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool mitUnterordnern = chkInhIndexUnterordner.IsChecked == true;

            if (vollstaendigNeu)
            {
                InhaltsIndex.Loeschen(indexPfad);
            }

            inhAbbruch = new CancellationTokenSource();
            var token = inhAbbruch.Token;
            InhPaarButtonsAktivieren(false);
            InhButtonsAktivieren(false);
            progressBarInh.Value = 0;

            InhLog($"--- Index {(vollstaendigNeu ? "neu aufbauen" : "aktualisieren")}: {indexPfad} ---");

            var fortschritt = new Progress<IndexFortschritt>(f =>
            {
                progressBarInh.Value = f.Gesamt > 0 ? (int)((f.Verarbeitet / (double)f.Gesamt) * 100) : 0;
                txtInhFortschritt.Text = $"Index: {f.Verarbeitet} von {f.Gesamt} - {f.NeuGelesen} neu gelesen";
            });

            try
            {
                var index = await Task.Run(
                    () => InhaltsIndex.Aufbauen(indexPfad, endungen, endungsText, mitUnterordnern,
                        vollstaendigNeu, fortschritt, token),
                    token);

                await Task.Run(() => index.Speichern(), token);

                inhIndex = index;
                inhIndexVeraltet = false;

                progressBarInh.Value = 100;
                txtInhFortschritt.Text = $"Index bereit: {index.Anzahl} Datei(en)";
                InhLog($"--- Index fertig: {index.Anzahl} Datei(en) erfasst ---");
                txtStatus.Text = $"Index: {index.Anzahl} Datei(en)";

                await InhZielIndexAufbauenAsync(endungen, endungsText, vollstaendigNeu, token);
            }
            catch (OperationCanceledException)
            {
                InhLog("--- Indexaufbau abgebrochen ---");
                txtInhFortschritt.Text = "Abgebrochen";
            }
            catch (Exception ex)
            {
                InhLog($"SCHWERER FEHLER beim Indexaufbau: {ex.Message}");
                MessageBox.Show($"Fehler beim Indexaufbau:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InhPaarButtonsAktivieren(true);
                InhButtonsAktivieren(true);
                inhAbbruch?.Dispose();
                inhAbbruch = null;
                InhIndexStatusAktualisieren();
            }
        }

        /// <summary>
        /// Baut den Inhaltsindex des Zielordners auf, der spaeter die Duplikaterkennung
        /// traegt. Er nutzt dieselben Endungen wie der Suchindex, damit beide Seiten
        /// denselben Dateibestand betrachten.
        ///
        /// Fehler werden hier nur protokolliert und nicht weitergereicht: Die
        /// Duplikaterkennung ist eine Zusatzfunktion und darf den Suchindex, der
        /// zu diesem Zeitpunkt bereits fertig ist, nicht entwerten.
        /// </summary>
        private async Task InhZielIndexAufbauenAsync(
            HashSet<string> endungen,
            string endungsText,
            bool vollstaendigNeu,
            CancellationToken token)
        {
            if (chkInhZielDuplikate.IsChecked != true)
            {
                inhZielIndex = null;
                return;
            }

            string zielpfad = txtInhZielpfad.Text.Trim();
            if (string.IsNullOrWhiteSpace(zielpfad) || !Directory.Exists(zielpfad))
            {
                inhZielIndex = null;
                InhLog("Duplikatindex übersprungen: Zielpfad ist nicht vorhanden.");
                return;
            }

            // Zeigen Ziel- und Suchverzeichnis auf denselben Ordner, waere der
            // zweite Index eine exakte Kopie des ersten und damit nutzlos.
            if (string.Equals(Path.GetFullPath(zielpfad),
                              Path.GetFullPath(InhIndexPfadLesen()),
                              StringComparison.OrdinalIgnoreCase))
            {
                inhZielIndex = null;
                InhLog("Duplikatindex übersprungen: Zielpfad und durchsuchtes Verzeichnis sind identisch.");
                return;
            }

            try
            {
                InhLog($"--- Duplikatindex des Zielordners: {zielpfad} ---");

                var fortschritt = new Progress<IndexFortschritt>(f =>
                {
                    progressBarInh.Value = f.Gesamt > 0 ? (int)((f.Verarbeitet / (double)f.Gesamt) * 100) : 0;
                    txtInhFortschritt.Text = $"Zielindex: {f.Verarbeitet} von {f.Gesamt} - {f.NeuGelesen} neu gelesen";
                });

                if (vollstaendigNeu)
                {
                    InhaltsIndex.Loeschen(zielpfad);
                }

                // Unterordner bewusst immer mit: Eine Datei ist auch dann ein
                // Duplikat, wenn sie in einem Unterordner des Ziels abgelegt wurde.
                var zielIndex = await Task.Run(
                    () => InhaltsIndex.Aufbauen(zielpfad, endungen, endungsText,
                        mitUnterordnern: true, vollstaendigNeu, fortschritt, token),
                    token);

                await Task.Run(() => zielIndex.Speichern(), token);

                inhZielIndex = zielIndex;
                progressBarInh.Value = 100;
                txtInhFortschritt.Text = $"Zielindex bereit: {zielIndex.Anzahl} Datei(en)";
                InhLog($"--- Duplikatindex fertig: {zielIndex.Anzahl} Datei(en) im Ziel ---");
            }
            catch (OperationCanceledException)
            {
                inhZielIndex = null;
                InhLog("--- Aufbau des Duplikatindex abgebrochen ---");
            }
            catch (Exception ex)
            {
                inhZielIndex = null;
                InhLog($"Duplikatindex konnte nicht aufgebaut werden: {ex.Message}");
            }
        }

        #endregion

        #region Paare suchen

        /// <summary>
        /// Baut die Indizes aller Suchpfade auf und fasst deren Eintraege in einer
        /// Nachschlagetabelle zusammen.
        ///
        /// Der Index wird je Verzeichnis abgelegt, daher wird pro Suchpfad einzeln
        /// aufgebaut. Unveraenderte Dateien werden dabei aus dem gespeicherten Index
        /// uebernommen, sodass ein Wiederholungslauf nur die Neuzugaenge liest.
        /// </summary>
        private async Task<Dictionary<string, IndexEintrag>> InhQuellIndexAufbauenAsync(
            List<string> suchpfade,
            HashSet<string> endungen,
            string endungsText,
            bool mitUnterordnern,
            CancellationToken token)
        {
            var tabelle = new Dictionary<string, IndexEintrag>(StringComparer.OrdinalIgnoreCase);

            foreach (var pfad in suchpfade)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    var fortschritt = new Progress<IndexFortschritt>(f =>
                    {
                        progressBarInh.Value = f.Gesamt > 0 ? (int)((f.Verarbeitet / (double)f.Gesamt) * 100) : 0;
                        txtInhFortschritt.Text = $"Quellindex: {f.Verarbeitet} von {f.Gesamt} - {f.NeuGelesen} neu gelesen";
                    });

                    var quellIndex = await Task.Run(
                        () => InhaltsIndex.Aufbauen(pfad, endungen, endungsText, mitUnterordnern,
                            vollstaendigNeu: false, fortschritt, token),
                        token);

                    await Task.Run(() => quellIndex.Speichern(), token);

                    foreach (var eintrag in quellIndex.Eintraege)
                    {
                        tabelle[Path.GetFullPath(eintrag.Pfad)] = eintrag;
                    }

                    InhLog($"Quellindex '{pfad}': {quellIndex.Anzahl} Datei(en).");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Ohne Quellindex wird die Datei einfach direkt gelesen -
                    // die Suche bleibt dadurch in jedem Fall funktionsfaehig.
                    InhLog($"Quellindex für '{pfad}' nicht möglich: {ex.Message}");
                }
            }

            progressBarInh.Value = 0;
            return tabelle;
        }

        private void btnInhPaareSuchen_Click(object sender, RoutedEventArgs e)
        {
            _ = InhPaareSuchenAsync();
        }

        private async Task InhPaareSuchenAsync()
        {
            string indexPfad = InhIndexPfadLesen();
            if (string.IsNullOrWhiteSpace(indexPfad) || !Directory.Exists(indexPfad))
            {
                MessageBox.Show("Bitte ein gültiges 'Durchsuchtes Verzeichnis' angeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool ueberInhalt = cmbInhPaarVergleich.SelectedIndex == 1;
            var schluessel = InhPaarSchluesselLesen();

            if (!ueberInhalt && schluessel.Count == 0)
            {
                MessageBox.Show("Bitte mindestens einen Vergleichsschlüssel angeben, z.B. 'Datum' oder 'Belegnummer'.\n\n" +
                    "Alternativ kann der komplette Dateiinhalt verglichen werden.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Index bereitstellen - falls noch keiner geladen ist, gespeicherten versuchen
            if (inhIndex == null || !string.Equals(inhIndex.Verzeichnis, indexPfad, StringComparison.OrdinalIgnoreCase))
            {
                inhIndex = InhaltsIndex.Laden(indexPfad);
                inhIndexVeraltet = false;
                InhIndexStatusAktualisieren();
            }

            if (inhIndex == null)
            {
                var antwort = MessageBox.Show(
                    "Für dieses Verzeichnis liegt noch kein Index vor.\n\nJetzt aufbauen?",
                    "Index fehlt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (antwort != MessageBoxResult.Yes)
                {
                    return;
                }

                await InhIndexAufbauenAsync(vollstaendigNeu: false);
                if (inhIndex == null)
                {
                    return;
                }
            }

            var suchpfade = lstInhSuchpfade.Items.Cast<string>().ToList();
            if (suchpfade.Count == 0)
            {
                string einzel = txtInhSuchpfad.Text.Trim();
                if (!string.IsNullOrWhiteSpace(einzel))
                {
                    suchpfade.Add(einzel);
                }
            }
            suchpfade = suchpfade.Where(Directory.Exists).ToList();

            if (suchpfade.Count == 0)
            {
                MessageBox.Show("Bitte mindestens einen gültigen Suchpfad angeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Fuer den Suchpfad gilt die Endungsauswahl der Textsuche
            var quellEndungen = EndungsAuswahl.Auswerten(InhEndungenText());
            if (quellEndungen.Count == 0)
            {
                MessageBox.Show("Bitte mindestens eine gültige Dateiendung für den Suchpfad angeben.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var suchOption = chkInhUnterordner.IsChecked == true
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            inhaltsTreffer.Clear();
            inhAbbruch = new CancellationTokenSource();
            var token = inhAbbruch.Token;
            InhPaarButtonsAktivieren(false);
            InhButtonsAktivieren(false);
            progressBarInh.Value = 0;

            InhLog(ueberInhalt
                ? "--- Paarsuche über den kompletten Dateiinhalt gestartet ---"
                : $"--- Paarsuche über {schluessel.Count} Schlüssel gestartet: {string.Join(", ", schluessel)} ---");

            int mitTreffer = 0;
            int ohneTreffer = 0;
            int verarbeitet = 0;
            var index = inhIndex!;

            try
            {
                var dateien = new List<string>();
                foreach (var pfad in suchpfade)
                {
                    try
                    {
                        dateien.AddRange(Directory.GetFiles(pfad, "*", suchOption)
                            .Where(f => EndungsAuswahl.Passt(f, quellEndungen)));
                    }
                    catch (Exception ex)
                    {
                        InhLog($"FEHLER beim Lesen von '{pfad}': {ex.Message}");
                    }
                }

                // Eine Datei darf sich nicht selbst als Partner finden. Verglichen
                // wird deshalb dateigenau gegen den Index, nicht ueber den Pfadanfang.
                // Ein Pfadvergleich wuerde alle Quelldateien verwerfen, sobald das
                // durchsuchte Verzeichnis im Suchpfad liegt oder mit ihm identisch ist.
                var imIndex = new HashSet<string>(
                    index.Eintraege.Select(e => Path.GetFullPath(e.Pfad)),
                    StringComparer.OrdinalIgnoreCase);

                int ausgelassen = dateien.Count;
                dateien = dateien
                    .Where(f => !imIndex.Contains(Path.GetFullPath(f)))
                    .ToList();
                ausgelassen -= dateien.Count;

                if (ausgelassen > 0)
                {
                    InhLog($"{ausgelassen} Datei(en) übersprungen, weil sie selbst im Index stehen.");
                }

                InhLog($"{dateien.Count} Datei(en) werden zugeordnet...");

                // Quellseite indizieren. Der Index liegt je Suchpfad vor, deshalb
                // wird pro Pfad geladen beziehungsweise fortgeschrieben. Danach
                // stehen die Paare und Pruefsummen ohne erneutes Lesen bereit.
                inhQuellEintraege = await InhQuellIndexAufbauenAsync(
                    suchpfade, quellEndungen, InhEndungenText(),
                    chkInhUnterordner.IsChecked == true, token);

                foreach (var datei in dateien)
                {
                    token.ThrowIfCancellationRequested();

                    string dateiname = Path.GetFileName(datei);
                    inhQuellEintraege.TryGetValue(Path.GetFullPath(datei), out var quellEintrag);

                    var ergebnis = await Task.Run(() =>
                    {
                        // Aus dem Quellindex bedienen, sofern die Datei dort
                        // unveraendert vorliegt - sonst wie bisher einlesen.
                        Dictionary<string, string> alle;
                        string pruefsummeQuelle;

                        if (quellEintrag != null)
                        {
                            alle = quellEintrag.Paare;
                            pruefsummeQuelle = quellEintrag.Pruefsumme;
                        }
                        else
                        {
                            var inhalt = DateiInhaltsLeser.LiesText(datei, out string? leseFehler);
                            if (inhalt == null)
                            {
                                return (Partner: new List<IndexEintrag>(), Beschreibung: "", Fehler: leseFehler);
                            }

                            alle = InhaltsIndex.PaareAusInhalt(inhalt);
                            pruefsummeQuelle = InhaltsIndex.PruefsummeAusInhalt(inhalt);
                        }

                        if (ueberInhalt)
                        {
                            return (Partner: index.FindeNachPruefsumme(pruefsummeQuelle),
                                    Beschreibung: "gleicher Inhalt",
                                    Fehler: (string?)null);
                        }

                        var gesucht = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var s in schluessel)
                        {
                            if (alle.TryGetValue(s, out var wert))
                            {
                                gesucht[s] = wert;
                            }
                        }

                        if (gesucht.Count != schluessel.Count)
                        {
                            var fehlend = schluessel.Where(s => !gesucht.ContainsKey(s));
                            return (Partner: new List<IndexEintrag>(),
                                    Beschreibung: "",
                                    Fehler: $"nicht enthalten: {string.Join(", ", fehlend)}");
                        }

                        string beschreibung = string.Join("; ", gesucht.Select(p => $"{p.Key}={p.Value}"));
                        return (Partner: index.FindeNachPaaren(gesucht),
                                Beschreibung: beschreibung,
                                Fehler: (string?)null);
                    }, token);

                    verarbeitet++;
                    progressBarInh.Value = dateien.Count > 0
                        ? (int)((verarbeitet / (double)dateien.Count) * 100)
                        : 100;
                    txtInhFortschritt.Text = $"{verarbeitet} von {dateien.Count} zugeordnet - {mitTreffer} Paar(e)";

                    if (ergebnis.Fehler != null)
                    {
                        ohneTreffer++;
                        inhaltsTreffer.Add(new InhaltsTreffer
                        {
                            Dateiname = dateiname,
                            Status = $"Kein Vergleich möglich ({ergebnis.Fehler})",
                            AnzeigePfad = Path.GetDirectoryName(datei) ?? "",
                            VollstaendigerPfad = datei
                        });
                        continue;
                    }

                    if (ergebnis.Partner.Count == 0)
                    {
                        ohneTreffer++;
                        inhaltsTreffer.Add(new InhaltsTreffer
                        {
                            Dateiname = dateiname,
                            Status = "Kein Partner gefunden",
                            Fundstelle = ergebnis.Beschreibung,
                            AnzeigePfad = Path.GetDirectoryName(datei) ?? "",
                            VollstaendigerPfad = datei
                        });
                        continue;
                    }

                    mitTreffer++;

                    // Mehrere Treffer werden alle aufgelistet, damit der Nutzer waehlen kann
                    foreach (var partner in ergebnis.Partner)
                    {
                        inhaltsTreffer.Add(new InhaltsTreffer
                        {
                            Dateiname = dateiname,
                            Status = ergebnis.Partner.Count > 1
                                ? $"Mehrdeutig ({ergebnis.Partner.Count} Treffer)"
                                : "Paar gefunden",
                            Partnerdatei = partner.Dateiname,
                            PartnerPfad = partner.Pfad,
                            Fundstelle = ergebnis.Beschreibung,
                            AnzeigePfad = Path.GetDirectoryName(datei) ?? "",
                            VollstaendigerPfad = datei
                        });
                    }
                }

                txtInhFortschritt.Text = $"Fertig: {mitTreffer} Paar(e), {ohneTreffer} ohne Partner";
                InhLog($"--- Fertig: {verarbeitet} geprüft, {mitTreffer} mit Partner, {ohneTreffer} ohne ---");
                txtStatus.Text = $"Paarsuche: {mitTreffer} Treffer";
            }
            catch (OperationCanceledException)
            {
                InhLog($"--- Abgebrochen nach {verarbeitet} Datei(en) ---");
                txtInhFortschritt.Text = "Abgebrochen";
            }
            catch (Exception ex)
            {
                InhLog($"SCHWERER FEHLER: {ex.Message}");
                MessageBox.Show($"Fehler bei der Paarsuche:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InhPaarButtonsAktivieren(true);
                InhButtonsAktivieren(true);
                inhAbbruch?.Dispose();
                inhAbbruch = null;

                bool esGibtTreffer = inhaltsTreffer.Any(t => !string.IsNullOrEmpty(t.PartnerPfad));
                btnInhPaareKopieren.IsEnabled = esGibtTreffer;
                btnInhPaareVerschieben.IsEnabled = esGibtTreffer;
            }
        }

        #endregion

        #region Treffer uebertragen

        private void btnInhPaareKopieren_Click(object sender, RoutedEventArgs e)
        {
            InhPaareUebertragen(DateiOperationModus.Kopieren);
        }

        private void btnInhPaareVerschieben_Click(object sender, RoutedEventArgs e)
        {
            InhPaareUebertragen(DateiOperationModus.Verschieben);
        }

        /// <summary>
        /// Kopiert oder verschiebt die gefundenen Partnerdateien in den Ordner
        /// der jeweiligen Quelldatei. Vorhandene Dateien werden nie ueberschrieben;
        /// bei Namensgleichheit wird automatisch "(1)", "(2)" usw. angehaengt.
        /// </summary>
        private void InhPaareUebertragen(DateiOperationModus modus)
        {
            var auswahl = InhPaarAuswahlLesen();
            if (auswahl.Count == 0)
            {
                MessageBox.Show("Es liegen keine übertragbaren Treffer vor. Bitte zuerst eine Paarsuche ausführen.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool nurAuswahl = dgInhGefundeneDateien.SelectedItems
                .OfType<InhaltsTreffer>()
                .Any(t => !string.IsNullOrEmpty(t.PartnerPfad));

            string aktion = modus == DateiOperationModus.Kopieren ? "kopiert" : "verschoben";
            var antwort = MessageBox.Show(
                $"{auswahl.Count} Partnerdatei(en) werden zur jeweiligen Quelldatei {aktion}.\n" +
                (nurAuswahl ? "(nur die markierten Zeilen)\n" : "(alle gefundenen Treffer)\n") +
                "\nVorhandene Dateien werden nicht überschrieben, sondern mit (1), (2) ... abgelegt." +
                (chkInhZielDuplikate.IsChecked == true && inhZielIndex != null
                    ? "\nInhaltlich bereits vorhandene Dateien werden übersprungen."
                    : "") +
                (modus == DateiOperationModus.Verschieben
                    ? "\n\nAchtung: Durch das Verschieben verändert sich der Zielpfad, der Index wird danach als veraltet markiert."
                    : "") +
                "\n\nFortfahren?",
                "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (antwort != MessageBoxResult.Yes)
            {
                return;
            }

            int erfolgreich = 0;
            int fehler = 0;
            int duplikate = 0;

            foreach (var treffer in auswahl)
            {
                string zielordner = Path.GetDirectoryName(treffer.VollstaendigerPfad) ?? "";
                if (string.IsNullOrEmpty(zielordner) || !File.Exists(treffer.PartnerPfad))
                {
                    treffer.Status = "Partnerdatei nicht mehr vorhanden";
                    fehler++;
                    continue;
                }

                if (InhIstDuplikatImZiel(treffer.PartnerPfad, zielordner, out string vorhanden))
                {
                    treffer.Status = $"Übersprungen - inhaltsgleich zu {Path.GetFileName(vorhanden)}";
                    InhLog($"Duplikat übersprungen: '{Path.GetFileName(treffer.PartnerPfad)}' " +
                           $"ist inhaltsgleich zu '{vorhanden}'.");
                    duplikate++;
                    continue;
                }

                // Konfliktaktion 1 = automatisch umbenennen, damit nichts überschrieben wird
                string status = InhDateiUebertragen(treffer.PartnerPfad, zielordner, modus,
                    konfliktAktion: 1, out string zielDatei);

                treffer.Status = status;
                if (status.StartsWith("Fehler", StringComparison.OrdinalIgnoreCase))
                {
                    fehler++;
                }
                else
                {
                    erfolgreich++;
                    treffer.PartnerPfad = modus == DateiOperationModus.Verschieben ? zielDatei : treffer.PartnerPfad;
                    treffer.AnzeigePfad = zielordner;
                }
            }

            if (erfolgreich > 0)
            {
                // Jede Uebertragung legt Dateien im Ziel ab, damit ist der
                // Duplikatindex nicht mehr vollstaendig. Beim Verschieben
                // veraendert sich zusaetzlich das durchsuchte Verzeichnis.
                inhIndexVeraltet = modus == DateiOperationModus.Verschieben || inhIndexVeraltet;
                inhZielIndex = null;
                InhIndexStatusAktualisieren();
            }

            dgInhGefundeneDateien.Items.Refresh();

            string duplikatText = duplikate > 0 ? $", {duplikate} Duplikat(e) übersprungen" : "";
            InhLog($"--- Übertragung fertig: {erfolgreich} Datei(en), {fehler} Fehler{duplikatText} ---");
            txtStatus.Text = $"Übertragen: {erfolgreich} Datei(en)";

            MessageBox.Show(
                $"{erfolgreich} Partnerdatei(en) übertragen.\n" +
                (duplikate > 0 ? $"{duplikate} bereits inhaltsgleich im Ziel vorhanden und übersprungen.\n" : "") +
                $"{fehler} Fehler.",
                "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Prueft, ob im Zielordner bereits eine inhaltlich identische Datei liegt.
        ///
        /// Verglichen wird ueber die Inhaltspruefsumme, nicht ueber den Dateinamen -
        /// ein Duplikat wird damit auch dann erkannt, wenn es anders heisst. Der
        /// Zielindex dient nur als Vorauswahl; der gefundene Kandidat wird vor dem
        /// Ueberspringen noch geprueft, da der Index zwischenzeitlich veralten kann.
        /// </summary>
        private bool InhIstDuplikatImZiel(string quelle, string zielordner, out string vorhandeneDatei)
        {
            vorhandeneDatei = "";

            if (chkInhZielDuplikate.IsChecked != true || inhZielIndex == null)
            {
                return false;
            }

            var inhalt = DateiInhaltsLeser.LiesText(quelle, out _);
            if (inhalt == null)
            {
                // Ohne lesbaren Inhalt ist keine Aussage moeglich - im Zweifel uebertragen
                return false;
            }

            string pruefsumme = InhaltsIndex.PruefsummeAusInhalt(inhalt);

            foreach (var kandidat in inhZielIndex.FindeNachPruefsumme(pruefsumme))
            {
                if (!File.Exists(kandidat.Pfad))
                {
                    continue;
                }

                // Nur Treffer innerhalb des betrachteten Zielordners zaehlen
                string ordner = Path.GetDirectoryName(kandidat.Pfad) ?? "";
                if (!Path.GetFullPath(ordner).StartsWith(Path.GetFullPath(zielordner),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                vorhandeneDatei = kandidat.Pfad;
                return true;
            }

            return false;
        }

        #endregion
    }
}
