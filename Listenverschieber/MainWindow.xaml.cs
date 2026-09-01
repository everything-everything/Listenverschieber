using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Listenverschieber
{
    public enum DateiOperationModus
    {
        Suchlauf,
        Kopieren,
        Verschieben
    }

    public class DateiEintrag
    {
        public string Dateiname { get; set; } = "";
        public string Status { get; set; } = "";
        public string VollstaendigerPfad { get; set; } = "";
        public string AnzeigePfad { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        // Daten für Tab 1 (Listenverschieber)
        private List<string> dateiListe = new List<string>();
        private List<string[]> csvData = new List<string[]>();
        private string[] csvHeaders = Array.Empty<string>();
        private int selectedCsvColumnIndex = -1;
        private bool isProcessing = false;

        private List<string> verschobeneDateienListe = new List<string>();
        private List<string> nichtGefundeneDateienListe = new List<string>();

        /// <summary>Merkt sich die zuletzt ausgefuehrte Aktion fuer die Beschriftung im Exportdialog.</summary>
        private ExportListenModus letzterExportModus = ExportListenModus.Verschieben;

        // Protokolle
        private readonly List<string> suchProtokoll = new List<string>();
        private readonly List<string> kopierProtokoll = new List<string>();

        // Daten für Tab 2 (Unvollständige Dateien)
        private List<VerschobeneDateiInfo> verschobeneDateienInfo = new List<VerschobeneDateiInfo>();
        private DispatcherTimer rueckschiebeTimer = new DispatcherTimer();
        private int countdownVerbleibend = 0;
        private int countdownGesamt = 0;
        private TaskCompletionSource<bool>? countdownTcs;

        // ObservableCollections für DataGrid
        public ObservableCollection<DateiEintrag> GefundeneDateien { get; } = new ObservableCollection<DateiEintrag>();
        public ObservableCollection<DateiEintrag> GefundeneDateienTab2 { get; } = new ObservableCollection<DateiEintrag>();

        public MainWindow()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitUmbenennenTab();
            InitInhaltssucheTab();
            LadeKonfiguration();

            rueckschiebeTimer.Tick += RueckschiebeTimer_Tick;

            DataContext = this;
        }

        #region Pfad-Konfiguration (global)
        private void cmbTeilnameModus_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AktualisiereTeilnameUi();
        }

        private void chkTrennzeichenSuche_Changed(object sender, RoutedEventArgs e)
        {
            AktualisiereTeilnameUi();
        }

        private void AktualisiereTeilnameUi()
        {
            if (txtTeilnameBeschreibung == null || grpTrennzeichen == null)
                return;

            bool teilnameAktiv = cmbTeilnameModus.SelectedIndex > 0;
            grpTrennzeichen.IsEnabled = teilnameAktiv;

            string basis = cmbTeilnameModus.SelectedIndex switch
            {
                1 => "Der Listeneintrag ist der Anfang des Dateinamens. '12345' findet '12345_Rechnung.PDF'.",
                2 => "Der Listeneintrag ist das Ende des Dateinamens. 'Rechnung.PDF' findet '12345_Rechnung.PDF'.",
                _ => "Es wird nur nach exakt übereinstimmenden Dateinamen gesucht."
            };

            if (teilnameAktiv && chkTrennzeichenSuche?.IsChecked == true)
            {
                basis += cmbTeilnameModus.SelectedIndex == 1
                    ? " Abschnitte werden von vorne gezählt."
                    : " Abschnitte werden von hinten gezählt.";
            }

            txtTeilnameBeschreibung.Text = basis;
        }

        private static readonly char[] TrennzeichenKandidaten = { '_', '-', '.', ',', ';', ' ' };

        private static string ErmittleTrennzeichen(string text, bool automatisch, string manuell)
        {
            if (!automatisch)
                return string.IsNullOrEmpty(manuell) ? "_" : manuell;

            char bestes = '_';
            int besteAnzahl = 0;
            foreach (char kandidat in TrennzeichenKandidaten)
            {
                int anzahl = text.Count(c => c == kandidat);
                if (anzahl > besteAnzahl)
                {
                    besteAnzahl = anzahl;
                    bestes = kandidat;
                }
            }
            return bestes.ToString();
        }

        /// <summary>
        /// Prueft, ob der Dateiname anhand der Abschnitts-/Trennzeichenlogik zum Listeneintrag passt.
        /// </summary>
        private static bool TrennzeichenTreffer(string dateiName, string suchName, bool trennzeichenAuto,
            string trennzeichenManuell, bool abschnittAuto, int abschnittNummer, bool einzelabschnitt,
            bool vonVorne, bool vonHinten)
        {
            string trenner = ErmittleTrennzeichen(suchName, trennzeichenAuto, trennzeichenManuell);
            if (string.IsNullOrEmpty(trenner))
                return false;

            var dateiTeile = dateiName.Split(new[] { trenner }, StringSplitOptions.None);
            var suchTeile = suchName.Split(new[] { trenner }, StringSplitOptions.None);
            if (dateiTeile.Length == 0 || suchTeile.Length == 0)
                return false;

            int maxAbschnitte = Math.Min(dateiTeile.Length, suchTeile.Length);
            IEnumerable<int> abschnitte = abschnittAuto
                ? Enumerable.Range(1, maxAbschnitte)
                : new[] { abschnittNummer };

            foreach (int n in abschnitte)
            {
                if (n < 1 || n > dateiTeile.Length)
                    continue;

                if (vonVorne)
                {
                    string abschnittDatei = einzelabschnitt
                        ? dateiTeile[n - 1]
                        : string.Join(trenner, dateiTeile.Take(n));
                    // Der Listeneintrag wird auf dieselbe Abschnittszahl gekuerzt.
                    // Ist er kuerzer als n Abschnitte, wird er vollstaendig verglichen.
                    string abschnittSuche = n > suchTeile.Length
                        ? suchName
                        : (einzelabschnitt ? suchTeile[n - 1] : string.Join(trenner, suchTeile.Take(n)));
                    if (abschnittDatei.Equals(abschnittSuche, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                if (vonHinten)
                {
                    string abschnittDatei = einzelabschnitt
                        ? dateiTeile[dateiTeile.Length - n]
                        : string.Join(trenner, dateiTeile.Skip(dateiTeile.Length - n));
                    string abschnittSuche = n > suchTeile.Length
                        ? suchName
                        : (einzelabschnitt ? suchTeile[suchTeile.Length - n] : string.Join(trenner, suchTeile.Skip(suchTeile.Length - n)));
                    if (abschnittDatei.Equals(abschnittSuche, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private void LadeKonfiguration()
        {
            var config = PfadKonfiguration.Laden();

            // Tab 1 - Listenverschieber
            txtArbeitspfad.Text = config.Arbeitspfad;
            txtVerschiebepfad.Text = config.Verschiebepfad;
            chkUseMoveFolder.IsChecked = config.UseMoveFolder;
            lstArbeitspfade.ItemsSource = null;
            lstArbeitspfade.ItemsSource = config.ArbeitspfadListe;
            chkIgnoreExtensionInList.IsChecked = config.IgnoreExtensionInList;
            cmbTeilnameModus.SelectedIndex = config.NameBeginntMit ? 1 : (config.NameEndetMit ? 2 : 0);
            chkProcessAllPaths.IsChecked = config.ProcessAllPaths;
            chkTrennzeichenSuche.IsChecked = config.TrennzeichenSuche;
            chkTrennzeichenAuto.IsChecked = config.TrennzeichenAuto;
            txtTrennzeichen.Text = config.TrennzeichenManuell;
            chkAbschnittAuto.IsChecked = config.AbschnittAuto;
            txtAbschnittNummer.Text = config.AbschnittNummer.ToString();
            chkEinzelabschnittsuche.IsChecked = config.Einzelabschnittsuche;
            chkDuplikateFiltern.IsChecked = config.DuplikateFiltern;

            // Tab 2 - Unvollständige Dateien
            txtUeberwachungspfad.Text = config.Ueberwachungspfad;
            txtVerschiebepfadTab2.Text = config.VerschiebepfadTab2;
            chkUseMoveFolderTab2.IsChecked = config.UseMoveFolderTab2;
            lstUeberwachungspfade.ItemsSource = null;
            lstUeberwachungspfade.ItemsSource = config.UeberwachungspfadListe;
            txtHauptformat.Text = config.Hauptformat;
            txtPflichtdatei1.Text = config.Pflichtdatei1;
            txtPflichtdatei2.Text = config.Pflichtdatei2;
            chkAutoRueckverschiebung.IsChecked = config.AutoRueckverschiebung;
            txtRueckschiebeZeit.Text = config.RueckschiebeZeitSekunden.ToString();
            chkProcessAllWatchPaths.IsChecked = config.ProcessAllWatchPaths;

            // Tab 3 - Dateien umbenennen
            UmbKonfigurationLaden(config);

            // Tab 4 - Inhaltssuche
            InhKonfigurationLaden(config);

            AktualisiereTeilnameUi();
        }

        private void btnPfadeSpeichern_Click(object sender, RoutedEventArgs e)
        {
            var config = new PfadKonfiguration
            {
                // Tab 1 - Listenverschieber
                Arbeitspfad = txtArbeitspfad.Text,
                Verschiebepfad = txtVerschiebepfad.Text,
                UseMoveFolder = chkUseMoveFolder.IsChecked == true,
                ArbeitspfadListe = lstArbeitspfade.ItemsSource as List<string> ?? new List<string>(),
                IgnoreExtensionInList = chkIgnoreExtensionInList.IsChecked == true,
                NameBeginntMit = cmbTeilnameModus.SelectedIndex == 1,
                NameEndetMit = cmbTeilnameModus.SelectedIndex == 2,
                ProcessAllPaths = chkProcessAllPaths.IsChecked == true,
                TrennzeichenSuche = chkTrennzeichenSuche.IsChecked == true,
                TrennzeichenAuto = chkTrennzeichenAuto.IsChecked == true,
                TrennzeichenManuell = txtTrennzeichen.Text,
                AbschnittAuto = chkAbschnittAuto.IsChecked == true,
                AbschnittNummer = int.TryParse(txtAbschnittNummer.Text, out int abschnittNr) ? abschnittNr : 1,
                Einzelabschnittsuche = chkEinzelabschnittsuche.IsChecked == true,
                DuplikateFiltern = chkDuplikateFiltern.IsChecked == true,

                // Tab 2 - Unvollständige Dateien
                Ueberwachungspfad = txtUeberwachungspfad.Text,
                VerschiebepfadTab2 = txtVerschiebepfadTab2.Text,
                UseMoveFolderTab2 = chkUseMoveFolderTab2.IsChecked == true,
                UeberwachungspfadListe = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>(),
                Hauptformat = txtHauptformat.Text,
                Pflichtdatei1 = txtPflichtdatei1.Text,
                Pflichtdatei2 = txtPflichtdatei2.Text,
                AutoRueckverschiebung = chkAutoRueckverschiebung.IsChecked == true,
                RueckschiebeZeitSekunden = int.TryParse(txtRueckschiebeZeit.Text, out int sek) ? sek : 10,
                ProcessAllWatchPaths = chkProcessAllWatchPaths.IsChecked == true
            };

            // Tab 3 - Dateien umbenennen
            UmbKonfigurationSpeichern(config);

            // Tab 4 - Inhaltssuche
            InhKonfigurationSpeichern(config);

            config.Speichern();
            LogMessage("Konfiguration gespeichert");
            System.Windows.MessageBox.Show("Konfiguration wurde gespeichert!", "Gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnPfadeLaden_Click(object sender, RoutedEventArgs e)
        {
            LadeKonfiguration();
            LogMessage("Pfad-Konfiguration geladen");
        }
        #endregion

        #region Mehrere Pfade - Listenverschieber (Tab 1)
        private void lstArbeitspfade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnArbeitspfadEntfernen.IsEnabled = lstArbeitspfade.SelectedIndex >= 0;
            if (lstArbeitspfade.SelectedItem is string selectedPath)
            {
                txtArbeitspfad.Text = selectedPath;
                UpdateVerschiebepfadIfMoveFolder();
            }
        }

        private void btnArbeitspfadHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtArbeitspfad.Text))
            {
                System.Windows.MessageBox.Show("Bitte einen Arbeitspfad angeben!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var liste = lstArbeitspfade.ItemsSource as List<string> ?? new List<string>();
            if (liste.Contains(txtArbeitspfad.Text))
            {
                System.Windows.MessageBox.Show(" Dieser Pfad ist bereits in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            liste.Add(txtArbeitspfad.Text);
            lstArbeitspfade.ItemsSource = null;
            lstArbeitspfade.ItemsSource = liste;
            LogMessage($"Arbeitspfad zur Liste hinzugefügt: {txtArbeitspfad.Text}");
        }

        private void btnArbeitspfadEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (lstArbeitspfade.SelectedItem is string selectedPath)
            {
                var liste = lstArbeitspfade.ItemsSource as List<string> ?? new List<string>();
                liste.Remove(selectedPath);
                lstArbeitspfade.ItemsSource = null;
                lstArbeitspfade.ItemsSource = liste;
                LogMessage($"Arbeitspfad aus Liste entfernt: {selectedPath}");
            }
        }
        #endregion

        #region Mehrere Pfade - Unvollständige Dateien (Tab 2)
        private void lstUeberwachungspfade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnUeberwachungspfadEntfernen.IsEnabled = lstUeberwachungspfade.SelectedIndex >= 0;
            if (lstUeberwachungspfade.SelectedItem is string selectedPath)
            {
                txtUeberwachungspfad.Text = selectedPath;
                UpdateVerschiebepfadTab2IfMoveFolder();
            }
        }

        private void btnUeberwachungspfadHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUeberwachungspfad.Text))
            {
                System.Windows.MessageBox.Show("Bitte einen Überwachungspfad angeben!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var liste = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>();
            if (liste.Contains(txtUeberwachungspfad.Text))
            {
                System.Windows.MessageBox.Show("Dieser Pfad ist bereits in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            liste.Add(txtUeberwachungspfad.Text);
            lstUeberwachungspfade.ItemsSource = null;
            lstUeberwachungspfade.ItemsSource = liste;
            LogMessage2($"Überwachungspfad zur Liste hinzugefügt: {txtUeberwachungspfad.Text}");
        }

        private void btnUeberwachungspfadEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (lstUeberwachungspfade.SelectedItem is string selectedPath)
            {
                var liste = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>();
                liste.Remove(selectedPath);
                lstUeberwachungspfade.ItemsSource = null;
                lstUeberwachungspfade.ItemsSource = liste;
                LogMessage2($"Überwachungspfad aus Liste entfernt: {selectedPath}");
            }
        }
        #endregion

        #region Listenverschieber (Tab 1)
        private void btnArbeitspfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Arbeitspfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtArbeitspfad.Text = dialog.SelectedPath;
                UpdateVerschiebepfadIfMoveFolder();
                LogMessage($"Arbeitspfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnVerschiebepfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Verschiebepfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtVerschiebepfad.Text = dialog.SelectedPath;
                LogMessage($"Verschiebepfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void chkUseMoveFolder_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateVerschiebepfadIfMoveFolder();
        }

        private void UpdateVerschiebepfadIfMoveFolder()
        {
            if (chkUseMoveFolder.IsChecked == true && !string.IsNullOrWhiteSpace(txtArbeitspfad.Text))
            {
                txtVerschiebepfad.Text = Path.Combine(txtArbeitspfad.Text, "Move");
                LogMessage($"Verschiebepfad auf 'Move'-Unterverzeichnis gesetzt: {txtVerschiebepfad.Text}");
            }
        }

        private void btnListeLaden_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Textdateiliste auswählen",
                Filter = "Textdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    pnlCsvSpalte.Visibility = Visibility.Collapsed;
                    txtListendatei.Text = dialog.FileName;
                    dateiListe = LoadFileList(dialog.FileName);
                    LogMessage($"TXT-Liste geladen: {dialog.FileName}");
                    LogMessage($"Anzahl Einträge: {dateiListe.Count}");
                    UpdateButtonState();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Laden der Liste: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER: {ex.Message}");
                }
            }
        }

        private void btnOrdnerListeLaden_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OrdnerListeDialog(txtArbeitspfad.Text) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            pnlCsvSpalte.Visibility = Visibility.Collapsed;
            dateiListe = dialog.Dateiliste;
            txtListendatei.Text = $"{dialog.Ordner} ({dialog.Suchmuster})";
            LogMessage($"Liste aus Ordner geladen: {dialog.Ordner} | Muster: {dialog.Suchmuster}" +
                       (dialog.UnterordnerEinbeziehen ? " | inkl. Unterordner" : ""));
            LogMessage($"Anzahl Einträge: {dateiListe.Count}");
            UpdateButtonState();
        }

        private void btnCsvLaden_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "CSV-Datei auswählen",
                Filter = "CSV-Dateien (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    txtListendatei.Text = dialog.FileName;
                    LoadCsvFile(dialog.FileName);
                    LogMessage($"CSV-Datei geladen: {dialog.FileName}");
                    LogMessage($"Anzahl Spalten: {csvHeaders.Length}");
                    LogMessage($"Anzahl Zeilen: {csvData.Count}");

                    pnlCsvSpalte.Visibility = Visibility.Visible;
                    cmbCsvSpalte.Items.Clear();
                    for (int i = 0; i < csvHeaders.Length; i++)
                        cmbCsvSpalte.Items.Add($"{csvHeaders[i]} (Spalte {i + 1})");

                    int defaultIndex = Array.FindIndex(csvHeaders, h => h.Equals("Beleg_Dateiname", StringComparison.OrdinalIgnoreCase));
                    if (defaultIndex >= 0)
                    {
                        cmbCsvSpalte.SelectedIndex = defaultIndex;
                        LogMessage("Standard-Spalte 'Beleg_Dateiname' ausgewählt");
                    }
                    else if (csvHeaders.Length > 0)
                    {
                        cmbCsvSpalte.SelectedIndex = 0;
                        LogMessage($"Erste Spalte '{csvHeaders[0]}' ausgewählt");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Laden der CSV-Datei: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER: {ex.Message}");
                    pnlCsvSpalte.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void cmbCsvSpalte_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCsvSpalte.SelectedIndex >= 0)
            {
                selectedCsvColumnIndex = cmbCsvSpalte.SelectedIndex;
                dateiListe = csvData
                    .Select(row => selectedCsvColumnIndex < row.Length ? row[selectedCsvColumnIndex] : "")
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
                LogMessage($"Spalte '{csvHeaders[selectedCsvColumnIndex]}' ausgewählt");
                LogMessage($"Anzahl gültiger Einträge: {dateiListe.Count}");
                txtCsvInfo.Text = $"({dateiListe.Count} Einträge)";
                UpdateButtonState();
            }
        }

        private void UpdateButtonState()
        {
            bool canProcess = !isProcessing && dateiListe.Count > 0 &&
                              !string.IsNullOrWhiteSpace(txtArbeitspfad.Text) &&
                              !string.IsNullOrWhiteSpace(txtVerschiebepfad.Text);
            btnDateienSuchlauf.IsEnabled = canProcess;
            btnDateienKopieren.IsEnabled = canProcess;
            btnDateienVerschieben.IsEnabled = canProcess;
        }

        private void LogMessage(string message)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
            txtLog.ScrollToEnd();

            // Protokolle mitschreiben
            if (message.StartsWith("[Suchlauf]", StringComparison.OrdinalIgnoreCase) || message.StartsWith("=== Start Suchlauf", StringComparison.OrdinalIgnoreCase))
                suchProtokoll.Add(message);
            if (message.StartsWith("Kopiert", StringComparison.OrdinalIgnoreCase) || message.StartsWith("Verschoben", StringComparison.OrdinalIgnoreCase) || message.StartsWith("=== Start ", StringComparison.OrdinalIgnoreCase))
                kopierProtokoll.Add(message);
        }

        private void LogMessage2(string message) => LogMessage(message);

        private List<string> LoadFileList(string filePath)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private void LoadCsvFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length == 0)
            {
                csvHeaders = Array.Empty<string>();
                csvData = new List<string[]>();
                return;
            }
            csvHeaders = lines[0].Split(';');
            csvData = lines
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(';'))
                .ToList();
        }
        #endregion

        #region Unvollständige Dateien (Tab 2)
        private void btnUeberwachungspfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Überwachungspfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtUeberwachungspfad.Text = dialog.SelectedPath;
                UpdateVerschiebepfadTab2IfMoveFolder();
                LogMessage2($"Überwachungspfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnVerschiebepfadDurchsuchenTab2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog { Description = "Verschiebepfad auswählen", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtVerschiebepfadTab2.Text = dialog.SelectedPath;
                LogMessage2($"Verschiebepfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void chkUseMoveFolderTab2_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateVerschiebepfadTab2IfMoveFolder();
        }

        private void UpdateVerschiebepfadTab2IfMoveFolder()
        {
            if (chkUseMoveFolderTab2.IsChecked == true && !string.IsNullOrWhiteSpace(txtUeberwachungspfad.Text))
            {
                txtVerschiebepfadTab2.Text = Path.Combine(txtUeberwachungspfad.Text, "Move");
                LogMessage2($"Verschiebepfad auf 'Move'-Unterverzeichnis gesetzt: {txtVerschiebepfadTab2.Text}");
            }
        }

        private async void btnUnvollstaendigeSuchlauf_Click(object sender, RoutedEventArgs e)
            => await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Suchlauf);
        private async void btnUnvollstaendigeKopieren_Click(object sender, RoutedEventArgs e)
            => await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Kopieren);
        private async void btnUnvollstaendigeVerschieben_Click(object sender, RoutedEventArgs e)
            => await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Verschieben);

        private async Task UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus modus)
        {
            bool processAllPaths = chkProcessAllWatchPaths?.IsChecked == true;

            // Auto-Rückverschiebung prüfen
            int countdownSekunden = 0;
            bool autoReturn = modus == DateiOperationModus.Verschieben
                              && chkAutoRueckverschiebung.IsChecked == true
                              && int.TryParse(txtRueckschiebeZeit.Text, out countdownSekunden)
                              && countdownSekunden > 0;

            if (processAllPaths)
            {
                var pfadListe = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>();
                if (pfadListe.Count == 0)
                {
                    System.Windows.MessageBox.Show("Keine Pfade in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                for (int p = 0; p < pfadListe.Count; p++)
                {
                    var pfad = pfadListe[p];
                    if (modus != DateiOperationModus.Suchlauf) verschobeneDateienInfo.Clear();

                    txtUeberwachungspfad.Text = pfad;
                    UpdateVerschiebepfadTab2IfMoveFolder();
                    LogMessage2($"\n=== Verarbeite Pfad {p + 1} von {pfadListe.Count}: {pfad} ===");
                    await UnvollstaendigeDateienVerarbeitenInPfadAsync(pfad, modus);

                    // Per-Pfad Countdown + Rückverschiebung
                    if (autoReturn && verschobeneDateienInfo.Count > 0)
                    {
                        bool shouldMoveBack = await CountdownStartenAsync(countdownSekunden);
                        if (shouldMoveBack)
                        {
                            await DateienZurueckschiebenAsync();
                        }
                    }
                }
                LogMessage2("\n=== Alle Pfade verarbeitet ===");
                System.Windows.MessageBox.Show("Alle Pfade wurden verarbeitet!", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtUeberwachungspfad.Text) || !Directory.Exists(txtUeberwachungspfad.Text))
                {
                    System.Windows.MessageBox.Show("Bitte gültigen Überwachungspfad angeben!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (modus != DateiOperationModus.Suchlauf) verschobeneDateienInfo.Clear();
                await UnvollstaendigeDateienVerarbeitenInPfadAsync(txtUeberwachungspfad.Text, modus);

                // Einzelpfad Countdown + Rückverschiebung
                if (autoReturn && verschobeneDateienInfo.Count > 0)
                {
                    bool shouldMoveBack = await CountdownStartenAsync(countdownSekunden);
                    if (shouldMoveBack)
                    {
                        await DateienZurueckschiebenAsync();
                    }
                }
            }
        }

        private async Task UnvollstaendigeDateienVerarbeitenInPfadAsync(string ueberwachungspfad, DateiOperationModus modus)
        {
            grpFortschritt2.Visibility = Visibility.Visible;
            btnUnvollstaendigeSuchlauf.IsEnabled = false;
            btnUnvollstaendigeKopieren.IsEnabled = false;
            btnUnvollstaendigeVerschieben.IsEnabled = false;

            await Task.Run(() => UnvollstaendigeDateienVerarbeitenCoreAsync(ueberwachungspfad, modus));

            grpFortschritt2.Visibility = Visibility.Collapsed;
            btnUnvollstaendigeSuchlauf.IsEnabled = true;
            btnUnvollstaendigeKopieren.IsEnabled = true;
            btnUnvollstaendigeVerschieben.IsEnabled = true;
            btnDateienZurueckschieben.IsEnabled = verschobeneDateienInfo.Count > 0;
        }

        private async Task UnvollstaendigeDateienVerarbeitenCoreAsync(string ueberwachungspfad, DateiOperationModus modus)
        {
            string verschiebepfad = "", hauptformat = "", pflicht1 = "", pflicht2 = "";
            string modusText = modus switch
            {
                DateiOperationModus.Suchlauf => "Suchlauf",
                DateiOperationModus.Kopieren => "Kopieren",
                DateiOperationModus.Verschieben => "Verschieben",
                _ => "Verarbeiten"
            };

            await Dispatcher.InvokeAsync(() =>
            {
                GefundeneDateienTab2.Clear();
                verschiebepfad = chkUseMoveFolderTab2.IsChecked == true
                    ? Path.Combine(ueberwachungspfad, "Move")
                    : txtVerschiebepfadTab2.Text;
                hauptformat = txtHauptformat.Text.Trim().ToLower();
                pflicht1 = txtPflichtdatei1.Text.Trim().ToLower();
                pflicht2 = txtPflichtdatei2.Text.Trim().ToLower();
                LogMessage2($"\n=== Start {modusText}: Unvollständige Dateien ===");
                LogMessage2($"Überwachungspfad: {ueberwachungspfad}");
                LogMessage2($"Verschiebepfad: {verschiebepfad}");
                LogMessage2($"Hauptformat: {(hauptformat == "*" ? "Alle" : hauptformat)}");
                LogMessage2($"Pflichtdatei 1 (erforderlich): .{pflicht1}");
                if (!string.IsNullOrWhiteSpace(pflicht2))
                    LogMessage2($"Pflichtdatei 2 (optional): .{pflicht2}");
                else
                    LogMessage2("Pflichtdatei 2: Nicht verwendet");
            });

            if (modus != DateiOperationModus.Suchlauf && !Directory.Exists(verschiebepfad))
            {
                try
                {
                    Directory.CreateDirectory(verschiebepfad);
                    await Dispatcher.InvokeAsync(() => LogMessage2($"Verschiebepfad erstellt: { verschiebepfad}"));
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LogMessage2($"FEHLER: Kann Verschiebepfad nicht erstellen: {ex.Message}");
                        System.Windows.MessageBox.Show($"Fehler beim Erstellen des Verschiebepfads:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }
            }

            var alleDateien = Directory.GetFiles(ueberwachungspfad, "*.*", SearchOption.TopDirectoryOnly);
            var gruppen = alleDateien.GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase).ToList();
            await Dispatcher.InvokeAsync(() => { progressBar2.Maximum = gruppen.Count; progressBar2.Value = 0; });

            int verarbeiteteCount = 0;
            int processed = 0;

            // Exportlisten fuer diesen Lauf zuruecksetzen und Modus merken.
            await Dispatcher.InvokeAsync(() =>
            {
                verschobeneDateienListe.Clear();
                nichtGefundeneDateienListe.Clear();
                letzterExportModus = modus switch
                {
                    DateiOperationModus.Suchlauf => ExportListenModus.Suchlauf,
                    DateiOperationModus.Kopieren => ExportListenModus.Kopieren,
                    _ => ExportListenModus.Verschieben
                };
            });

            foreach (var gruppe in gruppen)
            {
                processed++;
                await Dispatcher.InvokeAsync(() =>
                {
                    progressBar2.Value = processed;
                    txtFortschritt2.Text = $"Prüfe {processed} von {gruppen.Count}: {gruppe.Key}";
                });

                var dateien = gruppe.ToList();
                var hauptdateien = hauptformat == "*"
                    ? dateien
                    : dateien.Where(f => Path.GetExtension(f).TrimStart('.').Equals(hauptformat, StringComparison.OrdinalIgnoreCase)).ToList();
                if (hauptdateien.Count == 0) continue;

                bool hatPflicht1 = dateien.Any(f => Path.GetExtension(f).TrimStart('.').Equals(pflicht1, StringComparison.OrdinalIgnoreCase));
                bool hatPflicht2 = string.IsNullOrWhiteSpace(pflicht2) || dateien.Any(f => Path.GetExtension(f).TrimStart('.').Equals(pflicht2, StringComparison.OrdinalIgnoreCase));
                bool sollVerschieben = !hatPflicht1 || (!string.IsNullOrWhiteSpace(pflicht2) && !hatPflicht2);
                if (!sollVerschieben) continue;

                var info = new VerschobeneDateiInfo { VerschobenAm = DateTime.Now, QuellOrdner = ueberwachungspfad };
                string fehlendeDateien = "";
                if (!hatPflicht1) fehlendeDateien += $".{pflicht1}";
                if (!string.IsNullOrWhiteSpace(pflicht2) && !hatPflicht2)
                {
                    if (!string.IsNullOrEmpty(fehlendeDateien)) fehlendeDateien += ", ";
                    fehlendeDateien += $".{pflicht2}";
                }

                foreach (var datei in dateien)
                {
                    try
                    {
                        var fileName = Path.GetFileName(datei);
                        var ziel = Path.Combine(verschiebepfad, fileName);

                        if (modus == DateiOperationModus.Suchlauf)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                LogMessage2($"[Suchlauf] Würde {modusText.ToLower()}: {fileName} (fehlt: {fehlendeDateien})");
                                GefundeneDateienTab2.Add(new DateiEintrag
                                {
                                    Dateiname = fileName,
                                    Status = $"? Unvollständig (fehlt: {fehlendeDateien})",
                                    VollstaendigerPfad = datei,
                                    AnzeigePfad = ueberwachungspfad
                                });
                            });
                            verschobeneDateienListe.Add(fileName);
                            verarbeiteteCount++;
                        }
                        else if (File.Exists(ziel))
                        {
                            nichtGefundeneDateienListe.Add(fileName);
                            await Dispatcher.InvokeAsync(() =>
                            {
                                LogMessage2($"Übersprungen (existiert): {fileName}");
                                GefundeneDateienTab2.Add(new DateiEintrag
                                {
                                    Dateiname = fileName,
                                    Status = "? Existiert bereits",
                                    VollstaendigerPfad = ziel,
                                    AnzeigePfad = verschiebepfad
                                });
                            });
                        }
                        else
                        {
                            if (modus == DateiOperationModus.Kopieren)
                            {
                                File.Copy(datei, ziel);
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    LogMessage2($"Kopiert: {fileName} (fehlt: {fehlendeDateien})");
                                    GefundeneDateienTab2.Add(new DateiEintrag
                                    {
                                        Dateiname = fileName,
                                        Status = $"? Kopiert (fehlt: {fehlendeDateien})",
                                        VollstaendigerPfad = ziel,
                                        AnzeigePfad = verschiebepfad
                                    });
                                });
                            }
                            else
                            {
                                File.Move(datei, ziel);
                                info.ZugehoerigeDateien.Add(fileName);
                                if (string.IsNullOrEmpty(info.Quelldatei))
                                {
                                    info.Quelldatei = datei;
                                    info.Zieldatei = ziel;
                                }
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    LogMessage2($"Verschoben: {fileName} (fehlt: {fehlendeDateien})");
                                    GefundeneDateienTab2.Add(new DateiEintrag
                                    {
                                        Dateiname = fileName,
                                        Status = $"? Verschoben (fehlt: {fehlendeDateien})",
                                        VollstaendigerPfad = ziel,
                                        AnzeigePfad = verschiebepfad
                                    });
                                });
                            }
                            verschobeneDateienListe.Add(fileName);
                            verarbeiteteCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        nichtGefundeneDateienListe.Add(Path.GetFileName(datei));
                        await Dispatcher.InvokeAsync(() => LogMessage2($"FEHLER: {Path.GetFileName(datei)} - {ex.Message}"));
                    }
                }

                if (modus == DateiOperationModus.Verschieben && info.ZugehoerigeDateien.Count > 0)
                    verschobeneDateienInfo.Add(info);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                LogMessage2("\n=== Fertig ===");
                LogMessage2($"{modusText}: {verarbeiteteCount} Dateien");
                txtStatus.Text = $"{modusText}: {verarbeiteteCount} Dateien";
                if (modus != DateiOperationModus.Suchlauf)
                    System.Windows.MessageBox.Show($"{modusText} abgeschlossen!\n\n{modusText}e Dateien: {verarbeiteteCount}", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async void btnDateienZurueckschieben_Click(object sender, RoutedEventArgs e)
        {
            if (verschobeneDateienInfo.Count == 0)
            {
                System.Windows.MessageBox.Show("Keine Dateien zum Zurückschieben vorhanden!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Falls Countdown läuft: sofort auslösen (die Schleife übernimmt das Zurückschieben)
            if (countdownTcs != null && !countdownTcs.Task.IsCompleted)
            {
                CountdownStoppen();
                countdownTcs.TrySetResult(true);
            }
            else
            {
                // Kein Countdown aktiv: direkt zurückschieben
                await DateienZurueckschiebenAsync();
            }
        }

        private async Task DateienZurueckschiebenAsync()
        {
            await Dispatcher.InvokeAsync(() => LogMessage2("\n=== Start Rückverschiebung ==="));
            int zurueckCount = 0;

            // Gruppiere nach QuellOrdner, damit pro Pfad geloggt wird
            var nachQuellOrdner = verschobeneDateienInfo.ToList()
                .GroupBy(i => i.QuellOrdner, StringComparer.OrdinalIgnoreCase);

            foreach (var gruppe in nachQuellOrdner)
            {
                string quellOrdner = gruppe.Key;
                await Dispatcher.InvokeAsync(() => LogMessage2($"\nRückverschiebung für Pfad: {quellOrdner}"));

                foreach (var info in gruppe)
                {
                    foreach (var dateiName in info.ZugehoerigeDateien)
                    {
                        try
                        {
                            string quelle = Path.Combine(Path.GetDirectoryName(info.Zieldatei)!, dateiName);
                            string ziel = Path.Combine(quellOrdner, dateiName);
                            if (!File.Exists(quelle)) { await Dispatcher.InvokeAsync(() => LogMessage2($"Übersprungen (Quelle fehlt): {dateiName}")); continue; }
                            if (File.Exists(ziel)) { await Dispatcher.InvokeAsync(() => LogMessage2($"Übersprungen (existiert): {dateiName}")); continue; }
                            File.Move(quelle, ziel);
                            zurueckCount++;
                            await Dispatcher.InvokeAsync(() => LogMessage2($"Zurückgeschoben: {dateiName}"));
                        }
                        catch (Exception ex) { await Dispatcher.InvokeAsync(() => LogMessage2($"FEHLER beim Zurückschieben von {dateiName} - {ex.Message}")); }
                    }
                }
            }

            verschobeneDateienInfo.Clear();
            btnDateienZurueckschieben.IsEnabled = false;
            await Dispatcher.InvokeAsync(() =>
            {
                LogMessage2("\n=== Rückverschiebung Fertig ===");
                LogMessage2($"Zurückgeschoben: {zurueckCount} Dateien");
                txtStatus.Text = $"Zurückgeschoben: {zurueckCount} Dateien";
                System.Windows.MessageBox.Show($"Rückverschiebung abgeschlossen!\n\nZurückgeschobene Dateien: {zurueckCount}", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private Task<bool> CountdownStartenAsync(int sekunden)
        {
            countdownTcs = new TaskCompletionSource<bool>();
            countdownGesamt = sekunden;
            countdownVerbleibend = sekunden;

            progressBarCountdown.Maximum = sekunden;
            progressBarCountdown.Value = sekunden;
            txtCountdown.Text = $"Rückverschiebung in {sekunden} Sekunden...";
            grpCountdown.Visibility = Visibility.Visible;
            btnCountdownAbbrechen.IsEnabled = true;

            rueckschiebeTimer.Interval = TimeSpan.FromSeconds(1);
            rueckschiebeTimer.Start();

            LogMessage2($"Countdown gestartet: {sekunden} Sekunden (für {verschobeneDateienInfo.Count} Dateigruppen)");

            return countdownTcs.Task;
        }

        private void CountdownStoppen()
        {
            rueckschiebeTimer.Stop();
            grpCountdown.Visibility = Visibility.Collapsed;
            btnCountdownAbbrechen.IsEnabled = false;
            countdownVerbleibend = 0;
        }

        private void RueckschiebeTimer_Tick(object? sender, EventArgs e)
        {
            countdownVerbleibend--;

            if (countdownVerbleibend <= 0)
            {
                CountdownStoppen();
                countdownTcs?.TrySetResult(true);
            }
            else
            {
                progressBarCountdown.Value = countdownVerbleibend;
                txtCountdown.Text = $"Rückverschiebung in {countdownVerbleibend} Sekunden...";
            }
        }

        private void btnCountdownAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            CountdownStoppen();
            countdownTcs?.TrySetResult(false);
            LogMessage2("Automatische Rückverschiebung abgebrochen.");
        }

        private void btnCountdownPlus_Click(object sender, RoutedEventArgs e)
        {
            if (countdownVerbleibend <= 0) return;
            countdownVerbleibend += 5;
            countdownGesamt += 5;
            progressBarCountdown.Maximum = countdownGesamt;
            progressBarCountdown.Value = countdownVerbleibend;
            txtCountdown.Text = $"Rückverschiebung in {countdownVerbleibend} Sekunden...";
            LogMessage2($"Countdown +5s ? {countdownVerbleibend} Sekunden verbleibend");
        }

        private void btnCountdownMinus_Click(object sender, RoutedEventArgs e)
        {
            if (countdownVerbleibend <= 0) return;
            countdownVerbleibend = Math.Max(1, countdownVerbleibend - 5);
            progressBarCountdown.Value = countdownVerbleibend;
            txtCountdown.Text = $"Rückverschiebung in {countdownVerbleibend} Sekunden...";
            LogMessage2($"Countdown -5s ? {countdownVerbleibend} Sekunden verbleibend");
        }
        #endregion

        #region Listenverschieber Dateioperationen (Tab 1)
        private async void btnDateienSuchlauf_Click(object sender, RoutedEventArgs e)
            => await DateienVerarbeitenAsync(DateiOperationModus.Suchlauf);
        private async void btnDateienKopieren_Click(object sender, RoutedEventArgs e)
            => await DateienVerarbeitenAsync(DateiOperationModus.Kopieren);
        private async void btnDateienVerschieben_Click(object sender, RoutedEventArgs e)
            => await DateienVerarbeitenAsync(DateiOperationModus.Verschieben);

        private async Task DateienVerarbeitenAsync(DateiOperationModus modus)
        {
            bool processAllPaths = chkProcessAllPaths?.IsChecked == true;
            string modusText = modus switch
            {
                DateiOperationModus.Suchlauf => "Suchlauf",
                DateiOperationModus.Kopieren => "Kopieren",
                DateiOperationModus.Verschieben => "Verschieben",
                _ => "Verarbeiten"
            };

            if (processAllPaths)
            {
                var pfadListe = lstArbeitspfade.ItemsSource as List<string> ?? new List<string>();
                if (pfadListe.Count == 0)
                {
                    System.Windows.MessageBox.Show("Keine Pfade in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                bool useMoveFolder = chkUseMoveFolder.IsChecked == true;
                foreach (var pfad in pfadListe)
                {
                    txtArbeitspfad.Text = pfad;
                    if (useMoveFolder) txtVerschiebepfad.Text = Path.Combine(pfad, "Move");
                    LogMessage($"\n=== Verarbeite Pfad: {pfad} ===");
                    await DateienVerarbeitenInPfadAsync(pfad, modus);
                }
                LogMessage("\n=== Alle Pfade verarbeitet ===");
                System.Windows.MessageBox.Show("Alle Pfade wurden verarbeitet!", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await DateienVerarbeitenInPfadAsync(txtArbeitspfad.Text, modus);
            }
        }

        private async Task DateienVerarbeitenInPfadAsync(string arbeitspfad, DateiOperationModus modus)
        {
            if (string.IsNullOrWhiteSpace(arbeitspfad) || !Directory.Exists(arbeitspfad))
            {
                LogMessage("FEHLER: Ungültiger Arbeitspfad!");
                return;
            }

            string verschiebepfad = chkUseMoveFolder.IsChecked == true
                ? Path.Combine(arbeitspfad, "Move")
                : txtVerschiebepfad.Text;
            if (string.IsNullOrWhiteSpace(verschiebepfad))
            {
                LogMessage("FEHLER: Ungültiger Verschiebepfad!");
                return;
            }

            isProcessing = true;
            UpdateButtonState();
            grpFortschritt.Visibility = Visibility.Visible;

            // Protokolle für neuen Lauf leeren
            suchProtokoll.Clear();
            kopierProtokoll.Clear();

            verschobeneDateienListe.Clear();
            nichtGefundeneDateienListe.Clear();

            letzterExportModus = modus switch
            {
                DateiOperationModus.Suchlauf => ExportListenModus.Suchlauf,
                DateiOperationModus.Kopieren => ExportListenModus.Kopieren,
                _ => ExportListenModus.Verschieben
            };

            await Dispatcher.InvokeAsync(() => GefundeneDateien.Clear());

            string modusText = modus switch
            {
                DateiOperationModus.Suchlauf => "Suchlauf",
                DateiOperationModus.Kopieren => "Kopieren",
                DateiOperationModus.Verschieben => "Verschieben",
                _ => "Verarbeiten"
            };

            LogMessage($"\n=== Start {modusText} ===");
            LogMessage($"Arbeitspfad: {arbeitspfad}");
            LogMessage($"Verschiebepfad: {verschiebepfad}");
            LogMessage($"Anzahl Dateien: {dateiListe.Count}");
            LogMessage($"Dateiendung ignorieren: {(chkIgnoreExtensionInList.IsChecked == true ? "Ja" : "Nein")}");

            bool ignoreExtension = chkIgnoreExtensionInList.IsChecked == true;
            bool nameBeginntMit = cmbTeilnameModus.SelectedIndex == 1;
            bool nameEndetMit = cmbTeilnameModus.SelectedIndex == 2;
            bool trennzeichenSuche = chkTrennzeichenSuche.IsChecked == true && (nameBeginntMit || nameEndetMit);
            bool trennzeichenAuto = chkTrennzeichenAuto.IsChecked == true;
            string trennzeichenManuell = txtTrennzeichen.Text;
            bool abschnittAuto = chkAbschnittAuto.IsChecked == true;
            int abschnittNummer = int.TryParse(txtAbschnittNummer.Text, out int abschnittEingabe) && abschnittEingabe > 0 ? abschnittEingabe : 1;
            bool einzelabschnitt = trennzeichenSuche && chkEinzelabschnittsuche.IsChecked == true;

            if (nameBeginntMit) LogMessage("Teilname-Suche: Dateiname BEGINNT mit Listeneintrag");
            if (nameEndetMit) LogMessage("Teilname-Suche: Dateiname ENDET mit Listeneintrag");

            bool duplikateFiltern = chkDuplikateFiltern.IsChecked == true;
            List<string> arbeitsListe = dateiListe;
            if (duplikateFiltern)
            {
                // Exakter Vergleich des Dateinamens - 'Datei (1).pdf' gilt bewusst NICHT als Duplikat.
                var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                arbeitsListe = dateiListe.Where(eintrag => gesehen.Add(eintrag.Trim())).ToList();
                int entfernt = dateiListe.Count - arbeitsListe.Count;
                LogMessage($"Duplikatfilter aktiv: {entfernt} doppelte Listeneinträge entfernt ({arbeitsListe.Count} verbleiben)");
            }
            if (trennzeichenSuche)
            {
                LogMessage($"Trennzeichen-Suche aktiv (Trennzeichen: {(trennzeichenAuto ? "automatisch" : trennzeichenManuell)}, Abschnitt: {(abschnittAuto ? "alle" : abschnittNummer.ToString())}, Einzelabschnitt: {(einzelabschnitt ? "Ja" : "Nein")})");
            }

            await Task.Run(async () =>
            {
                if (modus != DateiOperationModus.Suchlauf && !Directory.Exists(verschiebepfad))
                {
                    try
                    {
                        Directory.CreateDirectory(verschiebepfad);
                        await Dispatcher.InvokeAsync(() => LogMessage($"Verschiebepfad erstellt: {verschiebepfad}"));
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            LogMessage($"FEHLER: Kann Verschiebepfad nicht erstellen: {ex.Message}");
                            System.Windows.MessageBox.Show($"Fehler beim Erstellen des Verschiebepfads:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }
                }

                int gefunden = 0, nichtGefunden = 0, verschoben = 0;
                KonfliktAktion? fuerAlleAktion = null;
                var bereitsVerarbeitet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await Dispatcher.InvokeAsync(() => { progressBar.Maximum = arbeitsListe.Count; progressBar.Value = 0; });

                for (int i = 0; i < arbeitsListe.Count; i++)
                {
                    string dateiName = arbeitsListe[i].Trim();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        progressBar.Value = i + 1;
                        txtFortschritt.Text = $"{modusText} {i + 1} von {arbeitsListe.Count}: {dateiName}";
                    });

                    List<string> gefundeneDateien = new List<string>();
                    if (nameBeginntMit || nameEndetMit)
                    {
                        // Teilname-Suche: Name ohne Endung aus der Liste extrahieren
                        string suchName = ignoreExtension
                            ? Path.GetFileNameWithoutExtension(dateiName)
                            : dateiName;
                        var allFiles = Directory.GetFiles(arbeitspfad, "*.*", SearchOption.TopDirectoryOnly);
                        gefundeneDateien = allFiles.Where(f =>
                        {
                            string fileName = ignoreExtension
                                ? Path.GetFileNameWithoutExtension(f)
                                : Path.GetFileName(f);

                            if (trennzeichenSuche)
                            {
                                return TrennzeichenTreffer(fileName, suchName, trennzeichenAuto, trennzeichenManuell,
                                    abschnittAuto, abschnittNummer, einzelabschnitt, nameBeginntMit, nameEndetMit);
                            }

                            if (nameBeginntMit && fileName.StartsWith(suchName, StringComparison.OrdinalIgnoreCase))
                                return true;
                            if (nameEndetMit && fileName.EndsWith(suchName, StringComparison.OrdinalIgnoreCase))
                                return true;
                            return false;
                        }).ToList();
                    }
                    else if (ignoreExtension)
                    {
                        string nameOhneExtension = Path.GetFileNameWithoutExtension(dateiName);
                        var allFiles = Directory.GetFiles(arbeitspfad, "*.*", SearchOption.TopDirectoryOnly);
                        gefundeneDateien = allFiles
                            .Where(f => Path.GetFileNameWithoutExtension(f).Equals(nameOhneExtension, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    else
                    {
                        string suchPfad = Path.Combine(arbeitspfad, dateiName);
                        if (File.Exists(suchPfad)) gefundeneDateien.Add(suchPfad);
                    }

                    if (duplikateFiltern && gefundeneDateien.Count > 0)
                    {
                        int vorFilter = gefundeneDateien.Count;
                        gefundeneDateien = gefundeneDateien.Where(f => bereitsVerarbeitet.Add(f)).ToList();
                        if (gefundeneDateien.Count == 0)
                        {
                            // Alle Treffer wurden bereits ueber einen frueheren Listeneintrag verarbeitet.
                            // Das ist kein "nicht gefunden", der Eintrag wird schlicht uebersprungen.
                            await Dispatcher.InvokeAsync(() =>
                                LogMessage($"Übersprungen (Duplikat): {dateiName} - {vorFilter} Treffer bereits verarbeitet"));
                            continue;
                        }
                    }

                    if (gefundeneDateien.Count > 0)
                    {
                        gefunden += gefundeneDateien.Count;
                        foreach (var quelldatei in gefundeneDateien)
                        {
                            string fileName = Path.GetFileName(quelldatei);
                            string zielPfad = Path.Combine(verschiebepfad, fileName);
                            try
                            {
                                if (modus == DateiOperationModus.Suchlauf)
                                {
                                    verschobeneDateienListe.Add(fileName);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        LogMessage($"[Suchlauf] Gefunden: {fileName}");
                                        GefundeneDateien.Add(new DateiEintrag
                                        {
                                            Dateiname = fileName,
                                            Status = "? Gefunden",
                                            VollstaendigerPfad = quelldatei,
                                            AnzeigePfad = arbeitspfad
                                        });
                                    });
                                }
                                else if (File.Exists(zielPfad))
                                {
                                    // Konfliktbehandlung mit Hash-Vergleich
                                    string quellHash = KonfliktDialog.BerechneHash(quelldatei);
                                    string zielHash = KonfliktDialog.BerechneHash(zielPfad);

                                    KonfliktAktion aktion;
                                    if (fuerAlleAktion.HasValue)
                                    {
                                        aktion = fuerAlleAktion.Value;
                                    }
                                    else
                                    {
                                        var ergebnis = await Dispatcher.InvokeAsync(() =>
                                        {
                                            var dialog = new KonfliktDialog(quelldatei, zielPfad, quellHash, zielHash) { Owner = this };
                                            if (dialog.ShowDialog() == true)
                                                return dialog.Ergebnis;
                                            return new KonfliktErgebnis { Aktion = KonfliktAktion.Ueberspringen };
                                        });
                                        aktion = ergebnis.Aktion;
                                        if (ergebnis.FuerAlleAnwenden)
                                            fuerAlleAktion = aktion;
                                    }

                                    switch (aktion)
                                    {
                                        case KonfliktAktion.Ueberspringen:
                                            await Dispatcher.InvokeAsync(() =>
                                            {
                                                LogMessage($"Übersprungen (Konflikt): {fileName}");
                                                GefundeneDateien.Add(new DateiEintrag
                                                {
                                                    Dateiname = fileName,
                                                    Status = "? Übersprungen",
                                                    VollstaendigerPfad = zielPfad,
                                                    AnzeigePfad = verschiebepfad
                                                });
                                            });
                                            break;

                                        case KonfliktAktion.Ueberschreiben:
                                            if (modus == DateiOperationModus.Kopieren)
                                                File.Copy(quelldatei, zielPfad, overwrite: true);
                                            else
                                            {
                                                File.Delete(zielPfad);
                                                File.Move(quelldatei, zielPfad);
                                            }
                                            verschobeneDateienListe.Add(fileName);
                                            verschoben++;
                                            await Dispatcher.InvokeAsync(() =>
                                            {
                                                string aktionText = modus == DateiOperationModus.Kopieren ? "Kopiert" : "Verschoben";
                                                LogMessage($"{aktionText} (überschrieben): {fileName}");
                                                GefundeneDateien.Add(new DateiEintrag
                                                {
                                                    Dateiname = fileName,
                                                    Status = $"?? {aktionText} (überschrieben)",
                                                    VollstaendigerPfad = zielPfad,
                                                    AnzeigePfad = verschiebepfad
                                                });
                                            });
                                            break;

                                        case KonfliktAktion.QuelleUmbenennen:
                                        {
                                            string neuerZielPfad = KonfliktDialog.DateinameMitHash(zielPfad, quellHash);
                                            string neuerName = Path.GetFileName(neuerZielPfad);
                                            if (modus == DateiOperationModus.Kopieren)
                                                File.Copy(quelldatei, neuerZielPfad);
                                            else
                                                File.Move(quelldatei, neuerZielPfad);
                                            verschobeneDateienListe.Add(neuerName);
                                            verschoben++;
                                            await Dispatcher.InvokeAsync(() =>
                                            {
                                                string aktionText = modus == DateiOperationModus.Kopieren ? "Kopiert" : "Verschoben";
                                                LogMessage($"{aktionText} (umbenannt): {fileName} ? {neuerName}");
                                                GefundeneDateien.Add(new DateiEintrag
                                                {
                                                    Dateiname = neuerName,
                                                    Status = $"?? {aktionText} (Quelle umbenannt)",
                                                    VollstaendigerPfad = neuerZielPfad,
                                                    AnzeigePfad = verschiebepfad
                                                });
                                            });
                                            break;
                                        }

                                        case KonfliktAktion.ZielUmbenennen:
                                        {
                                            string umbenanntesZiel = KonfliktDialog.DateinameMitHash(zielPfad, zielHash);
                                            string umbenanntName = Path.GetFileName(umbenanntesZiel);
                                            File.Move(zielPfad, umbenanntesZiel);
                                            if (modus == DateiOperationModus.Kopieren)
                                                File.Copy(quelldatei, zielPfad);
                                            else
                                                File.Move(quelldatei, zielPfad);
                                            verschobeneDateienListe.Add(fileName);
                                            verschoben++;
                                            await Dispatcher.InvokeAsync(() =>
                                            {
                                                string aktionText = modus == DateiOperationModus.Kopieren ? "Kopiert" : "Verschoben";
                                                LogMessage($"{aktionText}: {fileName} (Ziel umbenannt ? {umbenanntName})");
                                                GefundeneDateien.Add(new DateiEintrag
                                                {
                                                    Dateiname = fileName,
                                                    Status = $"?? {aktionText} (Ziel umbenannt)",
                                                    VollstaendigerPfad = zielPfad,
                                                    AnzeigePfad = verschiebepfad
                                                });
                                            });
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    if (modus == DateiOperationModus.Kopieren)
                                    {
                                        File.Copy(quelldatei, zielPfad);
                                        verschobeneDateienListe.Add(fileName);
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            LogMessage($"Kopiert: {fileName}");
                                            GefundeneDateien.Add(new DateiEintrag
                                            {
                                                Dateiname = fileName,
                                                Status = $"? Kopiert",
                                                VollstaendigerPfad = zielPfad,
                                                AnzeigePfad = verschiebepfad
                                            });
                                        });
                                    }
                                    else
                                    {
                                        File.Move(quelldatei, zielPfad);
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            LogMessage($"Verschoben: {fileName}");
                                            GefundeneDateien.Add(new DateiEintrag
                                            {
                                                Dateiname = fileName,
                                                Status = $"? Verschoben",
                                                VollstaendigerPfad = zielPfad,
                                                AnzeigePfad = verschiebepfad
                                            });
                                        });
                                        verschobeneDateienListe.Add(fileName);
                                    }
                                    verschoben++;
                                }
                            }
                            catch (Exception ex)
                            {
                                await Dispatcher.InvokeAsync(() => LogMessage($"FEHLER bei {fileName}: {ex.Message}"));
                            }
                        }
                    }
                    else
                    {
                        nichtGefunden++;
                        nichtGefundeneDateienListe.Add(dateiName);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            LogMessage($"Nicht gefunden: {dateiName}");
                            GefundeneDateien.Add(new DateiEintrag
                            {
                                Dateiname = dateiName,
                                Status = "? Nicht gefunden",
                                VollstaendigerPfad = "",
                                AnzeigePfad = ""
                            });
                        });
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    LogMessage("\n=== Fertig ===");
                    LogMessage($"Gefunden: {gefunden} Dateien");
                    LogMessage($"Nicht gefunden: {nichtGefunden} Dateien");
                    if (modus != DateiOperationModus.Suchlauf)
                        LogMessage($"{modusText}: {verschoben} Dateien");
                    txtStatus.Text = $"{modusText} abgeschlossen: {gefunden} gefunden, {nichtGefunden} nicht gefunden";
                    if (modus != DateiOperationModus.Suchlauf)
                        System.Windows.MessageBox.Show($"{modusText} abgeschlossen!\n\nGefunden: {gefunden}\nNicht gefunden: {nichtGefunden}\n{modusText}: {verschoben}", "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });

            grpFortschritt.Visibility = Visibility.Collapsed;
            isProcessing = false;
            UpdateButtonState();
        }
        #endregion

        #region Menü
        private void MenuExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportDialog(verschobeneDateienListe.Count, nichtGefundeneDateienListe.Count, letzterExportModus) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var encoding = dialog.ExportAlsUtf8 ? Encoding.UTF8 : Encoding.GetEncoding(1252);

                if (dialog.ExportSuchprotokoll)
                {
                    ExportListe(suchProtokoll, "Suchprotokoll", "txt", encoding);
                    return;
                }
                if (dialog.ExportKopierprotokoll)
                {
                    ExportListe(kopierProtokoll, "Kopierprotokoll", "txt", encoding);
                    return;
                }
                if (dialog.ExportKomplettesProtokoll)
                {
                    ExportListe(txtLog.Text.Split('\n').ToList(), "Komplettes_Protokoll", "txt", encoding);
                    return;
                }

                List<string> quellListe;
                string bezeichnung;
                if (dialog.ExportAlle)
                {
                    // Treffer und Gegenstueck zusammen, Reihenfolge bleibt erhalten.
                    quellListe = verschobeneDateienListe.Concat(nichtGefundeneDateienListe).ToList();
                    bezeichnung = dialog.BezeichnungAlle;
                }
                else if (dialog.ExportVerschobene)
                {
                    quellListe = verschobeneDateienListe;
                    bezeichnung = dialog.BezeichnungTreffer;
                }
                else
                {
                    quellListe = nichtGefundeneDateienListe;
                    bezeichnung = dialog.BezeichnungGegenteil;
                }

                string beschreibung = bezeichnung.Replace(' ', '_');

                // Namen ggf. am Trennzeichen kuerzen; Duplikate bleiben erhalten.
                var liste = dialog.Kuerzen.Aktiv
                    ? quellListe.Select(dialog.Kuerzen.Anwenden).ToList()
                    : quellListe;

                string extension = dialog.ExportAlsCsv ? "csv" : "txt";
                if (dialog.ExportAlsCsv)
                    ExportListeAlsCsv(liste, beschreibung, encoding);
                else
                    ExportListe(liste, beschreibung, extension, encoding);
            }
        }

        private void MenuBeenden_Click(object sender, RoutedEventArgs e) => Close();
        private void MenuInfo_Click(object sender, RoutedEventArgs e)
        {
            var fenster = new InfoWindow { Owner = this };
            fenster.ShowDialog();
        }

        private void MenuHilfe_Click(object sender, RoutedEventArgs e) => HilfeAnzeigen(null);

        private void MenuTechnischeInfos_Click(object sender, RoutedEventArgs e)
            => HilfeAnzeigen("Technische Informationen");

        private void HilfeCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
            => HilfeAnzeigen(null);

        /// <summary>Öffnet das Hilfefenster einmalig und bringt es bei erneutem Aufruf nach vorn.</summary>
        internal void HilfeAnzeigen(string? thema)
        {
            if (_hilfeFenster == null || !_hilfeFenster.IsLoaded)
            {
                _hilfeFenster = new HilfeWindow { Owner = this };
                _hilfeFenster.Closed += (_, _) => _hilfeFenster = null;
                _hilfeFenster.Show();
            }
            else
            {
                _hilfeFenster.Activate();
            }

            if (!string.IsNullOrWhiteSpace(thema))
                _hilfeFenster.ThemaAnzeigen(thema);
        }

        private HilfeWindow? _hilfeFenster;

        private void btnOpenLogInExplorer_Click(object sender, RoutedEventArgs e)
        {
            // Minimal stub to satisfy XAML. Original implementation omitted.
        }
        private void btnDateiOeffnen_Click(object sender, RoutedEventArgs e)
        {
            // Minimal stub to satisfy XAML. Original implementation omitted.
        }
        #endregion

        private void ExportListe(List<string> liste, string beschreibung, string erweiterung, Encoding encoding)
        {
            if (liste == null || liste.Count == 0)
            {
                System.Windows.MessageBox.Show($"Keine Daten zum Exportieren vorhanden!\n\n{beschreibung.Replace("_", " ")} ist leer.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"{beschreibung.Replace("_", " ")} exportieren",
                Filter = $"Textdateien (*.{erweiterung})|*.{erweiterung}|Alle Dateien (*.*)|*.*",
                DefaultExt = erweiterung,
                FileName = $"{beschreibung}_{DateTime.Now:yyyyMMdd_HHmmss}.{erweiterung}"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllLines(dialog.FileName, liste, encoding);
                    string encodingName = encoding.EncodingName.Contains("1252") ? "ANSI" : "UTF-8";
                    LogMessage($"{beschreibung.Replace("_", " ")} exportiert: {dialog.FileName} ({encodingName}, {liste.Count} Einträge)");
                    System.Windows.MessageBox.Show($"{beschreibung.Replace("_", " ")} erfolgreich exportiert!\n\nDatei: {Path.GetFileName(dialog.FileName)}\nEncoding: {encodingName}\nEinträge: {liste.Count}", "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Exportieren:\n\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER beim Export: {ex.Message}");
                }
            }
        }

        private void ExportListeAlsCsv(List<string> liste, string beschreibung, Encoding encoding)
        {
            if (liste == null || liste.Count == 0)
            {
                System.Windows.MessageBox.Show($"Keine Daten zum Exportieren vorhanden!\n\n{beschreibung.Replace("_", " ")} ist leer.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"{beschreibung.Replace("_", " ")} als CSV exportieren",
                Filter = "CSV-Dateien (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"{beschreibung}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csvLines = new List<string> { "Dateiname;Zeitstempel" };
                    string zeitstempel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    foreach (var datei in liste)
                    {
                        string escapedDatei = datei;
                        if (datei.Contains(";") || datei.Contains("\""))
                            escapedDatei = $"\"{datei.Replace("\"", "\"\"")}\"";
                        csvLines.Add($"{escapedDatei};{zeitstempel}");
                    }
                    File.WriteAllLines(dialog.FileName, csvLines, encoding);
                    string encodingName = encoding.EncodingName.Contains("1252") ? "ANSI" : "UTF-8";
                    LogMessage($"{beschreibung.Replace("_", " ")} als CSV exportiert: {dialog.FileName} ({encodingName}, {liste.Count} Einträge)");
                    System.Windows.MessageBox.Show($"{beschreibung.Replace("_", " ")} erfolgreich als CSV exportiert!\n\nDatei: {Path.GetFileName(dialog.FileName)}\nEncoding: {encodingName}\nEinträge: {liste.Count}", "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Exportieren:\n\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER beim CSV-Export: {ex.Message}");
                }
            }
        }
    }
}