using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace Listenverschieber
{
    /// <summary>
    /// Liest den Textinhalt durchsuchbarer Dateien (TXT, INI, PDF, DOCX, XLSX, ...).
    /// </summary>
    public static class DateiInhaltsLeser
    {
        private static readonly HashSet<string> TextEndungen = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".ini", ".log", ".csv", ".xml", ".json", ".htm", ".html", ".dat", ".cfg", ".conf", ".md"
        };

        /// <summary>
        /// Alle Endungen, die inhaltlich durchsucht werden koennen.
        /// </summary>
        public static IEnumerable<string> UnterstuetzteEndungen =>
            TextEndungen.Concat(new[] { ".pdf", ".docx", ".xlsx" });

        public static bool IstDurchsuchbar(string dateiPfad)
        {
            var ext = Path.GetExtension(dateiPfad);
            return TextEndungen.Contains(ext)
                || ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Liest den Textinhalt einer Datei. Gibt null zurueck, wenn der Typ nicht
        /// unterstuetzt wird oder kein Text extrahiert werden konnte.
        /// </summary>
        public static string? LiesText(string dateiPfad, out string? fehler)
        {
            fehler = null;
            try
            {
                var ext = Path.GetExtension(dateiPfad);

                if (TextEndungen.Contains(ext))
                {
                    return LiesTextDatei(dateiPfad);
                }
                if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return LiesPdf(dateiPfad);
                }
                if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    return LiesWord(dateiPfad);
                }
                if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return LiesExcel(dateiPfad);
                }

                fehler = $"Dateityp '{ext}' wird nicht durchsucht";
                return null;
            }
            catch (Exception ex)
            {
                fehler = ex.Message;
                return null;
            }
        }

        private static string LiesTextDatei(string dateiPfad)
        {
            // Encoding automatisch erkennen, Fallback auf Windows-1252 fuer alte INI-Dateien
            using var reader = new StreamReader(dateiPfad, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private static string LiesPdf(string dateiPfad)
        {
            var sb = new StringBuilder();
            using var dokument = PdfDocument.Open(dateiPfad);
            foreach (var seite in dokument.GetPages())
            {
                sb.AppendLine(seite.Text);
            }
            return sb.ToString();
        }

        private static string LiesWord(string dateiPfad)
        {
            using var dokument = WordprocessingDocument.Open(dateiPfad, false);
            return dokument.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
        }

        private static string LiesExcel(string dateiPfad)
        {
            var sb = new StringBuilder();
            using var dokument = SpreadsheetDocument.Open(dateiPfad, false);
            var workbookPart = dokument.WorkbookPart;
            if (workbookPart == null)
            {
                return string.Empty;
            }

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

            foreach (var sheetPart in workbookPart.WorksheetParts)
            {
                foreach (var zelle in sheetPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                {
                    var wert = zelle.CellValue?.InnerText;
                    if (string.IsNullOrEmpty(wert))
                    {
                        continue;
                    }

                    if (zelle.DataType != null
                        && zelle.DataType.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString
                        && sharedStrings != null
                        && int.TryParse(wert, out int index)
                        && index >= 0 && index < sharedStrings.ChildElements.Count)
                    {
                        wert = sharedStrings.ChildElements[index].InnerText;
                    }

                    sb.AppendLine(wert);
                }
            }

            return sb.ToString();
        }
    }
}
