namespace Listenverschieber
{
    /// <summary>
    /// Informationen über verschobene Dateien für Rückverschiebung
    /// </summary>
    public class VerschobeneDateiInfo
    {
        public string Quelldatei { get; set; } = "";
        public string Zieldatei { get; set; } = "";
        public string QuellOrdner { get; set; } = "";
        public DateTime VerschobenAm { get; set; }
        public List<string> ZugehoerigeDateien { get; set; } = new List<string>();
    }
}
