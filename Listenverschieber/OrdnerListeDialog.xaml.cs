using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace Listenverschieber
{
    /// <summary>
    /// Dialog zum Laden einer Dateiliste direkt aus einem Ordner (Ersatz fuer "dir *.* /B > Liste.txt").
    /// </summary>
    public partial class OrdnerListeDialog : Window
    {
        public string Ordner { get; private set; } = string.Empty;
        public string Suchmuster { get; private set; } = "*.*";
        public bool UnterordnerEinbeziehen { get; private set; }
        public List<string> Dateiliste { get; private set; } = new List<string>();

        public OrdnerListeDialog(string vorgabePfad)
        {
            InitializeComponent();
            txtPfad.Text = vorgabePfad ?? string.Empty;
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtCustomFilter == null) return;
            txtCustomFilter.IsEnabled = cmbFilter.SelectedIndex == 4;
        }

        private void BtnDurchsuchen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Ordner auswählen"
            };
            if (Directory.Exists(txtPfad.Text))
                dialog.InitialDirectory = txtPfad.Text;

            if (dialog.ShowDialog() == true)
                txtPfad.Text = dialog.FolderName;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string ordner = txtPfad.Text.Trim();
            if (!Directory.Exists(ordner))
            {
                MessageBox.Show("Der angegebene Ordner existiert nicht.", "Ungültiger Pfad",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string muster;
            switch (cmbFilter.SelectedIndex)
            {
                case 1: muster = "*.pdf"; break;
                case 2: muster = "*.txt"; break;
                case 3: muster = "*.ini"; break;
                case 4:
                    string endung = txtCustomFilter.Text.Trim().TrimStart('*', '.');
                    if (string.IsNullOrWhiteSpace(endung))
                    {
                        MessageBox.Show("Bitte eine Dateiendung angeben, z.B. bin.", "Fehlender Filter",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    muster = "*." + endung;
                    break;
                default: muster = "*.*"; break;
            }

            try
            {
                var suchOption = chkUnterordner.IsChecked == true
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                Dateiliste = Directory.GetFiles(ordner, muster, suchOption)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Lesen des Ordners:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Dateiliste.Count == 0)
            {
                MessageBox.Show($"Im Ordner wurden keine Dateien mit dem Muster '{muster}' gefunden.",
                    "Keine Treffer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Ordner = ordner;
            Suchmuster = muster;
            UnterordnerEinbeziehen = chkUnterordner.IsChecked == true;
            DialogResult = true;
        }
    }
}
