using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace Listenverschieber
{
    /// <summary>
    /// Ein Suchtreffer der Inhaltssuche.
    /// </summary>
    public class InhaltsTreffer
    {
        public string Dateiname { get; set; } = "";
        public string Status { get; set; } = "";
        public string Fundstelle { get; set; } = "";
        public string AnzeigePfad { get; set; } = "";
        public string VollstaendigerPfad { get; set; } = "";
    }

    /// <summary>
    /// Tab 4: Dateien anhand ihres Textinhalts suchen, kopieren oder verschieben.
    /// </summary>
    public partial class MainWindow
    {
        private readonly ObservableCollection<InhaltsTreffer> inhaltsTreffer = new();
        private CancellationTokenSource? inhAbbruch;

        private void InitInhaltssucheTab()
        {
            dgInhGefundeneDateien.ItemsSource = inhaltsTreffer;
            cmbInhDateiEndungen.ItemsSource = EndungsAuswahl.Vorgaben;
            InhEndungenSetzen(EndungsAuswahl.AlleKennung);
        }

        /// <summary>Liest die aktuelle Endungsauswahl - egal ob Listeneintrag oder freie Eingabe.</summary>
        private string InhEndungenText()
        {
            if (cmbInhDateiEndungen.SelectedItem is EndungsVorgabe vorgabe
                && string.Equals(cmbInhDateiEndungen.Text, vorgabe.Anzeige, StringComparison.Ordinal))
            {
                return vorgabe.Wert;
            }
            return cmbInhDateiEndungen.Text;
        }

        /// <summary>Setzt die Auswahl; passende Vorgaben werden in der Liste markiert.</summary>
        private void InhEndungenSetzen(string? wert)
        {
            var text = string.IsNullOrWhiteSpace(wert) ? EndungsAuswahl.AlleKennung : wert.Trim();
            var treffer = EndungsAuswahl.Vorgaben
                .Select((v, i) => new { v, i })
                .FirstOrDefault(x => string.Equals(x.v.Wert, text, StringComparison.OrdinalIgnoreCase));

            if (treffer != null)
            {
                cmbInhDateiEndungen.SelectedIndex = treffer.i;
            }
            else
            {
                cmbInhDateiEndungen.SelectedIndex = -1;
                cmbInhDateiEndungen.Text = text;
            }
        }

        #region Pfadverwaltung

        private void btnInhSuchpfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Suchpfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtInhSuchpfad.Text = dialog.SelectedPath;
                InhZielpfadAktualisieren();
                InhLog($"Suchpfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnInhZielpfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Zielpfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtInhZielpfad.Text = dialog.SelectedPath;
                InhLog($"Zielpfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void chkInhUseMoveFolder_Changed(object sender, RoutedEventArgs e)
        {
            InhZielpfadAktualisieren();
        }

        private void InhZielpfadAktualisieren()
        {
            if (chkInhUseMoveFolder?.IsChecked == true && !string.IsNullOrWhiteSpace(txtInhSuchpfad?.Text))
            {
                txtInhZielpfad.Text = Path.Combine(txtInhSuchpfad.Text, "Move");
            }
        }

        private void btnInhSuchpfadHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            string pfad = txtInhSuchpfad.Text.Trim();
            if (string.IsNullOrWhiteSpace(pfad) || !Directory.Exists(pfad))
            {
                MessageBox.Show("Bitte einen gültigen Suchpfad auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (lstInhSuchpfade.Items.Cast<string>().Any(p => p.Equals(pfad, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Dieser Pfad ist bereits in der Liste.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            lstInhSuchpfade.Items.Add(pfad);
            InhLog($"Suchpfad zur Liste hinzugefügt: {pfad}");
        }

        private void btnInhSuchpfadEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (lstInhSuchpfade.SelectedItem is string pfad)
            {
                lstInhSuchpfade.Items.Remove(pfad);
                InhLog($"Suchpfad aus Liste entfernt: {pfad}");
            }
        }

        private void lstInhSuchpfade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnInhSuchpfadEntfernen.IsEnabled = lstInhSuchpfade.SelectedItem != null;
        }

        private void btnInhDateiOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is not InhaltsTreffer treffer)
            {
                return;
            }

            try
            {
                if (!File.Exists(treffer.VollstaendigerPfad))
                {
                    MessageBox.Show("Die Datei existiert nicht mehr.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo(treffer.VollstaendigerPfad) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Datei konnte nicht geöffnet werden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InhLog(string nachricht)
        {
            txtInhLog.AppendText($"{DateTime.Now:HH:mm:ss} - {nachricht}\n");
            txtInhLog.ScrollToEnd();
        }

        #endregion

        #region Aktionen

        private void btnInhSuchlauf_Click(object sender, RoutedEventArgs e)
        {
            _ = InhVerarbeitenAsync(DateiOperationModus.Suchlauf);
        }

        private void btnInhKopieren_Click(object sender, RoutedEventArgs e)
        {
            _ = InhVerarbeitenAsync(DateiOperationModus.Kopieren);
        }

        private void btnInhVerschieben_Click(object sender, RoutedEventArgs e)
        {
            _ = InhVerarbeitenAsync(DateiOperationModus.Verschieben);
        }

        private void btnInhAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            inhAbbruch?.Cancel();
            InhLog("Abbruch angefordert...");
        }

        private void InhButtonsAktivieren(bool aktiv)
        {
            btnInhSuchlauf.IsEnabled = aktiv;
            btnInhKopieren.IsEnabled = aktiv;
            btnInhVerschieben.IsEnabled = aktiv;
            btnInhAbbrechen.IsEnabled = !aktiv;
        }

        private async Task InhVerarbeitenAsync(DateiOperationModus modus)
        {
            var suchzeilen = InhaltsSuche.SuchzeilenLesen(txtInhSuchbegriff.Text);
            if (suchzeilen.Count == 0)
            {
                MessageBox.Show("Bitte einen Suchtext eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
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
                MessageBox.Show("Bitte mindestens einen gültigen Suchpfad angeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var endungsEingabe = InhEndungenText();
            var endungen = EndungsAuswahl.Auswerten(endungsEingabe);

            if (endungen.Count == 0)
            {
                MessageBox.Show("Bitte mindestens eine gültige Dateiendung angeben oder '*.*' für alle durchsuchbaren Dateitypen wählen.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string zielpfad = txtInhZielpfad.Text.Trim();
            if (modus != DateiOperationModus.Suchlauf)
            {
                if (string.IsNullOrWhiteSpace(zielpfad))
                {
                    MessageBox.Show("Bitte einen Zielpfad angeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string aktion = modus == DateiOperationModus.Kopieren ? "kopiert" : "verschoben";
                var antwort = MessageBox.Show(
                    $"Alle Dateien mit dem gesuchten Inhalt werden nach\n{zielpfad}\n{aktion}.\n\nFortfahren?",
                    "Bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (antwort != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var modusAuswahl = (InhaltsSuchModus)Math.Max(0, cmbInhSuchModus.SelectedIndex);
            bool grossKlein = chkInhGrossKlein.IsChecked == true;
            bool platzhalter = chkInhPlatzhalter.IsChecked == true;
            bool gleichnamige = chkInhGleichnamige.IsChecked == true;
            int konfliktAktion = Math.Max(0, cmbInhKonflikt.SelectedIndex);
            var suchOption = chkInhUnterordner.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            inhaltsTreffer.Clear();
            inhAbbruch = new CancellationTokenSource();
            var token = inhAbbruch.Token;
            InhButtonsAktivieren(false);
            progressBarInh.Value = 0;

            InhLog($"--- {modus} gestartet ({suchzeilen.Count} Suchzeile(n)) ---");

            int treffer = 0;
            int verarbeitet = 0;
            int fehler = 0;

            try
            {
                // Zu durchsuchende Dateien sammeln
                var dateien = new List<string>();
                foreach (var pfad in suchpfade)
                {
                    try
                    {
                        dateien.AddRange(Directory.GetFiles(pfad, "*.*", suchOption)
                            .Where(f => EndungsAuswahl.Passt(f, endungen)));
                    }
                    catch (Exception ex)
                    {
                        InhLog($"FEHLER beim Lesen von '{pfad}': {ex.Message}");
                    }
                }

                InhLog($"{dateien.Count} Datei(en) werden durchsucht...");

                if (modus != DateiOperationModus.Suchlauf && !Directory.Exists(zielpfad))
                {
                    Directory.CreateDirectory(zielpfad);
                    InhLog($"Zielverzeichnis erstellt: {zielpfad}");
                }

                for (int i = 0; i < dateien.Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    string datei = dateien[i];
                    string dateiname = Path.GetFileName(datei);

                    // Inhalt im Hintergrund lesen und pruefen
                    var ergebnis = await Task.Run(() =>
                    {
                        var inhalt = DateiInhaltsLeser.LiesText(datei, out string? leseFehler);
                        if (inhalt == null)
                        {
                            return (Treffer: false, Fundstelle: "", Fehler: leseFehler);
                        }

                        bool passt = InhaltsSuche.Passt(inhalt, suchzeilen, modusAuswahl, grossKlein, platzhalter, out string fund);
                        return (Treffer: passt, Fundstelle: fund, Fehler: (string?)null);
                    }, token);

                    verarbeitet++;

                    int prozent = (int)((verarbeitet / (double)dateien.Count) * 100);
                    progressBarInh.Value = prozent;
                    txtInhFortschritt.Text = $"{verarbeitet} von {dateien.Count} geprüft - {treffer} Treffer";

                    if (ergebnis.Fehler != null)
                    {
                        InhLog($"Nicht lesbar: {dateiname} ({ergebnis.Fehler})");
                        fehler++;
                        continue;
                    }

                    if (!ergebnis.Treffer)
                    {
                        continue;
                    }

                    treffer++;

                    // Betroffene Dateien bestimmen
                    var betroffene = new List<string> { datei };
                    if (gleichnamige)
                    {
                        string ordner = Path.GetDirectoryName(datei) ?? "";
                        string basis = Path.GetFileNameWithoutExtension(datei);
                        betroffene = Directory.GetFiles(ordner, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => Path.GetFileNameWithoutExtension(f).Equals(basis, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }

                    if (modus == DateiOperationModus.Suchlauf)
                    {
                        InhLog($"Treffer: {dateiname}");
                        inhaltsTreffer.Add(new InhaltsTreffer
                        {
                            Dateiname = dateiname,
                            Status = gleichnamige && betroffene.Count > 1 ? $"Gefunden (+{betroffene.Count - 1} gleichnamige)" : "Gefunden",
                            Fundstelle = ergebnis.Fundstelle,
                            AnzeigePfad = Path.GetDirectoryName(datei) ?? "",
                            VollstaendigerPfad = datei
                        });
                        continue;
                    }

                    // Kopieren oder Verschieben
                    foreach (var quelle in betroffene)
                    {
                        string status = InhDateiUebertragen(quelle, zielpfad, modus, konfliktAktion, out string zielPfadDatei);

                        inhaltsTreffer.Add(new InhaltsTreffer
                        {
                            Dateiname = Path.GetFileName(quelle),
                            Status = status,
                            Fundstelle = quelle == datei ? ergebnis.Fundstelle : "(gleichnamige Datei)",
                            AnzeigePfad = zielpfad,
                            VollstaendigerPfad = File.Exists(zielPfadDatei) ? zielPfadDatei : quelle
                        });
                    }
                }

                txtInhFortschritt.Text = $"Fertig: {verarbeitet} geprüft, {treffer} Treffer";
                InhLog($"--- Fertig: {verarbeitet} Datei(en) geprüft, {treffer} Treffer, {fehler} nicht lesbar ---");
                txtStatus.Text = $"Inhaltssuche: {treffer} Treffer";
            }
            catch (OperationCanceledException)
            {
                InhLog($"--- Abgebrochen nach {verarbeitet} Datei(en), {treffer} Treffer ---");
                txtInhFortschritt.Text = "Abgebrochen";
            }
            catch (Exception ex)
            {
                InhLog($"SCHWERER FEHLER: {ex.Message}");
                MessageBox.Show($"Fehler bei der Verarbeitung:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InhButtonsAktivieren(true);
                inhAbbruch?.Dispose();
                inhAbbruch = null;
            }
        }

        /// <summary>
        /// Kopiert oder verschiebt eine Datei und behandelt dabei Namenskonflikte.
        /// </summary>
        private string InhDateiUebertragen(string quelle, string zielpfad, DateiOperationModus modus, int konfliktAktion, out string zielDatei)
        {
            string dateiname = Path.GetFileName(quelle);
            zielDatei = Path.Combine(zielpfad, dateiname);
            string aktionText = modus == DateiOperationModus.Kopieren ? "Kopiert" : "Verschoben";

            try
            {
                if (File.Exists(zielDatei))
                {
                    switch (konfliktAktion)
                    {
                        case 0: // Überspringen
                            InhLog($"Übersprungen (existiert bereits): {dateiname}");
                            return "Übersprungen";

                        case 1: // Automatisch umbenennen
                            zielDatei = InhFreienNamenFinden(zielpfad, dateiname);
                            InhLog($"{aktionText} (umbenannt): {dateiname} -> {Path.GetFileName(zielDatei)}");
                            break;

                        default: // Überschreiben
                            File.Delete(zielDatei);
                            InhLog($"{aktionText} (überschrieben): {dateiname}");
                            break;
                    }
                }
                else
                {
                    InhLog($"{aktionText}: {dateiname}");
                }

                if (modus == DateiOperationModus.Kopieren)
                {
                    File.Copy(quelle, zielDatei);
                }
                else
                {
                    File.Move(quelle, zielDatei);
                }

                return aktionText;
            }
            catch (Exception ex)
            {
                InhLog($"FEHLER bei '{dateiname}': {ex.Message}");
                return $"Fehler: {ex.Message}";
            }
        }

        /// <summary>
        /// Ermittelt einen freien Zieldateinamen nach dem Muster "Name (1).ext".
        /// </summary>
        private static string InhFreienNamenFinden(string zielpfad, string dateiname)
        {
            string basis = Path.GetFileNameWithoutExtension(dateiname);
            string endung = Path.GetExtension(dateiname);

            for (int i = 1; i < 10000; i++)
            {
                string kandidat = Path.Combine(zielpfad, $"{basis} ({i}){endung}");
                if (!File.Exists(kandidat))
                {
                    return kandidat;
                }
            }

            return Path.Combine(zielpfad, $"{basis} ({Guid.NewGuid():N}){endung}");
        }

        #endregion

        #region Konfiguration

        private void InhKonfigurationLaden(PfadKonfiguration konfiguration)
        {
            txtInhSuchpfad.Text = konfiguration.InhSuchpfad;

            lstInhSuchpfade.Items.Clear();
            foreach (var pfad in konfiguration.InhSuchpfadListe)
            {
                lstInhSuchpfade.Items.Add(pfad);
            }

            txtInhZielpfad.Text = konfiguration.InhZielpfad;
            chkInhUseMoveFolder.IsChecked = konfiguration.InhUseMoveFolder;
            chkInhUnterordner.IsChecked = konfiguration.InhUnterordner;
            txtInhSuchbegriff.Text = konfiguration.InhSuchbegriff;
            InhEndungenSetzen(konfiguration.InhDateiEndungen);
            cmbInhSuchModus.SelectedIndex = konfiguration.InhSuchModus;
            chkInhGrossKlein.IsChecked = konfiguration.InhGrossKleinBeachten;
            chkInhPlatzhalter.IsChecked = konfiguration.InhPlatzhalter;
            chkInhGleichnamige.IsChecked = konfiguration.InhGleichnamigeMitnehmen;
            cmbInhKonflikt.SelectedIndex = konfiguration.InhKonfliktAktion;
        }

        private void InhKonfigurationSpeichern(PfadKonfiguration konfiguration)
        {
            konfiguration.InhSuchpfad = txtInhSuchpfad.Text;
            konfiguration.InhSuchpfadListe = lstInhSuchpfade.Items.Cast<string>().ToList();
            konfiguration.InhZielpfad = txtInhZielpfad.Text;
            konfiguration.InhUseMoveFolder = chkInhUseMoveFolder.IsChecked == true;
            konfiguration.InhUnterordner = chkInhUnterordner.IsChecked == true;
            konfiguration.InhSuchbegriff = txtInhSuchbegriff.Text;
            konfiguration.InhDateiEndungen = InhEndungenText();
            konfiguration.InhSuchModus = Math.Max(0, cmbInhSuchModus.SelectedIndex);
            konfiguration.InhGrossKleinBeachten = chkInhGrossKlein.IsChecked == true;
            konfiguration.InhPlatzhalter = chkInhPlatzhalter.IsChecked == true;
            konfiguration.InhGleichnamigeMitnehmen = chkInhGleichnamige.IsChecked == true;
            konfiguration.InhKonfliktAktion = Math.Max(0, cmbInhKonflikt.SelectedIndex);
        }

        #endregion
    }
}
