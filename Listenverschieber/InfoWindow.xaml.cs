using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using MessageBox = System.Windows.MessageBox;

namespace Listenverschieber
{
    /// <summary>
    /// Info-/About-Fenster mit Autor, GitHub-Profil, KI-Hinweis, Lizenz und Version.
    /// </summary>
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();
            FelderFuellen();
        }

        private void FelderFuellen()
        {
            txtTitel.Text = ProgrammInfo.Name;
            txtVersion.Text = $"Version {ProgrammInfo.Version}  -  © {ProgrammInfo.CopyrightJahr} {ProgrammInfo.Autor}";
            txtAutor.Text = $"Geschrieben von {ProgrammInfo.Autor}.";
            runGitHub.Text = ProgrammInfo.GitHubProfil;
            linkGitHub.NavigateUri = new Uri(ProgrammInfo.GitHubProfil);
            txtKi.Text = ProgrammInfo.KiHinweis;
            txtLizenz.Text = $"{ProgrammInfo.Lizenz}\n\n{ProgrammInfo.LizenzKurz}";
            txtFremdlizenzen.Text = string.Join(
                Environment.NewLine,
                ProgrammInfo.NugetPakete.Select(p => $"• {p.Paket} {p.Version} ({p.Lizenz}) - {p.Zweck}"));
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Der Link konnte nicht geöffnet werden.\n\n{ex.Message}",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            e.Handled = true;
        }

        private void btnKopieren_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ProgrammInfo.Name} - Version {ProgrammInfo.Version}");
            sb.AppendLine($"© {ProgrammInfo.CopyrightJahr} {ProgrammInfo.Autor}");
            sb.AppendLine(ProgrammInfo.GitHubProfil);
            sb.AppendLine();
            sb.AppendLine(ProgrammInfo.KiHinweis);
            sb.AppendLine();
            sb.AppendLine(ProgrammInfo.Lizenz);
            sb.AppendLine(ProgrammInfo.LizenzKurz);

            try
            {
                System.Windows.Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Die Zwischenablage konnte nicht beschrieben werden.\n\n{ex.Message}",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnHilfe_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow haupt)
            {
                haupt.HilfeAnzeigen(null);
            }
            else
            {
                var hilfe = new HilfeWindow { Owner = this };
                hilfe.Show();
            }
        }
    }
}
