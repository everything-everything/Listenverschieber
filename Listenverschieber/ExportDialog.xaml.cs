using System.Windows;

namespace Listenverschieber
{
    /// <summary>
    /// Beschreibt, welche Aktion zuletzt ausgefuehrt wurde. Danach richten sich
    /// die Beschriftungen der beiden Listen im Exportdialog.
    /// </summary>
    public enum ExportListenModus
    {
        Suchlauf,
        Kopieren,
        Verschieben
    }

    /// <summary>
    /// Regeln zum Kuerzen der Dateinamen beim Export.
    /// </summary>
    public sealed class KuerzenEinstellungen
    {
        public bool Aktiv { get; init; }
        public string Trennzeichen { get; init; } = "_";
        public int Abschnitte { get; init; } = 1;
        /// <summary>True = angegebene Abschnitte entfernen, False = angegebene Abschnitte behalten.</summary>
        public bool Entfernen { get; init; }
        public bool Rueckwaerts { get; init; }
        public bool EndungEntfernen { get; init; } = true;

        /// <summary>Wendet die Kuerzung auf einen einzelnen Namen an.</summary>
        public string Anwenden(string name)
        {
            if (!Aktiv || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(Trennzeichen))
                return name;

            string wert = name;
            string endung = string.Empty;

            if (EndungEntfernen)
            {
                int punkt = wert.LastIndexOf('.');
                if (punkt > 0)
                {
                    endung = wert[punkt..];
                    wert = wert[..punkt];
                }
            }

            var teile = wert.Split([Trennzeichen], StringSplitOptions.None);
            if (teile.Length <= 1)
                return name;

            int anzahl = Math.Max(1, Math.Min(Abschnitte, teile.Length));
            if (Entfernen)
            {
                // Beim Entfernen muss mindestens ein Abschnitt uebrig bleiben.
                anzahl = Math.Min(anzahl, teile.Length - 1);
                if (anzahl <= 0)
                    return name;
                anzahl = teile.Length - anzahl;
            }

            var behalten = Rueckwaerts == Entfernen
                ? teile[..anzahl]
                : teile[^anzahl..];

            string ergebnis = string.Join(Trennzeichen, behalten);
            return EndungEntfernen ? ergebnis : ergebnis + endung;
        }
    }

    public partial class ExportDialog : Window
    {
        public bool ExportVerschobene { get; private set; }
        public bool ExportNichtGefunden { get; private set; }
        public bool ExportAlle { get; private set; }
        public bool ExportSuchprotokoll { get; private set; }
        public bool ExportKopierprotokoll { get; private set; }
        public bool ExportKomplettesProtokoll { get; private set; }
        public bool ExportAlsCsv { get; private set; }
        public bool ExportAlsUtf8 { get; private set; }

        /// <summary>Die im Dialog gewaehlten Kuerzungsregeln.</summary>
        public KuerzenEinstellungen Kuerzen { get; private set; } = new();

        /// <summary>Beschriftung der Trefferliste, passend zur zuletzt ausgefuehrten Aktion.</summary>
        public string BezeichnungTreffer { get; }

        /// <summary>Beschriftung der Gegenliste, passend zur zuletzt ausgefuehrten Aktion.</summary>
        public string BezeichnungGegenteil { get; }

        /// <summary>Beschriftung der Gesamtliste (Treffer und Gegenteil zusammen).</summary>
        public string BezeichnungAlle { get; } = "Alle Dateien";

        private readonly bool _initialisiert;

        public ExportDialog(int anzahlVerschoben, int anzahlNichtGefunden)
            : this(anzahlVerschoben, anzahlNichtGefunden, ExportListenModus.Verschieben)
        {
        }

        public ExportDialog(int anzahlTreffer, int anzahlGegenteil, ExportListenModus modus)
        {
            InitializeComponent();

            (BezeichnungTreffer, BezeichnungGegenteil) = modus switch
            {
                ExportListenModus.Suchlauf => ("Gefundene Dateien", "Nicht gefundene Dateien"),
                ExportListenModus.Kopieren => ("Kopierte Dateien", "Nicht kopierte Dateien"),
                _ => ("Verschobene Dateien", "Nicht verschobene Dateien")
            };

            ListeVorbereiten(rbVerschoben, BezeichnungTreffer, anzahlTreffer);
            ListeVorbereiten(rbNichtGefunden, BezeichnungGegenteil, anzahlGegenteil);
            ListeVorbereiten(rbAlle, BezeichnungAlle, anzahlTreffer + anzahlGegenteil);

            if (rbVerschoben.IsEnabled)
                rbVerschoben.IsChecked = true;
            else if (rbNichtGefunden.IsEnabled)
                rbNichtGefunden.IsChecked = true;
            else if (rbAlle.IsEnabled)
                rbAlle.IsChecked = true;

            _initialisiert = true;
            UpdateInfo();
        }

        private static void ListeVorbereiten(System.Windows.Controls.RadioButton rb, string bezeichnung, int anzahl)
        {
            rb.Content = anzahl > 0
                ? $"{bezeichnung} ({anzahl} Einträge)"
                : $"{bezeichnung} (keine Einträge)";
            rb.IsEnabled = anzahl > 0;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e) => UpdateInfo();

        private void Kuerzen_Changed(object sender, RoutedEventArgs e) => UpdateInfo();

        private void Kuerzen_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateInfo();

        /// <summary>Liest die Kuerzungsfelder aus und liefert die aktuellen Regeln.</summary>
        private KuerzenEinstellungen KuerzenLesen()
        {
            int abschnitte = 1;
            if (txtAbschnitte != null && int.TryParse(txtAbschnitte.Text, out int gelesen) && gelesen > 0)
                abschnitte = gelesen;

            return new KuerzenEinstellungen
            {
                Aktiv = chkKuerzen?.IsChecked == true,
                Trennzeichen = string.IsNullOrEmpty(txtTrennzeichen?.Text) ? "_" : txtTrennzeichen.Text,
                Abschnitte = abschnitte,
                Entfernen = rbEntfernen?.IsChecked == true,
                Rueckwaerts = rbRueckwaerts?.IsChecked == true,
                EndungEntfernen = chkEndungEntfernen?.IsChecked == true
            };
        }

        private void UpdateInfo()
        {
            if (!_initialisiert || txtInfo == null || btnExport == null) return;

            string quelle = rbVerschoben?.IsChecked == true ? BezeichnungTreffer
                          : rbNichtGefunden?.IsChecked == true ? BezeichnungGegenteil
                          : rbAlle?.IsChecked == true ? BezeichnungAlle
                          : rbSuchprotokoll?.IsChecked == true ? "Suchprotokoll"
                          : rbKopierprotokoll?.IsChecked == true ? "Kopierprotokoll"
                          : "Komplettes Protokoll";

            // Protokolle -> CSV und Kuerzen deaktivieren
            bool istProtokoll = rbSuchprotokoll?.IsChecked == true
                             || rbKopierprotokoll?.IsChecked == true
                             || rbKomplettesProtokoll?.IsChecked == true;

            rbCsv.IsEnabled = !istProtokoll;
            chkKuerzen.IsEnabled = !istProtokoll;
            if (istProtokoll)
            {
                rbTxt.IsChecked = true;
                chkKuerzen.IsChecked = false;
                txtFormatHinweis.Visibility = Visibility.Visible;
            }
            else
            {
                txtFormatHinweis.Visibility = Visibility.Collapsed;
            }

            var kuerzen = KuerzenLesen();
            grdKuerzen.IsEnabled = kuerzen.Aktiv && !istProtokoll;

            if (grdKuerzen.IsEnabled)
            {
                lblAbschnitte.Text = kuerzen.Entfernen
                    ? "Wie viele Abschnitte entfernen?"
                    : "Wie viele Abschnitte behalten?";
                lblRichtung.Text = kuerzen.Entfernen
                    ? "Welche Abschnitte entfernen?"
                    : "Welche Abschnitte behalten?";

                const string beispiel = "Beleg_2025_04_01_Kunde.pdf";
                txtKuerzenBeispiel.Text = $"Beispiel: {beispiel}  ->  {kuerzen.Anwenden(beispiel)}";
            }
            else
            {
                txtKuerzenBeispiel.Text = istProtokoll
                    ? "Kürzen ist für Protokolle nicht verfügbar."
                    : "Die Namen werden unverändert exportiert.";
            }

            string format = rbTxt?.IsChecked == true ? "TXT" : "CSV";
            string encoding = rbAnsi?.IsChecked == true ? "ANSI" : "UTF-8";

            txtInfo.Text = $"Export: {quelle}";
            txtDateiname.Text = kuerzen.Aktiv && !istProtokoll
                ? $"Format: {format} | Kodierung: {encoding} | Gekürzt an '{kuerzen.Trennzeichen}'"
                : $"Format: {format} | Kodierung: {encoding}";

            btnExport.IsEnabled = rbVerschoben?.IsEnabled == true
                               || rbNichtGefunden?.IsEnabled == true
                               || rbAlle?.IsEnabled == true
                               || istProtokoll;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            ExportVerschobene = rbVerschoben?.IsChecked == true;
            ExportNichtGefunden = rbNichtGefunden?.IsChecked == true;
            ExportAlle = rbAlle?.IsChecked == true;
            ExportSuchprotokoll = rbSuchprotokoll?.IsChecked == true;
            ExportKopierprotokoll = rbKopierprotokoll?.IsChecked == true;
            ExportKomplettesProtokoll = rbKomplettesProtokoll?.IsChecked == true;
            ExportAlsCsv = rbCsv?.IsChecked == true;
            ExportAlsUtf8 = rbUtf8?.IsChecked == true;

            bool istProtokoll = ExportSuchprotokoll || ExportKopierprotokoll || ExportKomplettesProtokoll;
            Kuerzen = istProtokoll ? new KuerzenEinstellungen() : KuerzenLesen();

            bool listeVerfuegbar = (ExportVerschobene && rbVerschoben?.IsEnabled == true)
                                || (ExportNichtGefunden && rbNichtGefunden?.IsEnabled == true)
                                || (ExportAlle && rbAlle?.IsEnabled == true);

            if (!listeVerfuegbar && !istProtokoll)
            {
                System.Windows.MessageBox.Show(
                    "Die gewählte Liste enthält keine Einträge zum Exportieren.\n\nBitte wählen Sie eine Liste mit Daten oder brechen Sie den Export ab.",
                    "Keine Daten",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

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
