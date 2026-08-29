namespace Listenverschieber
{
    /// <summary>
    /// Zentrale Programm-, Autoren- und Lizenzangaben.
    /// Hier bei Bedarf anpassen - alle Fenster lesen die Werte von hier.
    /// </summary>
    public static class ProgrammInfo
    {
        public const string Name = "Listenverschieber";

        /// <summary>
        /// Anzeigeversion. Wird aus der Assembly gelesen, damit nur die
        /// Projektdatei (Listenverschieber.csproj, Element &lt;Version&gt;) gepflegt werden muss.
        /// </summary>
        public static string Version
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "3.11" : $"{v.Major}.{v.Minor:00}";
            }
        }

        /// <summary>GitHub-Benutzername des Autors - bei Bedarf hier anpassen.</summary>
        public const string GitHubBenutzer = "everything-everything";

        public static string Autor => GitHubBenutzer;
        public static string GitHubProfil => $"https://github.com/{GitHubBenutzer}";
        public static string GitHubProjekt => $"https://github.com/{GitHubBenutzer}/{Name}";

        public const string Lizenz = "MIT-Lizenz";
        public const int CopyrightJahr = 2026;

        public const string LizenzKurz =
            "Dieses Programm steht unter der MIT-Lizenz. Es darf kostenlos genutzt, " +
            "verändert und weitergegeben werden - auch kommerziell. Die Weiterentwicklung " +
            "durch andere ist ausdrücklich erwünscht. Einzige Bedingung ist, dass der " +
            "Copyright-Hinweis und der Lizenztext erhalten bleiben. Die Software wird " +
            "ohne jede Gewährleistung bereitgestellt.";

        public const string KiHinweis =
            "Bei der Entwicklung hat GitHub Copilot (Claude) mitgewirkt - unter anderem " +
            "bei Entwurf, Implementierung und Dokumentation. Konzept, Anforderungen, " +
            "fachliche Vorgaben und Prüfung der Ergebnisse stammen vom Autor.";

        /// <summary>Verwendete Technologien.</summary>
        public static readonly (string Bereich, string Wert)[] Technologie =
        {
            ("Sprache", "C# 12"),
            ("Plattform", ".NET 8 (net8.0-windows)"),
            ("Oberfläche", "WPF (Windows Presentation Foundation)"),
            ("Zusatz", "Windows Forms (nur für Ordnerauswahl-Dialog)"),
            ("Projektformat", "SDK-Style csproj"),
            ("Zielplattformen", "win-x64, win-x86 und win-arm64"),
            ("Entwicklungsumgebung", "Microsoft Visual Studio 2026")
        };

        /// <summary>Eingebundene NuGet-Pakete inklusive Lizenz und Zweck.</summary>
        public static readonly (string Paket, string Version, string Lizenz, string Zweck)[] NugetPakete =
        {
            ("PdfPig", "0.1.10", "Apache-2.0", "Textextraktion aus durchsuchbaren PDF-Dateien"),
            ("DocumentFormat.OpenXml", "3.1.0", "MIT", "Textextraktion aus Word- (.docx) und Excel-Dateien (.xlsx)"),
            ("System.Text.Encoding.CodePages", "9.0.9", "MIT", "Unterstützung älterer Zeichensätze (z.B. Windows-1252)")
        };

        /// <summary>Systemvoraussetzungen.</summary>
        public static readonly (string Punkt, string Wert)[] Systemvoraussetzungen =
        {
            ("Betriebssystem", "Windows 10 (Version 1809) oder neuer, Windows 11, Windows Server 2019+"),
            ("Architektur", "x64 (64 Bit), x86 (32 Bit) oder ARM64"),
            ("Laufzeitumgebung", ".NET 8 Desktop Runtime (nur nötig bei den Paketen der Variante 'framework')"),
            ("Arbeitsspeicher", "Mindestens 256 MB frei; bei sehr großen PDF-Beständen mehr empfohlen"),
            ("Festplatte", "ca. 15 MB bei der Variante 'framework', ca. 165 MB bei der Variante 'standalone'"),
            ("Rechte", "Lese- und Schreibrechte auf den verwendeten Quell- und Zielverzeichnissen"),
            ("Netzwerk", "Nicht erforderlich - das Programm arbeitet vollständig lokal")
        };

        /// <summary>Speicherort der Konfigurationsdatei.</summary>
        public static string KonfigurationsPfad => PfadKonfiguration.ConfigFilePath;
    }
}
