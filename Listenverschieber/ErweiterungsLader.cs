using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Listenverschieber
{
    /// <summary>
    /// Laedt die mitgelieferten Erweiterungen (NuGet-Bibliotheken) aus dem
    /// Unterverzeichnis "Erweiterungen".
    ///
    /// Hintergrund: Die EXE selbst sucht ihren .NET-Host (hostfxr.dll und die
    /// zugehoerigen Laufzeitdateien) immer im eigenen Verzeichnis. Das ist im
    /// Startprogramm fest einkompiliert und laesst sich nicht umstellen.
    /// Verwaltete Bibliotheken duerfen dagegen woanders liegen, solange die
    /// Anwendung sie beim Laden selbst findet - genau das erledigt diese Klasse.
    /// </summary>
    internal static class ErweiterungsLader
    {
        private static string[] suchpfade = [];

        [ModuleInitializer]
        internal static void Init()
        {
            var basis = Path.Combine(AppContext.BaseDirectory, "Erweiterungen");
            if (!Directory.Exists(basis))
                return;

            // Das Hauptverzeichnis der Erweiterungen und alle Paketordner darunter.
            suchpfade = [basis, .. Directory.GetDirectories(basis, "*", SearchOption.AllDirectories)];

            AppDomain.CurrentDomain.AssemblyResolve += Aufloesen;
        }

        private static Assembly? Aufloesen(object? sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var ordner in suchpfade)
            {
                var datei = Path.Combine(ordner, name + ".dll");
                if (File.Exists(datei))
                {
                    try
                    {
                        return Assembly.LoadFrom(datei);
                    }
                    catch
                    {
                        // Passt die Datei nicht, wird weitergesucht.
                    }
                }
            }

            return null;
        }
    }
}
