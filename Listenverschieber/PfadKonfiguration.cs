using System.IO;
using System.Text.Json;

namespace Listenverschieber
{
    /// <summary>
    /// Konfigurationsklasse für gespeicherte Pfade
    /// </summary>
    public class PfadKonfiguration
    {
        public string Arbeitspfad { get; set; } = "";
        public string Verschiebepfad { get; set; } = "";
        public bool UseMoveFolder { get; set; } = false;

        // Listen für mehrere Pfade
        public List<string> ArbeitspfadListe { get; set; } = new List<string>();
        public List<string> UeberwachungspfadListe { get; set; } = new List<string>();

        // Tab 2 - Unvollständige Dateien
        public string Ueberwachungspfad { get; set; } = "";
        public string VerschiebepfadTab2 { get; set; } = "";
        public bool UseMoveFolderTab2 { get; set; } = true;
        public string Hauptformat { get; set; } = "*";
        public string Pflichtdatei1 { get; set; } = "txt";
        public string Pflichtdatei2 { get; set; } = "ini";
        public bool AutoRueckverschiebung { get; set; } = false;
        public int RueckschiebeZeitSekunden { get; set; } = 10;
        public bool ProcessAllWatchPaths { get; set; } = false;

        // Tab 1 - Listenverschieber
        public bool IgnoreExtensionInList { get; set; } = true;
        public bool NameBeginntMit { get; set; } = false;
        public bool NameEndetMit { get; set; } = false;
        public bool ProcessAllPaths { get; set; } = false;
        public bool TrennzeichenSuche { get; set; } = false;
        public bool TrennzeichenAuto { get; set; } = true;
        public string TrennzeichenManuell { get; set; } = "_";
        public bool AbschnittAuto { get; set; } = true;
        public int AbschnittNummer { get; set; } = 1;
        public bool Einzelabschnittsuche { get; set; } = false;
        public bool DuplikateFiltern { get; set; } = false;

        // Tab 3 - Inhaltsbasiert umbenennen
        public string UmbArbeitspfad { get; set; } = "";
        public List<string> UmbArbeitspfadListe { get; set; } = new List<string>();
        public bool UmbUnterordner { get; set; } = false;
        public string UmbSuchschluessel { get; set; } = "Datum=";
        public string UmbQuelldateiEndungen { get; set; } = "ini";
        public string UmbTrennzeichen { get; set; } = "_";
        public bool UmbAbschnittAuto { get; set; } = true;
        public int UmbAbschnittNummer { get; set; } = 4;
        public bool UmbAbschnittVonHinten { get; set; } = false;
        public string UmbQuellFormatInhalt { get; set; } = "dd.MM.yyyy";
        public string UmbQuellFormatDateiname { get; set; } = "yyyyMMdd";
        public string UmbZielFormatDateiname { get; set; } = "yyyyMMdd";
        public bool UmbAlsDatumFormatieren { get; set; } = true;
        public string UmbAutoMuster { get; set; } = @"^\d+$";
        public int UmbMusterTyp { get; set; } = 0;
        public int UmbMusterLaenge { get; set; } = 0;
        public bool UmbGleichnamigeMitumbenennen { get; set; } = true;

        // Tab 4 - Inhaltssuche
        public string InhSuchpfad { get; set; } = "";
        public List<string> InhSuchpfadListe { get; set; } = new List<string>();
        public string InhZielpfad { get; set; } = "";
        public bool InhUseMoveFolder { get; set; } = true;
        public bool InhUnterordner { get; set; } = false;
        public string InhSuchbegriff { get; set; } = "";
        public string InhDateiEndungen { get; set; } = "txt";
        public int InhSuchModus { get; set; } = 0;
        public bool InhGrossKleinBeachten { get; set; } = false;
        public bool InhPlatzhalter { get; set; } = false;
        public bool InhGleichnamigeMitnehmen { get; set; } = true;
        public int InhKonfliktAktion { get; set; } = 0;

        public static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Listenverschieber",
            "config.json"
        );

        public static PfadKonfiguration Laden()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<PfadKonfiguration>(json) ?? new PfadKonfiguration();
                }
            }
            catch
            {
                // Ignoriere Fehler beim Laden
            }

            return new PfadKonfiguration();
        }

        public void Speichern()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // Ignoriere Fehler beim Speichern
            }
        }
    }
}
