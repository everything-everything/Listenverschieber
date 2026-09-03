// Startprogramm fuer die Release-Pakete des Listenverschiebers.
//
// Im Wurzelverzeichnis eines Pakets liegt nur diese kleine Datei, die
// eigentliche Anwendung samt .NET-Laufzeit steckt im Unterordner
// "Programm". So sind vorne nur wenige Eintraege sichtbar, ohne dass an
// der Laufzeit selbst etwas veraendert werden muss.
//
// Bewusst in C++ geschrieben: NativeAOT beherrscht kein win-x86, ein
// gewoehnliches .NET-Programm wiederum wuerde seine Laufzeit neben sich
// erwarten - und die liegt hier eben im Unterordner.
//
// Der Pfad wird aus dem eigenen Dateipfad gebildet, nicht aus dem
// Arbeitsverzeichnis. Nur so klappt der Start auch ueber eine
// Verknuepfung oder aus einem fremden Verzeichnis heraus.

#include <windows.h>
#include <shlwapi.h>
#include <string>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "shell32.lib")

namespace {

const wchar_t* const kTitel = L"Listenverschieber";
const wchar_t* const kProgrammordner = L"Programm";
const wchar_t* const kAnwendung = L"Listenverschieber.exe";

void Melde(const std::wstring& text) {
	MessageBoxW(nullptr, text.c_str(), kTitel, MB_OK | MB_ICONERROR);
}

// Verzeichnis, in dem diese Datei liegt - mit abschliessendem Trennzeichen.
std::wstring EigenesVerzeichnis() {
	wchar_t puffer[MAX_PATH];
	const DWORD laenge = GetModuleFileNameW(nullptr, puffer, MAX_PATH);
	if (laenge == 0 || laenge >= MAX_PATH) {
		return std::wstring();
	}

	std::wstring pfad(puffer, laenge);
	const size_t trenner = pfad.find_last_of(L'\\');
	if (trenner == std::wstring::npos) {
		return std::wstring();
	}

	return pfad.substr(0, trenner + 1);
}

}  // namespace

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int nCmdShow) {
	const std::wstring wurzel = EigenesVerzeichnis();
	if (wurzel.empty()) {
		Melde(L"Der eigene Programmpfad konnte nicht ermittelt werden.");
		return 2;
	}

	const std::wstring ordner = wurzel + kProgrammordner;
	const std::wstring programm = ordner + L"\\" + kAnwendung;

	if (!PathFileExistsW(programm.c_str())) {
		Melde(L"Die Programmdatei wurde nicht gefunden:\n\n" + programm +
			  L"\n\nBitte das Paket vollstaendig entpacken - der Ordner\n\"" +
			  kProgrammordner + L"\" muss neben dieser Datei liegen.");
		return 2;
	}

	// Die Aufrufargumente unveraendert weiterreichen, damit sich Dateien
	// auch auf diese Datei ziehen lassen. GetCommandLineW liefert den
	// eigenen Dateinamen als erstes Element; der wird durch den Pfad zur
	// eigentlichen Anwendung ersetzt.
	std::wstring befehl = L"\"" + programm + L"\"";

	int anzahl = 0;
	LPWSTR* teile = CommandLineToArgvW(GetCommandLineW(), &anzahl);
	if (teile != nullptr) {
		for (int i = 1; i < anzahl; ++i) {
			befehl += L" \"";
			befehl += teile[i];
			befehl += L"\"";
		}
		LocalFree(teile);
	}

	STARTUPINFOW start = {};
	start.cb = sizeof(start);
	start.dwFlags = STARTF_USESHOWWINDOW;
	start.wShowWindow = static_cast<WORD>(nCmdShow);

	PROCESS_INFORMATION vorgang = {};

	// CreateProcessW veraendert den Puffer, deshalb eine eigene Kopie.
	std::wstring puffer = befehl;

	const BOOL erfolg = CreateProcessW(
		programm.c_str(),
		&puffer[0],
		nullptr,
		nullptr,
		FALSE,
		0,
		nullptr,
		ordner.c_str(),
		&start,
		&vorgang);

	if (!erfolg) {
		wchar_t nummer[16];
		wsprintfW(nummer, L"%lu", GetLastError());
		Melde(std::wstring(L"Der Listenverschieber konnte nicht gestartet "
						   L"werden.\n\nFehlernummer: ") + nummer);
		return 1;
	}

	// Nicht auf das Programmende warten - der Launcher wird nicht mehr
	// gebraucht, sobald die Anwendung laeuft.
	CloseHandle(vorgang.hThread);
	CloseHandle(vorgang.hProcess);
	return 0;
}
