using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace Listenverschieber
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        static App()
        {
            // Die Zusatzbibliotheken (PdfPig, OpenXml, ...) liegen nicht neben
            // der EXE, sondern nach Paket und Version geordnet:
            //
            //   Erweiterungen\<Paket>\<Version>\<Name>.dll
            //
            // Findet die Laufzeit eine Bibliothek nicht im Programmverzeichnis,
            // wird sie hier aus diesem Unterbaum nachgeladen. Der Resolver
            // rechnet immer vom Programmverzeichnis aus und funktioniert
            // deshalb unabhaengig davon, aus welchem Verzeichnis das Programm
            // gestartet wurde.
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (kontext, name) =>
            {
                var wurzel = System.IO.Path.Combine(
                    System.AppContext.BaseDirectory, "Erweiterungen");

                if (!System.IO.Directory.Exists(wurzel))
                {
                    return null;
                }

                var treffer = System.IO.Directory
                    .EnumerateFiles(wurzel, name.Name + ".dll", System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault();

                return treffer is null ? null : kontext.LoadFromAssemblyPath(treffer);
            };
        }
    }

}
