// Startfenster mit Schaltflaechen fuer die Release-Pakete.
//
// Versuchsfassung: Anders als "Startfenster.cpp" und
// "StartfensterUeberBatch.cpp" startet diese Fassung gar nichts von
// selbst. Sie zeigt ein Fenster mit zwei Schaltflaechen und wartet auf
// einen Klick des Benutzers.
//
// Hintergrund: Eine EXE, die beim Start unaufgefordert eine andere EXE
// startet, entspricht dem Muster eines Droppers - Norton verschob die
// Anwendung deswegen in Quarantaene, Defender meldete Wacatac.C!ml. Ein
// Fortschrittsbalken verzoegert den Start zwar, der Start folgt aber
// weiterhin ohne Zutun des Benutzers. Hier dagegen steht zwischen dem
// Programmstart und dem Erzeugen des Prozesses eine echte Eingabe.
//
// Ob das den Scannern genuegt, muss gemessen werden - die Startdatei
// "Listenverschieber.cmd" bleibt die erprobte Alternative.

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
const wchar_t* const kStartdatei = L"Listenverschieber.cmd";

const int kBreite = 430;
const int kHoehe = 190;

const int kSchaltflaecheBreite = 180;
const int kSchaltflaecheHoehe = 34;

// Kennungen der beiden Schaltflaechen
const int kIdDirekt = 101;
const int kIdStartdatei = 102;

HFONT g_schrift = nullptr;
std::wstring g_wurzel;
std::wstring g_ordner;
std::wstring g_programm;

void Melde(const std::wstring& text) {
	MessageBoxW(nullptr, text.c_str(), kTitel, MB_OK | MB_ICONERROR);
}

void MeldeFehlschlag(const std::wstring& was) {
	wchar_t nummer[16];
	wsprintfW(nummer, L"%lu", GetLastError());
	Melde(was + L"\n\nFehlernummer: " + nummer);
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

// Haengt die eigenen Aufrufargumente an eine Befehlszeile an.
void ArgumenteAnhaengen(std::wstring& befehl) {
	int anzahl = 0;
	LPWSTR* teile = CommandLineToArgvW(GetCommandLineW(), &anzahl);
	if (teile == nullptr) {
		return;
	}

	for (int i = 1; i < anzahl; ++i) {
		befehl += L" \"";
		befehl += teile[i];
		befehl += L"\"";
	}

	LocalFree(teile);
}

// Startet die Anwendung unmittelbar aus dem Unterordner "Programm".
bool DirektStarten() {
	if (einzelinstanz::LaeuftBereits(g_programm)) {
		return true;
	}

	std::wstring befehl = L"\"" + g_programm + L"\"";
	ArgumenteAnhaengen(befehl);

	STARTUPINFOW start = {};
	start.cb = sizeof(start);

	PROCESS_INFORMATION vorgang = {};
	std::wstring puffer = befehl;

	const BOOL erfolg = CreateProcessW(
		g_programm.c_str(), &puffer[0], nullptr, nullptr, FALSE, 0,
		nullptr, g_ordner.c_str(), &start, &vorgang);

	if (!erfolg) {
		MeldeFehlschlag(L"Der Listenverschieber konnte nicht gestartet werden.");
		return false;
	}

	CloseHandle(vorgang.hThread);
	CloseHandle(vorgang.hProcess);
	return true;
}

// Startet die Anwendung ueber die Startdatei, nicht direkt.
//
// Damit ist der Elternprozess der Anwendung cmd.exe statt dieser EXE. So
// laesst sich messen, ob die Virenscanner auf den Elternprozess reagieren
// oder auf die unbekannte EXE an sich.
bool UeberStartdateiStarten() {
	if (einzelinstanz::LaeuftBereits(g_programm)) {
		return true;
	}

	wchar_t systemordner[MAX_PATH];
	if (GetSystemDirectoryW(systemordner, MAX_PATH) == 0) {
		Melde(L"Das Systemverzeichnis konnte nicht ermittelt werden.");
		return false;
	}

	const std::wstring cmd = std::wstring(systemordner) + L"\\cmd.exe";
	const std::wstring batch = g_wurzel + kStartdatei;

	if (!PathFileExistsW(batch.c_str())) {
		Melde(L"Die Startdatei wurde nicht gefunden:\n\n" + batch);
		return false;
	}

	std::wstring befehl = L"\"" + cmd + L"\" /c \"\"" + batch + L"\"";
	ArgumenteAnhaengen(befehl);
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
		MeldeFehlschlag(L"Der Listenverschieber konnte nicht gestartet werden.");
		return false;
	}

	CloseHandle(vorgang.hThread);
	CloseHandle(vorgang.hProcess);
	return true;
}

HWND SchaltflaecheErstellen(HWND eltern, const wchar_t* beschriftung, int x,
							int y, int kennung, DWORD zusatz) {
	HWND schaltflaeche = CreateWindowExW(
		0, L"BUTTON", beschriftung,
		WS_CHILD | WS_VISIBLE | WS_TABSTOP | zusatz,
		x, y, kSchaltflaecheBreite, kSchaltflaecheHoehe, eltern,
		reinterpret_cast<HMENU>(static_cast<INT_PTR>(kennung)), nullptr,
		nullptr);
	SendMessageW(schaltflaeche, WM_SETFONT,
				 reinterpret_cast<WPARAM>(g_schrift), TRUE);
	return schaltflaeche;
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
			0, L"STATIC",
			L"Bitte waehlen, wie der Listenverschieber gestartet werden soll.",
			WS_CHILD | WS_VISIBLE,
			24, 24, kBreite - 60, 40, fenster, nullptr, nullptr, nullptr);
		SendMessageW(text, WM_SETFONT, reinterpret_cast<WPARAM>(g_schrift), TRUE);

		// Zwei Schaltflaechen nebeneinander, mittig ausgerichtet
		const int abstand = 16;
		const int gesamt = kSchaltflaecheBreite * 2 + abstand;
		const int links = (kBreite - 16 - gesamt) / 2;
		const int oben = 84;

		HWND direkt = SchaltflaecheErstellen(fenster, L"Direkt starten", links,
											 oben, kIdDirekt, BS_DEFPUSHBUTTON);
		SchaltflaecheErstellen(fenster, L"Ueber Startdatei starten",
							   links + kSchaltflaecheBreite + abstand, oben,
							   kIdStartdatei, BS_PUSHBUTTON);

		SetFocus(direkt);
		return 0;
	}

	case WM_COMMAND: {
		const int kennung = LOWORD(wParam);
		if (kennung != kIdDirekt && kennung != kIdStartdatei) {
			break;
		}

		// Waehrend des Starts keine weiteren Klicks annehmen
		EnableWindow(GetDlgItem(fenster, kIdDirekt), FALSE);
		EnableWindow(GetDlgItem(fenster, kIdStartdatei), FALSE);

		const bool erfolg = (kennung == kIdDirekt)
								? DirektStarten()
								: UeberStartdateiStarten();

		if (erfolg) {
			DestroyWindow(fenster);
		} else {
			EnableWindow(GetDlgItem(fenster, kIdDirekt), TRUE);
			EnableWindow(GetDlgItem(fenster, kIdStartdatei), TRUE);
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
	// Nur ein Startfenster je Sitzung - sonst stehen bei einem Doppelklick
	// zwei Auswahlfenster uebereinander.
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
	steuerelemente.dwICC = ICC_STANDARD_CLASSES;
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
		if (!IsDialogMessageW(fenster, &nachricht)) {
			TranslateMessage(&nachricht);
			DispatchMessageW(&nachricht);
		}
	}

	return static_cast<int>(nachricht.wParam);
}
