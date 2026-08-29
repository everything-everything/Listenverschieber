using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace Listenverschieber
{
    /// <summary>
    /// Modus für Dateioperationen
    /// </summary>
    public enum DateiOperationModus
    {
        Suchlauf,    // Nur anzeigen, nichts tun
        Kopieren,    // Dateien kopieren
        Verschieben  // Dateien verschieben
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Listenverschieber Variablen
        private List<string> dateiListe = new List<string>();
        private List<string[]> csvData = new List<string[]>();
        private string[] csvHeaders = Array.Empty<string>();
        private int selectedCsvColumnIndex = -1;
        private bool isProcessing = false;

        // Listen für Export
        private List<string> verschobeneDateienListe = new List<string>();
        private List<string> nichtGefundeneDateienListe = new List<string>();

        // Unvollständige Dateien Variablen
        private List<VerschobeneDateiInfo> verschobeneDateienInfo = new List<VerschobeneDateiInfo>();
        private DispatcherTimer rueckschiebeTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // Lade gespeicherte Konfiguration
            LadeKonfiguration();
            
            // Timer für Rückverschiebung
            rueckschiebeTimer = new DispatcherTimer();
            rueckschiebeTimer.Tick += RueckschiebeTimer_Tick;
        }

        #region Pfad-Konfiguration

        private void LadeKonfiguration()
        {
            var config = PfadKonfiguration.Laden();
            txtArbeitspfad.Text = config.Arbeitspfad;
            txtVerschiebepfad.Text = config.Verschiebepfad;
            chkUseMoveFolder.IsChecked = config.UseMoveFolder;
            
            // Lade Pfad-Listen
            lstArbeitspfade.ItemsSource = null;
            lstArbeitspfade.ItemsSource = config.ArbeitspfadListe;
            
            lstUeberwachungspfade.ItemsSource = null;
            lstUeberwachungspfade.ItemsSource = config.UeberwachungspfadListe;
        }

        private void btnPfadeSpeichern_Click(object sender, RoutedEventArgs e)
        {
            var config = new PfadKonfiguration
            {
                Arbeitspfad = txtArbeitspfad.Text,
                Verschiebepfad = txtVerschiebepfad.Text,
                UseMoveFolder = chkUseMoveFolder.IsChecked == true,
                ArbeitspfadListe = lstArbeitspfade.ItemsSource as List<string> ?? new List<string>(),
                UeberwachungspfadListe = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>()
            };
            
            config.Speichern();
            LogMessage("Pfad-Konfiguration gespeichert");
            System.Windows.MessageBox.Show("Pfad-Konfiguration wurde gespeichert!", "Gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnPfadeLaden_Click(object sender, RoutedEventArgs e)
        {
            LadeKonfiguration();
            LogMessage("Pfad-Konfiguration geladen");
        }

        #endregion

        #region Mehrere Pfade - Listenverschieber

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
                System.Windows.MessageBox.Show("Dieser Pfad ist bereits in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
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

        #region Mehrere Pfade - Unvollständige Dateien

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

        #region Listenverschieber

        private void btnArbeitspfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Arbeitspfad auswählen",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtArbeitspfad.Text = dialog.SelectedPath;
                UpdateVerschiebepfadIfMoveFolder();
                LogMessage($"Arbeitspfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnVerschiebepfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Verschiebepfad auswählen",
                ShowNewFolderButton = true
            };

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
                    {
                        cmbCsvSpalte.Items.Add($"{csvHeaders[i]} (Spalte {i + 1})");
                    }
                    
                    int defaultIndex = Array.FindIndex(csvHeaders, h => 
                        h.Equals("Beleg_Dateiname", StringComparison.OrdinalIgnoreCase));
                    
                    if (defaultIndex >= 0)
                    {
                        cmbCsvSpalte.SelectedIndex = defaultIndex;
                        LogMessage($"Standard-Spalte 'Beleg_Dateiname' ausgewählt");
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
            bool canProcess = !isProcessing && 
                              dateiListe.Count > 0 && 
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
        }

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

            // Erste Zeile als Header
            csvHeaders = lines[0].Split(';');
            
            // Rest als Daten
            csvData = lines.Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(';'))
                .ToList();
        }

        #endregion

        #region Unvollständige Dateien Modus

        private void btnUeberwachungspfadDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Überwachungspfad auswählen",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtUeberwachungspfad.Text = dialog.SelectedPath;
                UpdateVerschiebepfadTab2IfMoveFolder();
                LogMessage2($"Überwachungspfad gesetzt: {dialog.SelectedPath}");
            }
        }

        private void btnVerschiebepfadDurchsuchenTab2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Verschiebepfad auswählen",
                ShowNewFolderButton = true
            };

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
        {
            await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Suchlauf);
        }

        private async void btnUnvollstaendigeKopieren_Click(object sender, RoutedEventArgs e)
        {
            await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Kopieren);
        }

        private async void btnUnvollstaendigeVerschieben_Click(object sender, RoutedEventArgs e)
        {
            await UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus.Verschieben);
        }

        private async Task UnvollstaendigeDateienVerarbeitenAsync(DateiOperationModus modus)
        {
            // Prüfe ob "Alle Pfade verarbeiten" aktiviert ist
            bool processAllPaths = chkProcessAllWatchPaths?.IsChecked == true;
            
            if (processAllPaths)
            {
                var pfadListe = lstUeberwachungspfade.ItemsSource as List<string> ?? new List<string>();
                if (pfadListe.Count == 0)
                {
                    System.Windows.MessageBox.Show("Keine Pfade in der Liste!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var pfad in pfadListe)
                {
                    txtUeberwachungspfad.Text = pfad;
                    UpdateVerschiebepfadTab2IfMoveFolder();
                    LogMessage2($"\n=== Verarbeite Pfad: {pfad} ===");
                    await UnvollstaendigeDateienVerarbeitenInPfadAsync(pfad, modus);
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

                await UnvollstaendigeDateienVerarbeitenInPfadAsync(txtUeberwachungspfad.Text, modus);
            }
        }

        private async Task UnvollstaendigeDateienVerarbeitenInPfadAsync(string ueberwachungspfad, DateiOperationModus modus)
        {
            // UI vorbereiten
            grpFortschritt2.Visibility = Visibility.Visible;
            btnUnvollstaendigeSuchlauf.IsEnabled = false;
            btnUnvollstaendigeKopieren.IsEnabled = false;
            btnUnvollstaendigeVerschieben.IsEnabled = false;

            if (modus != DateiOperationModus.Suchlauf)
            {
                verschobeneDateienInfo.Clear();
            }

            await Task.Run(() => UnvollstaendigeDateienVerarbeitenCoreAsync(ueberwachungspfad, modus));

            // UI zurücksetzen
            grpFortschritt2.Visibility = Visibility.Collapsed;
            btnUnvollstaendigeSuchlauf.IsEnabled = true;
            btnUnvollstaendigeKopieren.IsEnabled = true;
            btnUnvollstaendigeVerschieben.IsEnabled = true;
            btnDateienZurueckschieben.IsEnabled = verschobeneDateienInfo.Count > 0;

            // Timer starten wenn gewünscht (nur bei Verschieben)
            if (modus == DateiOperationModus.Verschieben && chkAutoRueckverschiebung.IsChecked == true && verschobeneDateienInfo.Count > 0)
            {
                if (int.TryParse(txtRueckschiebeZeit.Text, out int sekunden) && sekunden > 0)
                {
                    rueckschiebeTimer.Interval = TimeSpan.FromSeconds(sekunden);
                    rueckschiebeTimer.Start();
                    LogMessage2($"Rückverschiebe-Timer gestartet: {sekunden} Sekunden");
                }
            }
        }

        private async Task UnvollstaendigeDateienVerarbeitenCoreAsync(string ueberwachungspfad, DateiOperationModus modus)
        {
            string verschiebepfad = "";
            string hauptformat = "";
            string pflicht1 = "";
            string pflicht2 = "";
            string modusText = modus switch
            {
                DateiOperationModus.Suchlauf => "Suchlauf",
                DateiOperationModus.Kopieren => "Kopieren",
                DateiOperationModus.Verschieben => "Verschieben",
                _ => "Verarbeiten"
            };

            await Dispatcher.InvokeAsync(() =>
            {
                // Bestimme Verschiebepfad
                if (chkUseMoveFolderTab2.IsChecked == true)
                {
                    verschiebepfad = Path.Combine(ueberwachungspfad, "Move");
                }
                else
                {
                    verschiebepfad = txtVerschiebepfadTab2.Text;
                }
                
                hauptformat = txtHauptformat.Text.Trim().ToLower();
                pflicht1 = txtPflichtdatei1.Text.Trim().ToLower();
                pflicht2 = txtPflichtdatei2.Text.Trim().ToLower();
                
                LogMessage2($"\n=== Start {modusText}: Unvollständige Dateien ===");
                LogMessage2($"Überwachungspfad: {ueberwachungspfad}");
                LogMessage2($"Verschiebepfad: {verschiebepfad}");
                LogMessage2($"Hauptformat: {(hauptformat == "*" ? "Alle" : hauptformat)}");
                LogMessage2($"Pflichtdatei 1 (erforderlich): .{pflicht1}");
                if (!string.IsNullOrWhiteSpace(pflicht2))
                {
                    LogMessage2($"Pflichtdatei 2 (optional): .{pflicht2}");
                }
                else
                {
                    LogMessage2("Pflichtdatei 2: Nicht verwendet");
                }
            });

            // Erstelle Verschiebe-Ordner wenn nicht Suchlauf
            if (modus != DateiOperationModus.Suchlauf && !Directory.Exists(verschiebepfad))
            {
                try
                {
                    Directory.CreateDirectory(verschiebepfad);
                    await Dispatcher.InvokeAsync(() => LogMessage2($"Verschiebepfad erstellt: {verschiebepfad}"));
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

            // Finde alle Dateien
            var alleDateien = Directory.GetFiles(ueberwachungspfad, "*.*", SearchOption.TopDirectoryOnly);
            
            // Gruppiere nach Basisname
            var gruppen = alleDateien
                .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            await Dispatcher.InvokeAsync(() =>
            {
                progressBar2.Maximum = gruppen.Count;
                progressBar2.Value = 0;
            });

            int verarbeiteteCount = 0;
            int processed = 0;

            foreach (var gruppe in gruppen)
            {
                processed++;
                await Dispatcher.InvokeAsync(() =>
                {
                    progressBar2.Value = processed;
                    txtFortschritt2.Text = $"Prüfe {processed} von {gruppen.Count}: {gruppe.Key}";
                });

                var dateien = gruppe.ToList();
                
                // Filtere nach Hauptformat
                var hauptdateien = hauptformat == "*" 
                    ? dateien 
                    : dateien.Where(f => Path.GetExtension(f).TrimStart('.').Equals(hauptformat, StringComparison.OrdinalIgnoreCase)).ToList();

                if (hauptdateien.Count == 0)
                    continue;

                // Prüfe ob Pflichtdateien vorhanden
                bool hatPflicht1 = dateien.Any(f => Path.GetExtension(f).TrimStart('.').Equals(pflicht1, StringComparison.OrdinalIgnoreCase));
                
                // Pflichtdatei 2 ist optional - nur prüfen wenn angegeben
                bool hatPflicht2 = string.IsNullOrWhiteSpace(pflicht2) || 
                                   dateien.Any(f => Path.GetExtension(f).TrimStart('.').Equals(pflicht2, StringComparison.OrdinalIgnoreCase));

                // Wenn Pflichtdatei 1 fehlt ODER (wenn Pflicht2 angegeben wurde UND diese fehlt): verschiebe
                bool sollVerschieben = !hatPflicht1 || (!string.IsNullOrWhiteSpace(pflicht2) && !hatPflicht2);

                if (sollVerschieben)
                {
                    var info = new VerschobeneDateiInfo
                    {
                        VerschobenAm = DateTime.Now
                    };

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
                                await Dispatcher.InvokeAsync(() => LogMessage2($"[Suchlauf] Würde {modusText.ToLower()}: {fileName} (fehlt: {fehlendeDateien})"));
                                verarbeiteteCount++;
                            }
                            else if (File.Exists(ziel))
                            {
                                await Dispatcher.InvokeAsync(() => LogMessage2($"Übersprungen (existiert): {fileName}"));
                            }
                            else
                            {
                                if (modus == DateiOperationModus.Kopieren)
                                {
                                    File.Copy(datei, ziel);
                                    await Dispatcher.InvokeAsync(() => LogMessage2($"Kopiert: {fileName} (fehlt: {fehlendeDateien})"));
                                }
                                else // Verschieben
                                {
                                    File.Move(datei, ziel);
                                    info.ZugehoerigeDateien.Add(fileName);
                                    
                                    if (string.IsNullOrEmpty(info.Quelldatei))
                                    {
                                        info.Quelldatei = datei;
                                        info.Zieldatei = ziel;
                                    }
                                    await Dispatcher.InvokeAsync(() => LogMessage2($"Verschoben: {fileName} (fehlt: {fehlendeDateien})"));
                                }
                                verarbeiteteCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            await Dispatcher.InvokeAsync(() => LogMessage2($"FEHLER: {Path.GetFileName(datei)} - {ex.Message}"));
                        }
                    }

                    if (modus == DateiOperationModus.Verschieben && info.ZugehoerigeDateien.Count > 0)
                    {
                        verschobeneDateienInfo.Add(info);
                    }
                }
            }

            await Dispatcher.InvokeAsync(() =>
            {
                LogMessage2($"\n=== Fertig ===");
                LogMessage2($"{modusText}: {verarbeiteteCount} Dateien");
                txtStatus.Text = $"{modusText}: {verarbeiteteCount} Dateien";
                
                if (modus != DateiOperationModus.Suchlauf)
                {
                    System.Windows.MessageBox.Show($"{modusText} abgeschlossen!\n\n{modusText}e Dateien: {verarbeiteteCount}", 
                        "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private async void btnDateienZurueckschieben_Click(object sender, RoutedEventArgs e)
        {
            if (verschobeneDateienInfo.Count == 0)
            {
                System.Windows.MessageBox.Show("Keine Dateien zum Zurückschieben vorhanden!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            rueckschiebeTimer.Stop();
            await DateienZurueckschiebenAsync();
        }

        private async Task DateienZurueckschiebenAsync()
        {
            await Dispatcher.InvokeAsync(() => LogMessage2("\n=== Start Rückverschiebung ==="));

            int zurueckCount = 0;
            string ueberwachungspfad = txtUeberwachungspfad.Text;
            
            foreach (var info in verschobeneDateienInfo.ToList())
            {
                foreach (var dateiName in info.ZugehoerigeDateien)
                {
                    try
                    {
                        string quelle = Path.Combine(Path.GetDirectoryName(info.Zieldatei)!, dateiName);
                        string ziel = Path.Combine(ueberwachungspfad, dateiName);

                        if (!File.Exists(quelle))
                        {
                            await Dispatcher.InvokeAsync(() => LogMessage2($"Übersprungen (Quelle fehlt): {dateiName}"));
                            continue;
                        }

                        if (File.Exists(ziel))
                        {
                            await Dispatcher.InvokeAsync(() => LogMessage2($"Übersprungen (existiert): {dateiName}"));
                            continue;
                        }

                        File.Move(quelle, ziel);
                        zurueckCount++;

                        await Dispatcher.InvokeAsync(() => LogMessage2($"Zurückgeschoben: {dateiName}"));
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() => LogMessage2($"FEHLER beim Zurückschieben von {dateiName} - {ex.Message}"));
                    }
                }
            }

            verschobeneDateienInfo.Clear();
            btnDateienZurueckschieben.IsEnabled = false;

            await Dispatcher.InvokeAsync(() =>
            {
                LogMessage2($"\n=== Rückverschiebung Fertig ===");
                LogMessage2($"Zurückgeschoben: {zurueckCount} Dateien");
                txtStatus.Text = $"Zurückgeschoben: {zurueckCount} Dateien";
                
                System.Windows.MessageBox.Show($"Rückverschiebung abgeschlossen!\n\nZurückgeschobene Dateien: {zurueckCount}", 
                    "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void RueckschiebeTimer_Tick(object? sender, EventArgs e)
        {
            rueckschiebeTimer.Stop();
            _ = DateienZurueckschiebenAsync();
        }

        private void LogMessage2(string message)
        {
            txtLog2.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
            txtLog2.ScrollToEnd();
        }

        #endregion

        #region Listenverschieber Dateioperationen

        private async void btnDateienSuchlauf_Click(object sender, RoutedEventArgs e)
        {
            await DateienVerarbeitenAsync(DateiOperationModus.Suchlauf);
        }

        private async void btnDateienKopieren_Click(object sender, RoutedEventArgs e)
        {
            await DateienVerarbeitenAsync(DateiOperationModus.Kopieren);
        }

        private async void btnDateienVerschieben_Click(object sender, RoutedEventArgs e)
        {
            await DateienVerarbeitenAsync(DateiOperationModus.Verschieben);
        }

        private async Task DateienVerarbeitenAsync(DateiOperationModus modus)
        {
            // Prüfe ob "Alle Pfade verarbeiten" aktiviert ist
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

                // Backup für Validierung
                string originalVerschiebepfad = txtVerschiebepfad.Text;
                bool useMoveFolder = chkUseMoveFolder.IsChecked == true;

                foreach (var pfad in pfadListe)
                {
                    txtArbeitspfad.Text = pfad;
                    
                    if (useMoveFolder)
                    {
                        txtVerschiebepfad.Text = Path.Combine(pfad, "Move");
                    }
                    
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

            // Listen für Export zurücksetzen
            verschobeneDateienListe.Clear();
            nichtGefundeneDateienListe.Clear();

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

            await Task.Run(async () =>
            {
                // Erstelle Verschiebe-Ordner wenn nicht Suchlauf
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

                int gefunden = 0;
                int nichtGefunden = 0;
                int verschoben = 0;

                await Dispatcher.InvokeAsync(() =>
                {
                    progressBar.Maximum = dateiListe.Count;
                    progressBar.Value = 0;
                });

                for (int i = 0; i < dateiListe.Count; i++)
                {
                    string dateiName = dateiListe[i].Trim();
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        progressBar.Value = i + 1;
                        txtFortschritt.Text = $"{modusText} {i + 1} von {dateiListe.Count}: {dateiName}";
                    });

                    List<string> gefundeneDateien = new List<string>();

                    if (ignoreExtension)
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
                        if (File.Exists(suchPfad))
                        {
                            gefundeneDateien.Add(suchPfad);
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
                                    await Dispatcher.InvokeAsync(() => LogMessage($"[Suchlauf] Gefunden: {fileName}"));
                                }
                                else if (File.Exists(zielPfad))
                                {
                                    await Dispatcher.InvokeAsync(() => LogMessage($"Übersprungen (existiert): {fileName}"));
                                }
                                else
                                {
                                    if (modus == DateiOperationModus.Kopieren)
                                    {
                                        File.Copy(quelldatei, zielPfad);
                                        await Dispatcher.InvokeAsync(() => LogMessage($"Kopiert: {fileName}"));
                                    }
                                    else // Verschieben
                                    {
                                        File.Move(quelldatei, zielPfad);
                                        await Dispatcher.InvokeAsync(() => LogMessage($"Verschoben: {fileName}"));
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
                        await Dispatcher.InvokeAsync(() => LogMessage($"Nicht gefunden: {dateiName}"));
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    LogMessage($"\n=== Fertig ===");
                    LogMessage($"Gefunden: {gefunden} Dateien");
                    LogMessage($"Nicht gefunden: {nichtGefunden} Dateien");
                    
                    if (modus != DateiOperationModus.Suchlauf)
                    {
                        LogMessage($"{modusText}: {verschoben} Dateien");
                    }

                    txtStatus.Text = $"{modusText} abgeschlossen: {gefunden} gefunden, {nichtGefunden} nicht gefunden";

                    if (modus != DateiOperationModus.Suchlauf)
                    {
                        System.Windows.MessageBox.Show(
                            $"{modusText} abgeschlossen!\n\n" +
                            $"Gefunden: {gefunden}\n" +
                            $"Nicht gefunden: {nichtGefunden}\n" +
                            $"{modusText}: {verschoben}",
                            "Fertig", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            });

            grpFortschritt.Visibility = Visibility.Collapsed;
            isProcessing = false;
            UpdateButtonState();
        }

        #endregion

        #region Menu Handlers

        private void MenuExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportDialog(verschobeneDateienListe.Count, nichtGefundeneDateienListe.Count);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var liste = dialog.ExportVerschobene ? verschobeneDateienListe : nichtGefundeneDateienListe;
                string beschreibung = dialog.ExportVerschobene ? "Verschobene_Dateien" : "Nicht_gefundene_Dateien";
                string extension = dialog.ExportAlsCsv ? "csv" : "txt";
                var encoding = dialog.ExportAlsUtf8 ? Encoding.UTF8 : Encoding.GetEncoding(1252);

                if (dialog.ExportAlsCsv)
                {
                    ExportListeAlsCsv(liste, beschreibung, encoding);
                }
                else
                {
                    ExportListe(liste, beschreibung, extension, encoding);
                }
            }
        }

        private void ExportListe(List<string> liste, string beschreibung, string erweiterung, Encoding encoding)
        {
            if (liste == null || liste.Count == 0)
            {
                System.Windows.MessageBox.Show($"Keine Daten zum Exportieren vorhanden!\n\n{beschreibung.Replace("_", " ")} ist leer.", 
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    
                    System.Windows.MessageBox.Show(
                        $"{beschreibung.Replace("_", " ")} erfolgreich exportiert!\n\n" +
                        $"Datei: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Encoding: {encodingName}\n" +
                        $"Einträge: {liste.Count}", 
                        "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Exportieren:\n\n{ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER beim Export: {ex.Message}");
                }
            }
        }

        private void ExportListeAlsCsv(List<string> liste, string beschreibung, Encoding encoding)
        {
            if (liste == null || liste.Count == 0)
            {
                System.Windows.MessageBox.Show($"Keine Daten zum Exportieren vorhanden!\n\n{beschreibung.Replace("_", " ")} ist leer.", 
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    var csvLines = new List<string>();
                    
                    // Header
                    csvLines.Add("Dateiname;Zeitstempel");
                    
                    // Daten
                    string zeitstempel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    foreach (var datei in liste)
                    {
                        // CSV-Escaping
                        string escapedDatei = datei;
                        if (datei.Contains(";") || datei.Contains("\""))
                        {
                            escapedDatei = $"\"{datei.Replace("\"", "\"\"")}\"";
                        }
                        csvLines.Add($"{escapedDatei};{zeitstempel}");
                    }
                    
                    File.WriteAllLines(dialog.FileName, csvLines, encoding);
                    
                    string encodingName = encoding.EncodingName.Contains("1252") ? "ANSI" : "UTF-8";
                    LogMessage($"{beschreibung.Replace("_", " ")} als CSV exportiert: {dialog.FileName} ({encodingName}, {liste.Count} Einträge)");
                    
                    System.Windows.MessageBox.Show(
                        $"{beschreibung.Replace("_", " ")} erfolgreich als CSV exportiert!\n\n" +
                        $"Datei: {Path.GetFileName(dialog.FileName)}\n" +
                        $"Encoding: {encodingName}\n" +
                        $"Einträge: {liste.Count}", 
                        "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Fehler beim Exportieren:\n\n{ex.Message}", 
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"FEHLER beim CSV-Export: {ex.Message}");
                }
            }
        }

        private void MenuBeenden_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuInfo_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Listenverschieber Pro\n\n" +
                "Version 2.0\n\n" +
                "Eine Anwendung zum Verschieben von Dateien basierend auf Listen\n" +
                "und zum Überwachen unvollständiger Dateigruppen.\n\n" +
                "© 2024",
                "Info",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }
}