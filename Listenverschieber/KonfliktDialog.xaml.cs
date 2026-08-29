using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace Listenverschieber
{
    public enum KonfliktAktion
    {
        Ueberspringen,
        Ueberschreiben,
        QuelleUmbenennen,
        ZielUmbenennen
    }

    public class KonfliktErgebnis
    {
        public KonfliktAktion Aktion { get; set; } = KonfliktAktion.Ueberspringen;
        public bool FuerAlleAnwenden { get; set; } = false;
    }

    public partial class KonfliktDialog : Window
    {
        public KonfliktErgebnis Ergebnis { get; private set; } = new KonfliktErgebnis();

        public string QuellHash { get; private set; } = "";
        public string ZielHash { get; private set; } = "";
        public bool DateienSindIdentisch { get; private set; }

        public KonfliktDialog(string quellDateiPfad, string zielDateiPfad)
        {
            InitializeComponent();
            DateienAnzeigen(quellDateiPfad, zielDateiPfad);
        }

        /// <summary>
        /// Constructor with pre-computed hashes (to avoid recomputation).
        /// </summary>
        public KonfliktDialog(string quellDateiPfad, string zielDateiPfad, string quellHash, string zielHash)
        {
            InitializeComponent();
            QuellHash = quellHash;
            ZielHash = zielHash;
            DateienSindIdentisch = string.Equals(quellHash, zielHash, StringComparison.OrdinalIgnoreCase);
            DateienAnzeigen(quellDateiPfad, zielDateiPfad);
        }

        private void DateienAnzeigen(string quellDateiPfad, string zielDateiPfad)
        {
            var quellInfo = new FileInfo(quellDateiPfad);
            var zielInfo = new FileInfo(zielDateiPfad);

            // Hashes berechnen falls nicht vorhanden
            if (string.IsNullOrEmpty(QuellHash))
                QuellHash = BerechneHash(quellDateiPfad);
            if (string.IsNullOrEmpty(ZielHash))
                ZielHash = BerechneHash(zielDateiPfad);

            DateienSindIdentisch = string.Equals(QuellHash, ZielHash, StringComparison.OrdinalIgnoreCase);

            // Quelldatei
            txtQuellDatei.Text = quellInfo.Name;
            txtQuellPfad.Text = quellInfo.DirectoryName ?? "";
            txtQuellGroesse.Text = FormatGroesse(quellInfo.Length);
            txtQuellHash.Text = QuellHash;

            // Zieldatei
            txtZielDatei.Text = zielInfo.Name;
            txtZielPfad.Text = zielInfo.DirectoryName ?? "";
            txtZielGroesse.Text = FormatGroesse(zielInfo.Length);
            txtZielHash.Text = ZielHash;

            // Kurz-Hash für Button-Texte
            string quellKurz = QuellHash.Length >= 8 ? QuellHash[..8] : QuellHash;
            string zielKurz = ZielHash.Length >= 8 ? ZielHash[..8] : ZielHash;

            btnQuelleUmbenennen.Content = $"📄 Quelle umbenennen (_{quellKurz})";
            btnZielUmbenennen.Content = $"📄 Ziel umbenennen (_{zielKurz})";

            // Status
            if (DateienSindIdentisch)
            {
                txtStatus.Text = "✅ Die Dateien sind IDENTISCH (gleicher Hash)";
                brdStatus.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(200, 240, 200));
            }
            else
            {
                txtStatus.Text = "⚠ Die Dateien sind UNTERSCHIEDLICH (verschiedener Hash)";
                brdStatus.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 230, 180));
            }
        }

        public static string BerechneHash(string dateiPfad)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(dateiPfad);
            var hashBytes = md5.ComputeHash(stream);
            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// Returns a short hash (first 8 chars) suitable for appending to filenames.
        /// </summary>
        public static string KurzHash(string fullHash)
        {
            return fullHash.Length >= 8 ? fullHash[..8] : fullHash;
        }

        /// <summary>
        /// Builds a new filename with hash suffix: "name_A1B2C3D4.ext"
        /// </summary>
        public static string DateinameMitHash(string dateiPfad, string hash)
        {
            string verzeichnis = Path.GetDirectoryName(dateiPfad) ?? "";
            string name = Path.GetFileNameWithoutExtension(dateiPfad);
            string ext = Path.GetExtension(dateiPfad);
            string kurzHash = KurzHash(hash);
            return Path.Combine(verzeichnis, $"{name}_{kurzHash}{ext}");
        }

        private static string FormatGroesse(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Bytes";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void SetzeErgebnis(KonfliktAktion aktion)
        {
            Ergebnis = new KonfliktErgebnis
            {
                Aktion = aktion,
                FuerAlleAnwenden = chkFuerAlle.IsChecked == true
            };
            DialogResult = true;
        }

        private void BtnUeberspringen_Click(object sender, RoutedEventArgs e)
            => SetzeErgebnis(KonfliktAktion.Ueberspringen);

        private void BtnUeberschreiben_Click(object sender, RoutedEventArgs e)
            => SetzeErgebnis(KonfliktAktion.Ueberschreiben);

        private void BtnQuelleUmbenennen_Click(object sender, RoutedEventArgs e)
            => SetzeErgebnis(KonfliktAktion.QuelleUmbenennen);

        private void BtnZielUmbenennen_Click(object sender, RoutedEventArgs e)
            => SetzeErgebnis(KonfliktAktion.ZielUmbenennen);
    }
}
