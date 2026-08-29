namespace Listenverschieber
{
    /// <summary>
    /// Klasse für Log-Einträge mit optionalem Dateipfad
    /// </summary>
    public class LogEintrag
    {
        public string Zeitstempel { get; set; } = "";
        public string Nachricht { get; set; } = "";
        public string? Dateipfad { get; set; }

        public override string ToString()
        {
            return $"{Zeitstempel} - {Nachricht}";
        }
    }
}
