// Startfenster fuer die Release-Pakete.
//
// Zeigt ein kleines Fenster mit Fortschrittsbalken und startet danach
// die Anwendung aus dem Unterordner "Programm".
//
// Hintergrund: Eine EXE, die beim Start sofort und unsichtbar eine
// andere EXE startet, entspricht dem Muster eines Droppers. Genau das
// hatte die Vorgaengerfassung getan, woraufhin Norton die Anwendung in
// Quarantaene verschob und Defender Wacatac.C!ml meldete.
//
// Diese Fassung zeigt zuerst ein Fenster und laesst Zeit vergehen,
// bevor sie den Prozess startet. Ob das den Scannern genuegt, muss
// gemessen werden - die Startdatei "Listenverschieber.cmd" bleibt die
// erprobte Alternative.

#include <windows.h>
#include <commctrl.h>
#include <shlwapi.h>
#include <string>

#include "Einzelinstanz.h"

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "shell32.lib")

// Die Abhaengigkeit auf die neuen Steuerelemente steht in
// Launcher.manifest. Sie darf nicht zusaetzlich per #pragma gesetzt
// werden: ein eingebundenes Manifest ersetzt solche Angaben ohnehin.

namespace {

const wchar_t* const kTitel = L"Listenverschieber";
const wchar_t* const kProgrammordner = L"Programm";
const wchar_t* const kAnwendung = L"Listenverschieber.exe";

// Anzeigedauer des Fensters. Lang genug, dass der Start nicht mehr
// unmittelbar auf das Programmereignis folgt, kurz genug, dass es beim
// taeglichen Gebrauch nicht stoert.
const UINT kSchritte = 20;
const UINT kSchrittdauer = 60;

const int kBreite = 380;
const int kHoehe = 130;

const UINT_PTR kZeitgeber = 1;

HWND g_balken = nullptr;
HFONT g_schrift = nullptr;
UINT g_stand = 0;
std::wstring g_wurzel;
std::wstring g_ordner;
std::wstring g_programm;

void Melde(const std::wstring& text) {
	MessageBoxW(nullptr, text.c_str(), kTitel, MB_OK | MB_ICONERROR);
}

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

// Startet die Anwendung ueber die Batch, nicht direkt.
//
// Versuchsfassung: Sie unterscheidet sich von "Startfenster.cpp" nur
// darin, dass der Prozess ueber "Listenverschieber.cmd" laeuft. Damit
// ist der Elternprozess der Anwendung cmd.exe statt dieser EXE. So laesst
// sich messen, ob die Virenscanner auf den Elternprozess reagieren oder
// auf die unbekannte EXE an sich.
bool AnwendungStarten() {
	// Laeuft die Anwendung schon, wird nur ihr Fenster nach vorne geholt.
	// Sonst entstuenden bei einem Doppelklick zwei Fenster an derselben
	// Stelle, und das untere taucht erst beim Schliessen des oberen auf.
	if (einzelinstanz::LaeuftBereits(g_programm)) {
		return true;
	}

	wchar_t systemordner[MAX_PATH];
	if (GetSystemDirectoryW(systemordner, MAX_PATH) == 0) {
		Melde(L"Das Systemverzeichnis konnte nicht ermittelt werden.");
		return false;
	}

	const std::wstring cmd = std::wstring(systemordner) + L"\\cmd.exe";
	const std::wstring batch = g_wurzel + L"Listenverschieber.cmd";

	std::wstring befehl = L"\"" + cmd + L"\" /c \"\"" + batch + L"\"";

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
	befehl += L"\"";

	STARTUPINFOW start = {};
	start.cb = sizeof(start);
	start.dwFlags = STARTF_USESHOWWINDOW;
	start.wShowWindow = SW_HIDE;

	PROCESS_INFORMATION vorgang = {};
	std::wstring puffer = befehl;

	const BOOL erfolg = CreateProcessW(
		cmd.c_str(), &puffer[0], nullptr, nullptr, FALSE, CREATE_NO_WINDOW,
		nullptr, g_wurzel.c_str(), &start, &vorgang);

	if (!erfolg) {
		wchar_t nummer[16];
		wsprintfW(nummer, L"%lu", GetLastError());
		Melde(std::wstring(L"Der Listenverschieber konnte nicht gestartet "
						   L"werden.\n\nFehlernummer: ") + nummer);
		return false;
	}

	CloseHandle(vorgang.hThread);
	CloseHandle(vorgang.hProcess);
	return true;
}

LRESULT CALLBACK FensterVerfahren(HWND fenster, UINT nachricht,
								  WPARAM wParam, LPARAM lParam) {
	switch (nachricht) {
	case WM_CREATE: {
		g_schrift = CreateFontW(-14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
								DEFAULT_CHARSET, OUT_DEFAULT_PRECIS,
								CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
								DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

		HWND text = CreateWindowExW(
			0, L"STATIC", L"Listenverschieber wird gestartet...",
			WS_CHILD | WS_VISIBLE,
			24, 24, kBreite - 60, 22, fenster, nullptr, nullptr, nullptr);
		SendMessageW(text, WM_SETFONT, reinterpret_cast<WPARAM>(g_schrift), TRUE);

		g_balken = CreateWindowExW(
			0, PROGRESS_CLASSW, nullptr,
			WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
			24, 58, kBreite - 60, 18, fenster, nullptr, nullptr, nullptr);
		SendMessageW(g_balken, PBM_SETRANGE, 0, MAKELPARAM(0, kSchritte));

		SetTimer(fenster, kZeitgeber, kSchrittdauer, nullptr);
		return 0;
	}

	case WM_TIMER: {
		if (wParam != kZeitgeber) {
			break;
		}

		++g_stand;
		SendMessageW(g_balken, PBM_SETPOS, g_stand, 0);

		if (g_stand >= kSchritte) {
			KillTimer(fenster, kZeitgeber);
			const bool erfolg = AnwendungStarten();
			DestroyWindow(fenster);
			if (!erfolg) {
				PostQuitMessage(1);
			}
		}
		return 0;
	}

	case WM_CTLCOLORSTATIC:
		SetBkMode(reinterpret_cast<HDC>(wParam), TRANSPARENT);
		return reinterpret_cast<LRESULT>(GetSysColorBrush(COLOR_WINDOW));

	case WM_DESTROY:
		if (g_schrift != nullptr) {
			DeleteObject(g_schrift);
			g_schrift = nullptr;
		}
		PostQuitMessage(0);
		return 0;
	}

	return DefWindowProcW(fenster, nachricht, wParam, lParam);
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instanz, HINSTANCE, PWSTR, int) {
	// Zweiter Klick waehrend des Ladebalkens: Zu diesem Zeitpunkt laeuft
	// die Anwendung noch nicht, die Pruefung weiter unten wuerde also
	// nicht greifen. Deshalb laesst sich das Startprogramm selbst nur
	// einmal je Sitzung ausfuehren.
	HANDLE sperre = CreateMutexW(nullptr, TRUE, L"Local\\ListenverschieberStartprogramm");
	if (sperre != nullptr && GetLastError() == ERROR_ALREADY_EXISTS) {
		CloseHandle(sperre);
		return 0;
	}

	const std::wstring wurzel = EigenesVerzeichnis();
	if (wurzel.empty()) {
		Melde(L"Der eigene Programmpfad konnte nicht ermittelt werden.");
		return 2;
	}

	g_wurzel = wurzel;
	g_ordner = wurzel + kProgrammordner;
	g_programm = g_ordner + L"\\" + kAnwendung;

	if (!PathFileExistsW(g_programm.c_str())) {
		Melde(L"Die Programmdatei wurde nicht gefunden:\n\n" + g_programm +
			  L"\n\nBitte das Paket vollstaendig entpacken - der Ordner\n\"" +
			  kProgrammordner + L"\" muss neben dieser Datei liegen.");
		return 2;
	}

	INITCOMMONCONTROLSEX steuerelemente = {};
	steuerelemente.dwSize = sizeof(steuerelemente);
	steuerelemente.dwICC = ICC_PROGRESS_CLASS;
	InitCommonControlsEx(&steuerelemente);

	WNDCLASSEXW klasse = {};
	klasse.cbSize = sizeof(klasse);
	klasse.lpfnWndProc = FensterVerfahren;
	klasse.hInstance = instanz;
	klasse.hCursor = LoadCursorW(nullptr, IDC_ARROW);
	klasse.hbrBackground = GetSysColorBrush(COLOR_WINDOW);
	klasse.lpszClassName = L"ListenverschieberStart";
	RegisterClassExW(&klasse);

	// Mittig auf dem Bildschirm, ohne Rahmenschaltflaechen
	const int x = (GetSystemMetrics(SM_CXSCREEN) - kBreite) / 2;
	const int y = (GetSystemMetrics(SM_CYSCREEN) - kHoehe) / 2;

	HWND fenster = CreateWindowExW(
		0, klasse.lpszClassName, kTitel,
		WS_POPUPWINDOW | WS_CAPTION,
		x, y, kBreite, kHoehe,
		nullptr, nullptr, instanz, nullptr);

	if (fenster == nullptr) {
		Melde(L"Das Startfenster konnte nicht erstellt werden.");
		return 1;
	}

	ShowWindow(fenster, SW_SHOW);
	UpdateWindow(fenster);

	MSG nachricht;
	while (GetMessageW(&nachricht, nullptr, 0, 0) > 0) {
		TranslateMessage(&nachricht);
		DispatchMessageW(&nachricht);
	}

	return static_cast<int>(nachricht.wParam);
}
