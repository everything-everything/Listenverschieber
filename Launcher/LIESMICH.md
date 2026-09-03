# Startdatei der Release-Pakete

In diesem Ordner liegt die Startdatei, die `build-release.ps1` in das
Hauptverzeichnis jedes Pakets kopiert.

## Aufbau der Pakete

    LV_Start.exe            Startprogramm mit Ladebalken, ruft die cmd auf
    Listenverschieber.cmd   startet die Anwendung
    Programm\               Anwendung und .NET-Laufzeit
    LICENSE.txt
    THIRD-PARTY-NOTICES.txt

Die Laufzeit liegt weiterhin unmittelbar neben der Anwendung, nur eine
Ebene tiefer. Sie laesst sich nicht von ihr trennen: der Host kuerzt die
Pfade aus der `deps.json` auf den reinen Dateinamen, und
`additionalProbingPaths` rechnet er gegen das Arbeitsverzeichnis statt
gegen den Programmordner.

## Warum eine Batch und kein Startprogramm

Zuerst war die Startdatei ein eigenes kleines Programm - erst in C# mit
NativeAOT, dann in C++. Beide Fassungen funktionierten technisch
einwandfrei, wurden aber von Virenscannern beanstandet:

- Microsoft Defender meldete `Trojan:Win32/Wacatac.C!ml` auf der
  Startdatei, waehrend die Anwendung selbst sauber blieb.
- Norton 360 meldete `IDP.HELU.PSD11` und verschob die **Anwendung** in
  Quarantaene - nicht die Startdatei.

Der Grund ist das Verhalten, nicht der Code: Eine kleine, unbekannte EXE,
deren einziger Zweck das Starten einer anderen EXE ist, entspricht dem
Muster eines Droppers. In einem Vergleichstest mit je fuenf Starts
schlugen ueber das Startprogramm drei Versuche fehl und die Anwendung
landete anschliessend in Quarantaene; ohne Startprogramm und ueber die
Batch liefen jeweils alle fuenf Versuche durch.

Bei einer Batch ist der startende Prozess `cmd.exe`, also eine von
Microsoft signierte Datei mit entsprechender Reputation. Damit entfaellt
das Muster.

## Was das Release verwendet

`build-release.ps1` legt ab 3.11c **nur** `Listenverschieber.cmd` ins Paket.

Grund: Microsoft Defender meldete zuletzt jede Fassung des Startprogramms
mit `Trojan:Win32/Wacatac.B!ml` - signiert wie unsigniert, mit Ladebalken
wie mit Schaltflaechen, und auch nach der Umbenennung auf `LV_Start.exe`.
Die Startdatei allein blieb dagegen durchweg unbeanstandet, weil dort
`cmd.exe` der startende Prozess ist und keine unbekannte kleine EXE
beurteilt werden muss.

Das Startprogramm bleibt im Verzeichnis erhalten und laesst sich weiter
bauen und messen:

    .\build-release.ps1 -MitStartprogramm

Dann kommt `LV_Start.exe` aus der Fassung `StartfensterUeberBatch` mit ins
Paket. Es startet die Anwendung **nicht selbst**, sondern ruft die
Startdatei auf; der Elternprozess bleibt also `cmd.exe`. Das genuegt den
Modellen derzeit trotzdem nicht.

## Kein Mehrfachstart

`Einzelinstanz.h` verhindert, dass die Anwendung mehrfach hochkommt.
Beobachtet wurde: das Programm war offen, und beim Schliessen ging es
scheinbar erneut auf. Tatsaechlich liefen zwei Anwendungen mit
deckungsgleichen Fenstern - die untere wurde erst beim Schliessen der
oberen sichtbar. Ursache war ein zweiter Klick waehrend des Ladebalkens.

Zwei Sperren greifen ineinander:

- Ein benannter Mutex laesst das Startprogramm nur einmal je Sitzung
  laufen. Das deckt den Doppelklick ab, bei dem die Anwendung noch gar
  nicht gestartet ist.
- Vor dem eigentlichen Start wird geprueft, ob die Programmdatei bereits
  laeuft. Wenn ja, wird nur ihr Fenster nach vorne geholt.

Die Pruefung vergleicht den **vollen Pfad**, nicht den Dateinamen: das
Startprogramm heisst selbst `Listenverschieber.exe` und wuerde sich sonst
selbst finden.

Die Anwendung selbst hat keine Einzelinstanz-Sperre. Wer bewusst zwei
Fenster braucht, startet die zweite ueber `Programm\Listenverschieber.exe`.

## Warum das Startprogramm nicht wie die Anwendung heisst

Bis zur Umstellung hiessen Startprogramm und Anwendung beide
`Listenverschieber.exe` - eines im Hauptverzeichnis, eines im Ordner
`Programm`. Eine EXE, die eine **gleichnamige** EXE aus einem Unterordner
startet, sieht allerdings nach einer Selbstkopie aus, wie sie
Schadsoftware zur Verankerung nutzt (Masquerading, MITRE ATT&CK T1036).

Deshalb heisst das Startprogramm jetzt `LV_Start.exe`. Die Angaben in
`Launcher.rc` wurden mitgezogen: `OriginalFilename` und `InternalName`
muessen zum Dateinamen passen, sonst waere die Abweichung fuer sich
genommen schon ein Verdachtsmerkmal. `FileDescription` benennt nun den
Zweck ("Listenverschieber - Startprogramm"), was auch im Task-Manager
verstaendlicher ist.

`build-launcher.ps1` uebersetzt das Startprogramm fuer x64, x86 und
arm64. NativeAOT schied als Grundlage aus, weil es `win-x86` nicht
unterstuetzt (Fehler NETSDK1203).

## Versuchsfassungen des Startprogramms

`build-launcher.ps1 -Fassung <Name>` waehlt aus:

    Launcher                      startet sofort und unsichtbar (Ursprungsfassung)
    Startfenster                  Fortschrittsbalken, danach direkter Start
    StartfensterUeberBatch        Fortschrittsbalken, danach Start ueber die Batch
    StartfensterMitSchaltflaechen startet erst auf Klick, wahlweise direkt oder ueber die Batch

Ausgeliefert wird `StartfensterUeberBatch`, **signiert**.

## Messungen auf VirusTotal

Zweiter Durchlauf, alle Fassungen mit Einzelinstanz-Pruefung:

| Fassung | Ergebnis |
| --- | --- |
| `StartfensterUeberBatch`, signiert | keine Meldung |
| `StartfensterUeberBatch`, unsigniert | `Trojan:Win32/Wacatac.B!ml` (Microsoft), `Malicious` (SecureAge) |
| `StartfensterMitSchaltflaechen`, signiert | `Trojan:Win32/Wacatac.B!ml` (Microsoft) |
| `StartfensterMitSchaltflaechen`, unsigniert | `Trojan:Win32/Wacatac.B!ml` (Microsoft), `Malicious` (SecureAge) |

Erster Durchlauf, noch ohne Einzelinstanz-Pruefung:

| Fassung | Ergebnis |
| --- | --- |
| `StartfensterUeberBatch`, unsigniert | keine Meldung |
| `StartfensterUeberBatch`, signiert | von Microsoft gemeldet |
| `StartfensterMitSchaltflaechen`, unsigniert | `Wacatac.B!ml`, `Malicious.moderate.ml.score` (Trapmine) |

### Was sich daraus ablesen laesst

Dritter Durchlauf, `StartfensterUeberBatch` signiert, je Architektur und
Paketart einzeln hochgeladen:

| Paket | Ergebnis |
| --- | --- |
| x64 standalone | keine Meldung |
| arm64 standalone | keine Meldung |
| x64 framework | `Trojan:Win32/Wacatac.B!ml` |
| x86 framework | `Trojan:Win32/Wacatac.B!ml` |
| x86 standalone | `Trojan:Win32/Wacatac.C!ml` |

Dieser Durchlauf ist der aufschlussreichste, denn die Startprogramme fuer
`x64-framework` und `x64-standalone` entstehen aus **derselben Quelle mit
demselben Compileraufruf**. Sie unterscheiden sich nur durch den
Signaturzeitstempel - und wurden trotzdem verschieden bewertet.

Die beiden frueheren Durchlaeufe widersprechen sich ausserdem bei der
Signatur: Dieselbe Fassung wurde einmal signiert und einmal unsigniert
beanstandet, und der signierte Button-Launcher galt zwischenzeitlich
sogar als unauffaellig. Der Grund: `!ml` steht fuer ein Urteil aus einem
Cloud-Modell, nicht fuer eine Signatur. Diese Modelle werden laufend
nachtrainiert - eine unveraenderte Datei kann heute sauber und morgen
auffaellig sein.

Einzelne Messungen taugen daher nicht als Beweis. Belastbar ist nur:

- Ein erheblicher Teil der Verdikte ist schlicht Rauschen. Praktisch
  identische Dateien werden unterschiedlich bewertet.
- Die Prozessaufzaehlung der Einzelinstanz-Pruefung hebt den Verdachtswert
  spuerbar an. Vorher war die unsignierte Batch-Fassung sauber, danach
  nicht mehr.
- Der direkte Start (`StartfensterMitSchaltflaechen`) wird durchgehend
  schlechter bewertet als der Start ueber die Batch - in jedem Durchlauf
  und in beiden Signaturvarianten.
- Die Framework-Pakete schneiden schlechter ab als die Standalone-Pakete.
  Sie enthalten nur 18 Dateien, fast alle unbekannt; bei den 270 Dateien
  der Standalone-Pakete faellt das Startprogramm weniger ins Gewicht.
- Betroffen ist immer nur die Startdatei, nie die Anwendung selbst.

**Vor jeder Veroeffentlichung neu messen.** Ein einmal sauberes Ergebnis
bleibt nicht zwangslaeufig sauber. Umgekehrt lohnt es nicht, wegen eines
einzelnen Treffers den Code umzubauen - erst ein Muster ueber mehrere
Durchlaeufe hinweg ist aussagekraeftig.

## Offener Punkt: der Button-Launcher

`StartfensterMitSchaltflaechen` bleibt als Versuchsfassung liegen, geht
aber nicht ins Paket. Der Klick vor dem Start hat die Bewertung nicht
verbessert - die ML-Modelle beurteilen ueberwiegend statische Merkmale:
kleine native EXE ohne Reputation, die `CreateProcessW` auf eine andere
EXE aufruft und Prozesse aufzaehlt.

Andere Hersteller haben dieses Problem nicht, weil sie an zwei Stellen
ansetzen, die uns bisher fehlen:

1. **Zertifikat einer oeffentlichen CA.** Das selbstsignierte Zertifikat
   aus `sign-release.ps1` hat keine vertrauenswuerdige Kette. Ein
   regulaeres Zertifikat kostet 200-600 Euro im Jahr, ein EV-Zertifikat
   bringt sofort SmartScreen-Reputation.
2. **Reputation ueber Verbreitung.** Microsoft bewertet unbekannte Dateien
   grundsaetzlich vorsichtiger. Mit steigender Verbreitung derselben
   signierten Datei sinkt der Verdachtswert von allein.

Zusaetzlich moeglich, falls die Button-Fassung spaeter doch gebraucht wird:

- Falsch-positiv bei Microsoft einreichen
  (`https://www.microsoft.com/en-us/wdsi/filesubmission`). Das ist kostenlos
  und wirkt fuer die konkrete Datei, muss aber nach jeder Neuuebersetzung
  wiederholt werden.
- Die Prozessaufzaehlung durch einen benannten Mutex ersetzen, den die
  Anwendung selbst setzt. Dann entfaellt `CreateToolhelp32Snapshot` - das
  Merkmal, das den Verdachtswert nachweislich angehoben hat. Das setzt
  allerdings eine Aenderung an der Anwendung voraus.

Eine Fassung, die nach kurzer Wartezeit von selbst startet, waere dagegen
ein Rueckschritt: das ist genau das zeitgesteuerte Verhalten aus
`Startfenster.cpp`, das Norton zur Quarantaene veranlasst hatte.

