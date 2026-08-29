using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace Listenverschieber
{
    /// <summary>
    /// Tab 3: Dateien anhand ihres Inhalts umbenennen.
    /// </summary>
    public partial class MainWindow
    {
        private readonly ObservableCollection<UmbenennungsEintrag> umbVorschau = new();

        private void InitUmbenennenTab()
        {
            dgUmbVorschau.ItemsSource = umbVorschau;
            cmbUmbQuelldateiEndungen.ItemsSource = EndungsAuswahl.Vorgaben;
            UmbEndungenSetzen("ini");
        }

        /// <summary>Liest die aktuelle Endungsauswahl - egal ob Listeneintrag oder freie Eingabe.</summary>
        private string UmbEndungenText()
        {
            if (cmbUmbQuelldateiEndungen.SelectedItem is EndungsVorgabe vorgabe
                && string.Equals(cmbUmbQuelldateiEndungen.Text, vorgabe.Anzeige, StringComparison.Ordinal))
            {
                return vorgabe.Wert;
            }
            return cmbUmbQuelldateiEndungen.Text;
        }

        /// <summary>Setzt die Auswahl; passende Vorgaben werden in der Liste markiert.</summary>
        private void UmbEndungenSetzen(string? wert)
        {
            var text = string.IsNullOrWhiteSpace(wert) ? "ini" : wert.Trim();
            var treffer = EndungsAuswahl.Vorgaben
                .Select((v, i) => new { v, i })
                .FirstOrDefault(x => string.Equals(x.v.Wert, text, StringComparison.OrdinalIgnoreCase));

            if (treffer != null)
            {
                cmbUmbQuelldateiEndungen.SelectedIndex = treffer.i;
            }
            else
            {
                cmbUmbQuelldateiEndungen.SelectedIndex = -1;
                cmbUmbQuelldateiEndungen.Text = text;
            }
        }

        #region Pfadverwaltung

        private void btnUmbArbeitspfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Arbeitspfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtUmbArbeitspfad.Text = dialog.SelectedPath;
                UmbLog($"Arbeitspfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnUmbArbeitspfadHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            string pfad = txtUmbArbeitspfad.Text.Trim();
            if (string.IsNullOrWhiteSpace(pfad) || !Directory.Exists(pfad))
            {
                MessageBox.Show("Bitte einen gültigen Arbeitspfad auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (lstUmbArbeitspfade.Items.Cast<string>().Any(p => p.Equals(pfad, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Dieser Pfad ist bereits in der Liste.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lstUmbArbeitspfade.Items.Add(pfad);
            UmbLog($"Arbeitspfad zur Liste hinzugefügt: {pfad}");
        }

        private void btnUmbArbeitspfadEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (lstUmbArbeitspfade.SelectedItem is string pfad)
            {
                lstUmbArbeitspfade.Items.Remove(pfad);
                UmbLog($"Arbeitspfad aus Liste entfernt: {pfad}");
            }
        }

        private void lstUmbArbeitspfade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnUmbArbeitspfadEntfernen.IsEnabled = lstUmbArbeitspfade.SelectedItem != null;
        }

        private void rbUmbAbschnitt_Changed(object sender, RoutedEventArgs e)
        {
            UmbFelderAktualisieren();
        }

        private void rbUmbWerttyp_Changed(object sender, RoutedEventArgs e)
        {
            UmbFelderAktualisieren();
        }

        private void cmbUmbAutoMusterTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UmbFelderAktualisieren();
        }

        /// <summary>
        /// Aktiviert die Eingabefelder passend zu Abschnittstyp und Abschnittswahl.
        /// </summary>
        private void UmbFelderAktualisieren()
        {
            if (txtUmbAbschnittNummer == null || rbUmbRichtungVorwaerts == null
                || txtUmbAutoMuster == null || txtUmbQuellFormatInhalt == null)
            {
                return;
            }

            bool fest = rbUmbAbschnittFest.IsChecked == true;
            bool alsDatum = rbUmbWertDatum.IsChecked == true;

            txtUmbAbschnittNummer.IsEnabled = fest;
            rbUmbRichtungVorwaerts.IsEnabled = fest;
            rbUmbRichtungRueckwaerts.IsEnabled = fest;

            // Suchmuster nur relevant, wenn im Text-Modus automatisch gesucht wird
            bool musterAktiv = !fest && !alsDatum;
            lblUmbAutoMuster.IsEnabled = musterAktiv;
            cmbUmbAutoMusterTyp.IsEnabled = musterAktiv;

            bool eigenesMuster = cmbUmbAutoMusterTyp.SelectedIndex == (int)AbschnittMusterTyp.EigenesMuster;

            // Bei eigenem Muster das Regex-Feld zeigen, sonst die Längenangabe
            txtUmbAutoMuster.Visibility = eigenesMuster ? Visibility.Visible : Visibility.Collapsed;
            txtUmbAutoMuster.IsEnabled = musterAktiv;

            bool laengeAktiv = musterAktiv && !eigenesMuster;
            lblUmbAutoLaenge.IsEnabled = laengeAktiv;
            txtUmbAutoLaenge.IsEnabled = laengeAktiv;
            lblUmbAutoLaengeHinweis.IsEnabled = laengeAktiv;
            lblUmbAutoLaenge.Visibility = eigenesMuster ? Visibility.Collapsed : Visibility.Visible;
            txtUmbAutoLaenge.Visibility = eigenesMuster ? Visibility.Collapsed : Visibility.Visible;
            lblUmbAutoLaengeHinweis.Visibility = eigenesMuster ? Visibility.Collapsed : Visibility.Visible;

            // Datumsformate nur im Datums-Modus
            txtUmbQuellFormatInhalt.IsEnabled = alsDatum;
            txtUmbQuellFormatDateiname.IsEnabled = alsDatum;
            txtUmbZielFormatDateiname.IsEnabled = alsDatum;
            lblUmbQuellFormatInhalt.IsEnabled = alsDatum;
            lblUmbQuellFormatDateiname.IsEnabled = alsDatum;
            lblUmbZielFormat.IsEnabled = alsDatum;
        }

        private void UmbLog(string nachricht)
        {
            txtUmbLog.AppendText($"{DateTime.Now:HH:mm:ss} - {nachricht}\n");
            txtUmbLog.ScrollToEnd();
        }

        #endregion

        #region Vorschau

        private UmbenennungsOptionen? UmbOptionenLesen()
        {
            var optionen = new UmbenennungsOptionen
            {
                Suchschluessel = txtUmbSuchschluessel.Text.Trim(),
                Trennzeichen = txtUmbTrennzeichen.Text,
                AbschnittAuto = rbUmbAbschnittAuto.IsChecked == true,
                QuellFormatInhalt = txtUmbQuellFormatInhalt.Text.Trim(),
                QuellFormatDateiname = txtUmbQuellFormatDateiname.Text.Trim(),
                ZielFormatDateiname = txtUmbZielFormatDateiname.Text.Trim(),
                AlsDatumFormatieren = rbUmbWertDatum.IsChecked == true,
                AutoMuster = txtUmbAutoMuster.Text.Trim(),
                MusterTyp = (AbschnittMusterTyp)Math.Max(0, cmbUmbAutoMusterTyp.SelectedIndex),
                MusterLaenge = int.TryParse(txtUmbAutoLaenge.Text.Trim(), out int laenge) && laenge > 0 ? laenge : 0
            };

            if (string.IsNullOrWhiteSpace(optionen.Suchschluessel))
            {
                MessageBox.Show("Bitte einen Suchbegriff angeben (z.B. 'Datum=').", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            if (string.IsNullOrEmpty(optionen.Trennzeichen))
            {
                MessageBox.Show("Bitte ein Trennzeichen angeben (z.B. '_').", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }

            if (!optionen.AbschnittAuto)
            {
                if (!int.TryParse(txtUmbAbschnittNummer.Text.Trim(), out int nummer) || nummer < 1)
                {
                    MessageBox.Show("Bitte eine gültige Abschnittsnummer (>= 1) angeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }
                optionen.AbschnittNummer = nummer;
                optionen.AbschnittVonHinten = rbUmbRichtungRueckwaerts.IsChecked == true;
            }
            else if (!optionen.AlsDatumFormatieren)
            {
                string muster = optionen.EffektivesMuster();
                if (string.IsNullOrWhiteSpace(muster))
                {
                    MessageBox.Show("Bitte auswählen, woraus der zu ersetzende Abschnitt besteht.",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return null;
                }

                try
                {
                    _ = System.Text.RegularExpressions.Regex.Match(string.Empty, muster);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show($"Das eigene Muster ist ungültig:\n{ex.Message}", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }

            return optionen;
        }

        private List<string> UmbArbeitspfadeErmitteln()
        {
            var pfade = lstUmbArbeitspfade.Items.Cast<string>().ToList();
            if (pfade.Count == 0)
            {
                string einzel = txtUmbArbeitspfad.Text.Trim();
                if (!string.IsNullOrWhiteSpace(einzel))
                {
                    pfade.Add(einzel);
                }
            }
            return pfade.Where(Directory.Exists).ToList();
        }

        private HashSet<string> UmbEndungenLesen()
            => EndungsAuswahl.Auswerten(UmbEndungenText());

        private void btnUmbVorschau_Click(object sender, RoutedEventArgs e)
        {
            var optionen = UmbOptionenLesen();
            if (optionen == null)
            {
                return;
            }

            var pfade = UmbArbeitspfadeErmitteln();
            if (pfade.Count == 0)
            {
                MessageBox.Show("Bitte mindestens einen gültigen Arbeitspfad angeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var endungen = UmbEndungenLesen();
            if (endungen.Count == 0)
            {
                MessageBox.Show("Bitte mindestens eine Dateiendung zum Durchsuchen angeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            umbVorschau.Clear();
            btnUmbAusfuehren.IsEnabled = false;

            var suchOption = chkUmbUnterordner.IsChecked == true
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            int gesamt = 0;
            int bereit = 0;

            foreach (var arbeitspfad in pfade)
            {
                UmbLog($"Analysiere: {arbeitspfad}");

                string[] alleDateien;
                try
                {
                    alleDateien = Directory.GetFiles(arbeitspfad, "*.*", suchOption);
                }
                catch (Exception ex)
                {
                    UmbLog($"FEHLER beim Lesen von '{arbeitspfad}': {ex.Message}");
                    continue;
                }

                // Dateien nach Ordner + Basisname (ohne Endung) gruppieren
                var gruppen = alleDateien.GroupBy(
                    f => (Ordner: Path.GetDirectoryName(f) ?? arbeitspfad, Basis: Path.GetFileNameWithoutExtension(f)));

                foreach (var gruppe in gruppen)
                {
                    gesamt++;
                    var eintrag = UmbGruppeAnalysieren(gruppe.Key.Ordner, gruppe.Key.Basis, gruppe.ToList(), endungen, optionen);
                    umbVorschau.Add(eintrag);
                    if (eintrag.Umbenennbar)
                    {
                        bereit++;
                    }
                }
            }

            UmbLog($"Vorschau erstellt: {gesamt} Dateigruppen analysiert, {bereit} umbenennbar.");
            btnUmbAusfuehren.IsEnabled = bereit > 0;
            txtStatus.Text = $"Vorschau: {bereit} von {gesamt} Gruppen umbenennbar";
        }

        private UmbenennungsEintrag UmbGruppeAnalysieren(
            string ordner,
            string basisName,
            List<string> dateien,
            HashSet<string> endungen,
            UmbenennungsOptionen optionen)
        {
            var eintrag = new UmbenennungsEintrag
            {
                BasisName = basisName,
                Ordner = ordner,
                AnzeigePfad = ordner
            };

            // Nur Dateien der konfigurierten Endungen als Informationsquelle nutzen
            var quellKandidaten = dateien
                .Where(f => EndungsAuswahl.Passt(f, endungen))
                .OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (quellKandidaten.Count == 0)
            {
                eintrag.Status = "Keine durchsuchbare Quelldatei";
                return eintrag;
            }

            string? wert = null;
            string? letzterFehler = null;

            foreach (var kandidat in quellKandidaten)
            {
                var inhalt = DateiInhaltsLeser.LiesText(kandidat, out string? leseFehler);
                if (inhalt == null)
                {
                    letzterFehler = leseFehler;
                    continue;
                }

                var gefunden = UmbenennungsLogik.WertAusInhalt(inhalt, optionen.Suchschluessel);
                if (!string.IsNullOrWhiteSpace(gefunden))
                {
                    wert = gefunden;
                    eintrag.Quelldatei = Path.GetFileName(kandidat);
                    break;
                }

                letzterFehler = $"'{optionen.Suchschluessel}' nicht gefunden";
            }

            if (wert == null)
            {
                eintrag.Status = letzterFehler ?? $"'{optionen.Suchschluessel}' nicht gefunden";
                return eintrag;
            }

            eintrag.GefundenerWert = wert;

            var formatiert = UmbenennungsLogik.WertFormatieren(wert, optionen, out string? formatFehler);
            if (formatiert == null)
            {
                eintrag.Status = formatFehler ?? "Wert nicht konvertierbar";
                return eintrag;
            }

            var neuerName = UmbenennungsLogik.NeuenNamenBilden(basisName, formatiert, optionen, out string? namensFehler);
            if (neuerName == null)
            {
                eintrag.Status = namensFehler ?? "Kein passender Abschnitt";
                return eintrag;
            }

            eintrag.NeuerBasisName = neuerName;

            // Welche Dateien werden mitumbenannt?
            eintrag.BetroffeneDateien = chkUmbGleichnamige.IsChecked == true
                ? dateien
                : dateien.Where(f => Path.GetFileName(f).Equals(eintrag.Quelldatei, StringComparison.OrdinalIgnoreCase)).ToList();

            // Kollisionsprüfung
            var kollision = eintrag.BetroffeneDateien
                .Select(f => Path.Combine(ordner, neuerName + Path.GetExtension(f)))
                .FirstOrDefault(File.Exists);

            if (kollision != null)
            {
                eintrag.Status = $"Zieldatei existiert bereits: {Path.GetFileName(kollision)}";
                return eintrag;
            }

            eintrag.Umbenennbar = true;
            eintrag.Status = $"Bereit ({eintrag.BetroffeneDateien.Count} Datei(en))";
            return eintrag;
        }

        #endregion

        #region Ausführung

        private void btnUmbAusfuehren_Click(object sender, RoutedEventArgs e)
        {
            var zuVerarbeiten = umbVorschau.Where(v => v.Umbenennbar).ToList();
            if (zuVerarbeiten.Count == 0)
            {
                MessageBox.Show("Es gibt keine umbenennbaren Einträge. Bitte zuerst eine Vorschau erstellen.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int dateienGesamt = zuVerarbeiten.Sum(v => v.BetroffeneDateien.Count);
            var antwort = MessageBox.Show(
                $"{zuVerarbeiten.Count} Dateigruppe(n) mit insgesamt {dateienGesamt} Datei(en) werden umbenannt.\n\nFortfahren?",
                "Umbenennen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (antwort != MessageBoxResult.Yes)
            {
                return;
            }

            int erfolgreich = 0;
            int fehlgeschlagen = 0;

            foreach (var eintrag in zuVerarbeiten)
            {
                bool gruppeOk = true;

                foreach (var datei in eintrag.BetroffeneDateien)
                {
                    string zielPfad = Path.Combine(eintrag.Ordner, eintrag.NeuerBasisName + Path.GetExtension(datei));

                    try
                    {
                        if (File.Exists(zielPfad))
                        {
                            UmbLog($"ÜBERSPRUNGEN (Ziel existiert): {Path.GetFileName(zielPfad)}");
                            gruppeOk = false;
                            continue;
                        }

                        File.Move(datei, zielPfad);
                        UmbLog($"Umbenannt: {Path.GetFileName(datei)} -> {Path.GetFileName(zielPfad)}");
                        erfolgreich++;
                    }
                    catch (Exception ex)
                    {
                        UmbLog($"FEHLER bei '{Path.GetFileName(datei)}': {ex.Message}");
                        gruppeOk = false;
                        fehlgeschlagen++;
                    }
                }

                eintrag.Umbenennbar = false;
                eintrag.Status = gruppeOk ? "Umbenannt" : "Teilweise fehlgeschlagen";
            }

            dgUmbVorschau.Items.Refresh();
            btnUmbAusfuehren.IsEnabled = false;

            UmbLog($"Fertig: {erfolgreich} Datei(en) umbenannt, {fehlgeschlagen} Fehler.");
            txtStatus.Text = $"Umbenennen abgeschlossen: {erfolgreich} Datei(en)";

            MessageBox.Show($"{erfolgreich} Datei(en) umbenannt.\n{fehlgeschlagen} Fehler.",
                "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Konfiguration

        private void UmbKonfigurationLaden(PfadKonfiguration konfiguration)
        {
            txtUmbArbeitspfad.Text = konfiguration.UmbArbeitspfad;

            lstUmbArbeitspfade.Items.Clear();
            foreach (var pfad in konfiguration.UmbArbeitspfadListe)
            {
                lstUmbArbeitspfade.Items.Add(pfad);
            }

            chkUmbUnterordner.IsChecked = konfiguration.UmbUnterordner;
            txtUmbSuchschluessel.Text = konfiguration.UmbSuchschluessel;
            UmbEndungenSetzen(konfiguration.UmbQuelldateiEndungen);
            txtUmbTrennzeichen.Text = konfiguration.UmbTrennzeichen;
            rbUmbAbschnittAuto.IsChecked = konfiguration.UmbAbschnittAuto;
            rbUmbAbschnittFest.IsChecked = !konfiguration.UmbAbschnittAuto;
            txtUmbAbschnittNummer.Text = konfiguration.UmbAbschnittNummer.ToString();
            rbUmbRichtungRueckwaerts.IsChecked = konfiguration.UmbAbschnittVonHinten;
            rbUmbRichtungVorwaerts.IsChecked = !konfiguration.UmbAbschnittVonHinten;
            rbUmbAbschnitt_Changed(this, new RoutedEventArgs());
            txtUmbQuellFormatInhalt.Text = konfiguration.UmbQuellFormatInhalt;
            txtUmbQuellFormatDateiname.Text = konfiguration.UmbQuellFormatDateiname;
            txtUmbZielFormatDateiname.Text = konfiguration.UmbZielFormatDateiname;
            rbUmbWertDatum.IsChecked = konfiguration.UmbAlsDatumFormatieren;
            rbUmbWertText.IsChecked = !konfiguration.UmbAlsDatumFormatieren;
            txtUmbAutoMuster.Text = konfiguration.UmbAutoMuster;
            cmbUmbAutoMusterTyp.SelectedIndex = konfiguration.UmbMusterTyp;
            txtUmbAutoLaenge.Text = konfiguration.UmbMusterLaenge > 0 ? konfiguration.UmbMusterLaenge.ToString() : "";
            chkUmbGleichnamige.IsChecked = konfiguration.UmbGleichnamigeMitumbenennen;
            UmbFelderAktualisieren();
        }

        private void UmbKonfigurationSpeichern(PfadKonfiguration konfiguration)
        {
            konfiguration.UmbArbeitspfad = txtUmbArbeitspfad.Text;
            konfiguration.UmbArbeitspfadListe = lstUmbArbeitspfade.Items.Cast<string>().ToList();
            konfiguration.UmbUnterordner = chkUmbUnterordner.IsChecked == true;
            konfiguration.UmbSuchschluessel = txtUmbSuchschluessel.Text;
            konfiguration.UmbQuelldateiEndungen = UmbEndungenText();
            konfiguration.UmbTrennzeichen = txtUmbTrennzeichen.Text;
            konfiguration.UmbAbschnittAuto = rbUmbAbschnittAuto.IsChecked == true;
            konfiguration.UmbAbschnittNummer = int.TryParse(txtUmbAbschnittNummer.Text.Trim(), out int nummer) ? nummer : 4;
            konfiguration.UmbAbschnittVonHinten = rbUmbRichtungRueckwaerts.IsChecked == true;
            konfiguration.UmbQuellFormatInhalt = txtUmbQuellFormatInhalt.Text;
            konfiguration.UmbQuellFormatDateiname = txtUmbQuellFormatDateiname.Text;
            konfiguration.UmbZielFormatDateiname = txtUmbZielFormatDateiname.Text;
            konfiguration.UmbAlsDatumFormatieren = rbUmbWertDatum.IsChecked == true;
            konfiguration.UmbAutoMuster = txtUmbAutoMuster.Text;
            konfiguration.UmbMusterTyp = Math.Max(0, cmbUmbAutoMusterTyp.SelectedIndex);
            konfiguration.UmbMusterLaenge = int.TryParse(txtUmbAutoLaenge.Text.Trim(), out int musterLaenge) && musterLaenge > 0 ? musterLaenge : 0;
            konfiguration.UmbGleichnamigeMitumbenennen = chkUmbGleichnamige.IsChecked == true;
        }

        #endregion
    }
}
