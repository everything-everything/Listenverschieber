# Anleitung: Explorer-Öffnen Funktion hinzufügen

## ? Was wurde bereits erledigt:

1. **Copyright-Jahr auf 2025 geändert** ?
2. **ExplorerHelper.cs erstellt** - Hilfsklasse für Explorer-Funktionen ?
3. **LogEintrag.cs erstellt** - Klasse für strukturierte Log-Einträge ?
4. **ExplorerMethoden_ZumEinfuegen.txt** - Code für MainWindow.xaml.cs ?

## ?? Was Sie noch tun müssen:

### Schritt 1: XAML anpassen (MainWindow.xaml)

#### A) Bei Tab "Unvollständige Dateien" (ca. Zeile 190-210):

Suchen Sie nach:
```xaml
<GroupBox Header="Status" Grid.Row="7">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

Ändern Sie zu:
```xaml
<GroupBox Header="Status" Grid.Row="7">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>  <!-- NEU -->
        </Grid.RowDefinitions>
```

Dann suchen Sie nach:
```xaml
<!-- Export Button -->
<Button Grid.Row="1" x:Name="btnExportTab2" Content="?? Exportieren..." Height="30" Margin="5" Click="MenuExport_Click" 
        ToolTip="Verschobene oder nicht gefundene Dateien exportieren"/>
```

Fügen Sie DANACH hinzu:
```xaml
<!-- Explorer Button -->
<Button Grid.Row="2" x:Name="btnOpenLogInExplorer2" Content="?? Datei im Explorer öffnen" Height="30" Margin="5" Click="btnOpenLogInExplorer_Click" 
        ToolTip="Text im Log markieren und hier klicken - öffnet die Datei im Windows Explorer"/>
```

#### B) Bei Tab "Listenverschieber" (ca. Zeile 325-345):

Suchen Sie nach:
```xaml
<GroupBox Header="Status" Grid.Row="6">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

Ändern Sie zu:
```xaml
<GroupBox Header="Status" Grid.Row="6">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>  <!-- NEU -->
        </Grid.RowDefinitions>
```

Dann suchen Sie nach:
```xaml
<!-- Export Button -->
<Button Grid.Row="1" x:Name="btnExportTab1" Content="?? Exportieren..." Height="30" Margin="5" Click="MenuExport_Click" 
        ToolTip="Verschobene oder nicht gefundene Dateien exportieren"/>
```

Fügen Sie DANACH hinzu:
```xaml
<!-- Explorer Button -->
<Button Grid.Row="2" x:Name="btnOpenLogInExplorer1" Content="?? Datei im Explorer öffnen" Height="30" Margin="5" Click="btnOpenLogInExplorer_Click" 
        ToolTip="Text im Log markieren und hier klicken - öffnet die Datei im Windows Explorer"/>
```

### Schritt 2: Code in MainWindow.xaml.cs einfügen

1. Öffnen Sie `MainWindow.xaml.cs`
2. Scrollen Sie zum Ende der Datei
3. Suchen Sie die letzte Zeile vor den schließenden Klammern:
   ```csharp
   private void MenuInfo_Click(object sender, RoutedEventArgs e) => System.Windows.MessageBox.Show(...

           #endregion
       }
   }
   ```

4. Fügen Sie den KOMPLETTEN Code aus `ExplorerMethoden_ZumEinfuegen.txt` NACH dem `#endregion` (Menu Handlers) und VOR der letzten `}}` ein.

### Schritt 3: Projekt neu bauen

1. Drücken Sie `Strg+Shift+B` um das Projekt zu bauen
2. Wenn Fehler auftreten, prüfen Sie ob alle using-Direktiven vorhanden sind

## ?? So funktioniert es:

1. **Führen Sie eine Dateiverarbeitung aus** (Verschieben/Kopieren/Suchlauf)
2. **Im Log werden Dateien angezeigt** (z.B. "Verschoben: Rechnung.pdf")
3. **Markieren Sie die Zeile mit der Maus** im Log-Textfeld
4. **Klicken Sie auf "?? Datei im Explorer öffnen"**
5. **Die Datei wird im Windows Explorer geöffnet und markiert**

## ?? Hinweise:

- Die Funktion erkennt automatisch Dateinamen in verschiedenen Log-Formaten
- Es werden mehrere Pfade durchsucht (Arbeits-, Verschiebe-, Move-Pfade)
- Falls die Datei nicht gefunden wird, wird der Ordner geöffnet
- Funktioniert in beiden Tabs (Listenverschieber und Unvollständige Dateien)

## ?? Troubleshooting:

**Fehler "Visibility" kann nicht gefunden werden:**
- Stellen Sie sicher, dass `using System.Windows;` am Anfang der Datei steht

**Button erscheint nicht:**
- Prüfen Sie, ob Grid.RowDefinitions korrekt erweitert wurde (3 statt 2 Rows)
- Prüfen Sie die Grid.Row Nummern (sollten 0, 1, 2 sein)

**Datei wird nicht gefunden:**
- Markieren Sie die KOMPLETTE Zeile im Log (inkl. Zeitstempel)
- Die Datei muss im Arbeits-, Verschiebe- oder Move-Ordner liegen
