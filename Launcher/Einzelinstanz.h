// Verhindert, dass der Listenverschieber mehrfach gestartet wird.
//
// Der Launcher zeigt rund eine Sekunde lang einen Ladebalken. Wer in
// dieser Zeit erneut klickt - oder im Explorer doppelt klickt, waehrend
// noch nichts zu sehen ist - bekam bisher eine zweite Anwendung. Beide
// Fenster erscheinen an derselben Stelle, deshalb faellt das erst beim
// Schliessen des oberen auf: "das Programm geht beim Schliessen wieder
// auf".
//
// Diese Datei wird von den Launcher-Fassungen per #include eingebunden.
// Ein eigenes Uebersetzungsmodul waere hier unnoetig: build-launcher.ps1
// uebersetzt bewusst genau eine .cpp-Datei.

#pragma once

#include <windows.h>
#include <tlhelp32.h>
#include <string>

namespace einzelinstanz {

namespace intern {

struct Suche {
	DWORD kennung;
	HWND treffer;
};

BOOL CALLBACK FensterPruefen(HWND fenster, LPARAM daten) {
	Suche* suche = reinterpret_cast<Suche*>(daten);

	DWORD kennung = 0;
	GetWindowThreadProcessId(fenster, &kennung);

	// Nur sichtbare Hauptfenster, keine Werkzeug- oder Hilfsfenster
	if (kennung != suche->kennung || !IsWindowVisible(fenster) ||
		GetWindow(fenster, GW_OWNER) != nullptr) {
		return TRUE;
	}

	suche->treffer = fenster;
	return FALSE;
}

// Holt das Fenster des laufenden Programms nach vorne.
//
// SetForegroundWindow verweigert den Dienst, wenn der eigene Prozess
// nicht im Vordergrund ist. Der Launcher ist es beim Klick aber gerade,
// deshalb genuegt der einfache Aufruf.
void FensterNachVorne(DWORD kennung) {
	Suche suche = { kennung, nullptr };
	EnumWindows(FensterPruefen, reinterpret_cast<LPARAM>(&suche));

	if (suche.treffer == nullptr) {
		return;
	}

	if (IsIconic(suche.treffer)) {
		ShowWindow(suche.treffer, SW_RESTORE);
	}
	SetForegroundWindow(suche.treffer);
}

// Vergleicht zwei Pfade ohne Ruecksicht auf Gross- und Kleinschreibung.
bool PfadeGleich(const std::wstring& links, const std::wstring& rechts) {
	return CompareStringOrdinal(links.c_str(), -1, rechts.c_str(), -1, TRUE) ==
		   CSTR_EQUAL;
}

}  // namespace intern

// Prueft, ob genau diese Programmdatei bereits laeuft.
//
// Der Vergleich laeuft ueber den vollen Pfad, nicht ueber den Dateinamen.
// Das ist robuster: Ein Prozess gleichen Namens aus einem anderen Ordner -
// etwa eine zweite Kopie des Pakets - gilt nicht als dieselbe Anwendung.
//
// Laeuft die Anwendung bereits, wird ihr Fenster nach vorne geholt und
// true zurueckgegeben - der Aufrufer startet dann nichts.
bool LaeuftBereits(const std::wstring& programm) {
	HANDLE abbild = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
	if (abbild == INVALID_HANDLE_VALUE) {
		// Ohne Auskunft lieber starten als den Start verweigern
		return false;
	}

	const DWORD eigene = GetCurrentProcessId();
	bool gefunden = false;

	PROCESSENTRY32W eintrag = {};
	eintrag.dwSize = sizeof(eintrag);

	if (Process32FirstW(abbild, &eintrag)) {
		do {
			if (eintrag.th32ProcessID == eigene) {
				continue;
			}

			HANDLE vorgang = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,
										 FALSE, eintrag.th32ProcessID);
			if (vorgang == nullptr) {
				continue;
			}

			wchar_t pfad[MAX_PATH];
			DWORD laenge = MAX_PATH;
			if (QueryFullProcessImageNameW(vorgang, 0, pfad, &laenge) &&
				intern::PfadeGleich(std::wstring(pfad, laenge), programm)) {
				gefunden = true;
			}

			CloseHandle(vorgang);

			if (gefunden) {
				intern::FensterNachVorne(eintrag.th32ProcessID);
				break;
			}
		} while (Process32NextW(abbild, &eintrag));
	}

	CloseHandle(abbild);
	return gefunden;
}

}  // namespace einzelinstanz
