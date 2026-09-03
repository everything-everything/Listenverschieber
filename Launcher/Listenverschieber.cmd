@echo off
rem Startet den Listenverschieber aus dem Unterordner "Programm".
rem
rem Im Hauptverzeichnis liegt nur diese Datei, damit vorne nicht die
rem gesamte .NET-Laufzeit sichtbar ist. Die Laufzeit selbst bleibt
rem unangetastet neben der Anwendung liegen - nur eine Ebene tiefer.
rem
rem Bewusst eine Batch und kein eigenes Startprogramm: eine kleine,
rem unbekannte EXE, die eine andere EXE startet, entspricht dem Muster
rem von Schadsoftware. Virenscanner haben daraufhin die Anwendung in
rem Quarantaene verschoben. Bei einer Batch ist der startende Prozess
rem dagegen cmd.exe - eine von Microsoft signierte Datei.
rem
rem %~dp0 ist der Ordner dieser Datei, unabhaengig davon, von wo aus
rem sie aufgerufen wird. Nur so klappt der Start auch ueber eine
rem Verknuepfung oder aus einem fremden Verzeichnis heraus.

cd /d "%~dp0Programm"

if not exist "Listenverschieber.exe" (
    echo Die Programmdatei wurde nicht gefunden.
    echo.
    echo Bitte das Paket vollstaendig entpacken - der Ordner "Programm"
    echo muss neben dieser Datei liegen.
    echo.
    pause
    exit /b 2
)

rem "start" uebergibt an Windows und kehrt sofort zurueck, damit sich
rem das Konsolenfenster schliesst. Der erste leere Parameter ist der
rem Fenstertitel; ohne ihn wuerde der Pfad als Titel gedeutet.
rem %* reicht Aufrufargumente weiter, etwa bei Drag and Drop.
start "" "Listenverschieber.exe" %*
