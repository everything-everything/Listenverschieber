using System.Text;
using System.Windows;

namespace Listenverschieber
{
    public partial class ExportDialog : Window
    {
        public bool ExportVerschobene { get; private set; }
        public bool ExportNichtGefunden { get; private set; }
        public bool ExportSuchprotokoll { get; private set; }
        public bool ExportKopierprotokoll { get; private set; }
        public bool ExportKomplettesProtokoll { get; private set; }
        public bool ExportAlsCsv { get; private set; }
        public bool ExportAlsUtf8 { get; private set; }

        public ExportDialog(int anzahlVerschoben, int anzahlNichtGefunden)
        {
            InitializeComponent();
            
            // Setze die Anzahl der Einträge
            if (anzahlVerschoben > 0)
            {
                rbVerschoben.Content = $"? Verschobene Dateien ({anzahlVerschoben} Einträge)";
                rbVerschoben.IsEnabled = true;
            }
            else
            {
                rbVerschoben.Content = "? Verschobene Dateien (keine Einträge)";
                rbVerschoben.IsEnabled = false;
            }

            if (anzahlNichtGefunden > 0)
            {
                rbNichtGefunden.Content = "? Nicht gefundene Dateien (" + anzahlNichtGefunden + " Einträge)";
                rbNichtGefunden.IsEnabled = true;
            }
            else
            {
                rbNichtGefunden.Content = "? Nicht gefundene Dateien (keine Einträge)";
                rbNichtGefunden.IsEnabled = false;
            }

            // Wenn verschobene Dateien deaktiviert sind, wähle nicht gefundene
            if (!rbVerschoben.IsEnabled && rbNichtGefunden.IsEnabled)
            {
                rbNichtGefunden.IsChecked = true;
            }
            else if (rbVerschoben.IsEnabled)
            {
                rbVerschoben.IsChecked = true;
            }

            // Initialisiere Info
            UpdateInfo();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if (txtInfo == null || btnExport == null) return; // Während Initialisierung

            // Bestimme Quelle
            string quelle = rbVerschoben?.IsChecked == true ? "? Verschobene Dateien"
                          : rbNichtGefunden?.IsChecked == true ? "? Nicht gefundene Dateien"
                          : rbSuchprotokoll?.IsChecked == true ? "Suchprotokoll"
                          : rbKopierprotokoll?.IsChecked == true ? "Kopierprotokoll"
                          : "Komplettes Protokoll";
            
            // Protokolle -> CSV deaktivieren
            bool istProtokoll = rbSuchprotokoll?.IsChecked == true || rbKopierprotokoll?.IsChecked == true || rbKomplettesProtokoll?.IsChecked == true;
            rbCsv.IsEnabled = !istProtokoll;
            if (istProtokoll)
            {
                rbTxt.IsChecked = true;
                txtFormatHinweis.Visibility = Visibility.Visible;
            }
            else
            {
                txtFormatHinweis.Visibility = Visibility.Collapsed;
            }

            // Bestimme Format  
            string format = rbTxt?.IsChecked == true ? "TXT" : "CSV";
            
            // Bestimme Encoding
            string encoding = rbAnsi?.IsChecked == true ? "ANSI" : "UTF-8";

            txtInfo.Text = $"Export: {quelle}";
            txtDateiname.Text = $"Format: {format} | Kodierung: {encoding}";
            
            // Button ist enabled, wenn irgendeine Datenquelle sinnvoll ist
            btnExport.IsEnabled = (rbVerschoben != null && rbVerschoben.IsEnabled) || 
                                  (rbNichtGefunden != null && rbNichtGefunden.IsEnabled) ||
                                  (rbSuchprotokoll != null) || (rbKopierprotokoll != null) || (rbKomplettesProtokoll != null);
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            // Speichere die Auswahl
            ExportVerschobene = rbVerschoben?.IsChecked == true;
            ExportNichtGefunden = rbNichtGefunden?.IsChecked == true;
            ExportSuchprotokoll = rbSuchprotokoll?.IsChecked == true;
            ExportKopierprotokoll = rbKopierprotokoll?.IsChecked == true;
            ExportKomplettesProtokoll = rbKomplettesProtokoll?.IsChecked == true;
            ExportAlsCsv = rbCsv?.IsChecked == true; // wird ggf. deaktiviert
            ExportAlsUtf8 = rbUtf8?.IsChecked == true;
            
            // Prüfe, ob bei Listen eine Liste verfügbar ist
            bool listeVerfügbar = (ExportVerschobene && rbVerschoben.IsEnabled) || (ExportNichtGefunden && rbNichtGefunden.IsEnabled);
            bool istProtokoll = ExportSuchprotokoll || ExportKopierprotokoll || ExportKomplettesProtokoll;
            if (!listeVerfügbar && !istProtokoll)
            {
                System.Windows.MessageBox.Show(
                    "Die gewählte Liste enthält keine Einträge zum Exportieren.\n\nBitte wählen Sie eine Liste mit Daten oder brechen Sie den Export ab.",
                    "Keine Daten",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return; // Dialog bleibt offen
            }
            
            // Setze DialogResult auf true, damit MainWindow weiß, dass Export gewünscht ist
            DialogResult = true;
            Close();
        }

        private void btnAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
