using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;

namespace Listenverschieber
{
    /// <summary>
    /// Hilfe-Fenster mit Themenübersicht, technischen Informationen und Bedienungsanleitung.
    /// </summary>
    public partial class HilfeWindow : Window
    {
        private readonly List<HilfeThema> _themen;

        public HilfeWindow()
        {
            InitializeComponent();
            _themen = HilfeInhalte.Alle().ToList();
            lstThemen.ItemsSource = _themen;
            if (_themen.Count > 0)
                lstThemen.SelectedIndex = 0;
        }

        /// <summary>Öffnet das Fenster und springt direkt zu einem Thema.</summary>
        public void ThemaAnzeigen(string titel)
        {
            var thema = _themen.FirstOrDefault(t =>
                t.Titel.Contains(titel, StringComparison.OrdinalIgnoreCase));
            if (thema != null)
                lstThemen.SelectedItem = thema;
        }

        private void lstThemen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstThemen.SelectedItem is not HilfeThema thema)
                return;

            txtKopf.Text = thema.Titel;

            // Alle Absaetze liegen in einem einzigen Dokument, damit sich der Text
            // ueber Absatzgrenzen und Leerzeilen hinweg zusammenhaengend markieren
            // und mit Strg+C kopieren laesst.
            var dokument = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                PagePadding = new Thickness(0)
            };

            foreach (var absatz in thema.Absaetze)
            {
                var istUeberschrift = absatz.StartsWith("## ", StringComparison.Ordinal);
                var text = istUeberschrift ? absatz[3..] : absatz;

                dokument.Blocks.Add(new Paragraph(new Run(text))
                {
                    Margin = istUeberschrift ? new Thickness(0, 14, 0, 6) : new Thickness(0, 0, 0, 8),
                    FontWeight = istUeberschrift ? FontWeights.Bold : FontWeights.Normal,
                    FontSize = istUeberschrift ? 14 : 13,
                    Foreground = istUeberschrift
                        ? new SolidColorBrush(Color.FromRgb(0x1F, 0x4E, 0x79))
                        : System.Windows.SystemColors.ControlTextBrush
                });
            }

            rtbInhalt.Document = dokument;
            rtbInhalt.ScrollToHome();
        }

        private void txtSuche_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filter = txtSuche.Text.Trim();
            if (filter.Length == 0)
            {
                lstThemen.ItemsSource = _themen;
            }
            else
            {
                lstThemen.ItemsSource = _themen
                    .Where(t => t.Titel.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || t.Absaetze.Any(a => a.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            if (lstThemen.SelectedItem == null && lstThemen.Items.Count > 0)
                lstThemen.SelectedIndex = 0;
        }

        private void btnSchliessen_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Kopiert das aktuell angezeigte Thema in die Zwischenablage.</summary>
        private void btnThemaKopieren_Click(object sender, RoutedEventArgs e)
        {
            if (lstThemen.SelectedItem is not HilfeThema thema)
                return;

            var text = new StringBuilder();
            text.AppendLine(thema.Titel);
            text.AppendLine(new string('=', thema.Titel.Length));
            text.AppendLine();

            foreach (var absatz in thema.Absaetze)
            {
                text.AppendLine(absatz.StartsWith("## ", StringComparison.Ordinal)
                    ? absatz[3..]
                    : absatz);
                text.AppendLine();
            }

            try
            {
                System.Windows.Clipboard.SetText(text.ToString());
                txtKopf.Text = $"{thema.Titel}  (in die Zwischenablage kopiert)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Der Text konnte nicht kopiert werden.\n\n{ex.Message}",
                    "Hilfe", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnAlsTextSpeichern_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Handbuch speichern",
                Filter = "Textdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"{ProgrammInfo.Name}_Handbuch_{ProgrammInfo.Version}.txt"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, HilfeInhalte.AllesAlsText());
                MessageBox.Show("Das Handbuch wurde gespeichert.", "Hilfe",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Das Handbuch konnte nicht gespeichert werden.\n\n{ex.Message}",
                    "Hilfe", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
