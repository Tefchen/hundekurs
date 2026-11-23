# Hundekurs – Web-App

Grundgerüst der Kursverwaltung. Konzept und offene Fragen: `../Planung/KONZEPT.md`.

## Stand

| Projekt | Inhalt | Status |
|---|---|---|
| `src/Hundekurs.Planung` | Schweißplanung (Saisonplan, Revierrotation, Helfer, Fährten, Tausch, Regelprüfung), Wochenmail, Kurskalender mit Ostern, Zulassungsregel (60 %), Kilometergeld, Routendienst (openrouteservice) | fertig, getestet |
| `src/Hundekurs.Domain` | Entitäten der Datenbank (Personen, Hunde, Reviere, Kurs, Termine, Einsätze, Training, Prüfung, Abrechnung, Historie) | in Benutzung für Planung und Stammdaten |
| `src/Hundekurs.Daten` | EF Core mit SQLite: Datenbank, Laden/Speichern des Plans, Reviere, Teilnehmer, Ausbilder, Historie, tägliche Sicherung | neu |
| `src/Hundekurs.Web` | Blazor-Oberfläche: Übersicht, Schweißplan, Termin mit Tausch-Vorschau, Teilnehmer und Ausbilder, Wochenmail, Reviere, Historie, Druck | lauffähig, lokal |
| `tests/Hundekurs.Planung.Tests` | 50 Tests für Planung, Tausch, Wochenmail, Kalender, Zulassung, km und Routendienst | grün |
| `tests/Hundekurs.Daten.Tests` | 12 Tests: Speichern und Laden mit SQLite im Arbeitsspeicher | neu |

Noch nicht enthalten: Nachweise, Prüfungen, Kursabrechnung, Abschluss eines km-Abrechnungszeitraums.

## Starten

Voraussetzung: .NET SDK 10.

```powershell
cd App
dotnet test
dotnet run --project src/Hundekurs.Web                          # Demodaten
dotnet run --project src/Hundekurs.Web --launch-profile echt     # echte Daten
```

Dann im Browser `http://localhost:5155` öffnen. Die App ist nur auf dem eigenen Rechner erreichbar.

Mit Demodaten gibt es lokal automatisch das Konto **admin** mit dem Passwort **demo-passwort** (Rolle Verwaltung).

## Anmeldung und Rollen

- Jede Seite außer der Anmeldung verlangt ein Konto (ASP.NET Core Identity, Tabellen `AspNet…` in derselben SQLite-Datei).
- Jedes Konto hat genau eine Rolle:

  | Rolle | Darf |
  |---|---|
  | Lesen | Übersicht, Schweißplan, Termine und Gehorsam/Niederwild ansehen und drucken; keine Personen-, Gebühren-, Auslagen- oder km-Daten, keine Anwesenheitslisten |
  | Bearbeiten | alle Kursdaten pflegen |
  | Verwaltung | wie Bearbeiten, dazu Seite **Konten**: anlegen, Rolle ändern, Startpasswort vergeben, sperren |

- Neue Konten und vergessene Passwörter: Die Verwaltung vergibt ein Startpasswort (wird nur einmal angezeigt). Beim ersten Login muss ein eigenes Passwort mit mindestens 10 Zeichen gesetzt werden.
- Nach 5 Fehlversuchen ist ein Konto 15 Minuten gesperrt. Gesperrte Konten, neue Rollen und zurückgesetzte Passwörter wirken in offenen Fenstern nach spätestens 5 Minuten.
- Leserecht wird zusätzlich in der Datenebene durchgesetzt: `HundekursDb` lehnt Änderungen an Kursdaten ab, wenn der angemeldete Benutzer nur lesen darf. Der Name des angemeldeten Benutzers erscheint in der Historie und in `ErstelltVon`/`GeaendertVon`.
- **Erstes Konto mit echten Daten / auf dem Server:** Solange es kein Konto gibt, legt die App beim Start eines mit der Rolle Verwaltung an, wenn diese beiden Einstellungen gesetzt sind (Umgebungsvariablen, z. B. in `app.env`):
  ```
  Erstkonto__Benutzername=franziska
  Erstkonto__Passwort=<vorläufiges Passwort, mind. 10 Zeichen>
  ```
  Beim ersten Login muss das Passwort geändert werden. Danach beide Zeilen wieder entfernen.
  Lokal mit echten Daten (`--launch-profile echt`) gehen dafür User Secrets:
  ```powershell
  cd src/Hundekurs.Web
  dotnet user-secrets set "Erstkonto:Benutzername" "franziska"
  dotnet user-secrets set "Erstkonto:Passwort" "<vorläufiges Passwort>"
  ```
- Die Schlüssel für Anmelde-Cookies liegen im Unterordner `Schluessel` neben der Datenbank.

## Datenbank

- SQLite, eine einzelne Datei; es muss nichts installiert werden.
- Ablage außerhalb des Projektordners, damit echte Daten nie ins Repository geraten:
  - Demodaten: `%LOCALAPPDATA%\Hundekurs\hundekurs-demo.db`
  - Echte Daten: `%LOCALAPPDATA%\Hundekurs\hundekurs.db`
  - Anderer Ort: `Datenbank:Pfad` in `appsettings.json`
- Demodatenbank zurücksetzen: App beenden, `hundekurs-demo.db` löschen, neu starten.
- **Nach einer Modelländerung ohne Migrationen** (so wie jetzt noch) prüft die App beim Start, ob die Datei zum Modell passt. Die Demodatenbank wird dann automatisch neu angelegt (Hinweis im Protokoll); bei `hundekurs.db` bricht der Start mit einer Meldung ab, welche Spalten fehlen – dann die Datei löschen, solange sie keine echten Daten enthält.
- Sicherung: Beim Start legt die App höchstens einmal täglich eine Kopie im Unterordner `Sicherungen` an und behält die letzten 14.
- Schema: Solange es keine Migrationen gibt, legt die App die Tabellen beim ersten Start an. **Bevor echte Daten eingegeben werden**, sollte die erste Migration erzeugt werden, damit spätere Änderungen am Modell die Daten nicht verlieren:
  ```powershell
  dotnet tool install --global dotnet-ef --version 10.0.15
  dotnet ef migrations add Start --project src/Hundekurs.Daten --startup-project src/Hundekurs.Web
  ```
  Ab dann wendet die App neue Migrationen beim Start automatisch an. (Eine bereits ohne Migrationen angelegte Datenbank vorher löschen.)
- **Nach jeder Modelländerung** eine neue Migration mit sprechendem Namen anlegen und den Ordner `src/Hundekurs.Daten/Migrations` mit einchecken, z. B.:
  ```powershell
  dotnet ef migrations add Stammdaten --project src/Hundekurs.Daten --startup-project src/Hundekurs.Web
  ```
  Vor dem ersten Start mit echten Daten lohnt ein Blick in die erzeugte Datei: Steht dort `DropColumn` für eine Spalte, deren Inhalt gebraucht wird, bitte melden.

## Kursjahr, Teil 1 und Niederwild

- **Einstellungen → Kursjahr anlegen** erzeugt alle Termine: Teil 1 (Grundgehorsam) ab dem 1. Märzsonntag über 12 Wochen ohne Ostersonntag (11 Sonntage), Teil 2 direkt im Anschluss (12 Wochenenden, Feiertage werden durchtrainiert), Nachsuche auf Niederwild an 12 Mittwochen ab 17:30 (ab dem Mittwoch vor dem ersten Schweißsonntag).
- **Gehorsam** und **Niederwild**: Anwesenheitsliste je Termin (zum Ankreuzen, auch druckbar), anwesende Ausbilder, Notiz; Termine lassen sich absagen.
- **Ausbildung** je Gespann mit drei Haken: Grundgehorsam, Nachsuche auf Schalenwild, Nachsuche auf Niederwild. Zivil = nur Grundgehorsam, jagdlich = mindestens eine Nachsuche (dann ist Grundgehorsam Pflicht). Schweißgruppe nur mit Schalenwild.
- **Zulassung zu Teil 2** (nur jagdlich): mindestens 60 % der stattgefundenen Gehorsams-Sonntage (aufgerundet; 11 → 7, 10 → 6). Ampel je Gespann, Anwärterliste nach Anwesenheit, Ausnahme nur mit Begründung (steht in der Historie). Befreit ist, wer nur Niederwild macht und dessen Hund die Brauchbarkeit schon bestanden hat.

## Personen, Hunde, Orte und Gebühren

- **Personen**: Anrede (Herr/Frau/divers), Titel, Hundeführer, Eigentümer, Ausbilder und Prüfer an einer Stelle; Doppelrollen sind möglich. Vereinsmitglied als Haken.
- **Hunde**: Rasse, Chip, Zuchtbuchnummer, Führer, Eigentümer und bestandene Brauchbarkeit (Datum, Ort, Fächer). Abmelden aus dem Kurs löscht weder Person noch Hund.
- **Orte** (Einstellungen): ein Ort kann in einem Revier liegen und für mehrere Fächer genutzt werden; die Fächerliste ist pflegbar.
- **Auslagen**: Verpflegung (z. B. zur Prüfung), Anschaffungen (z. B. Westen) und Sonstiges, mit Anlass, Beleg-Haken und Erstattungsvermerk; Druck als Übersicht oder als Erstattung je Person mit Unterschriftszeile. Ohne Person = direkt aus der Vereinskasse bezahlt.
- **Gebühren**: Preis und Mitgliederpreis je Kombination der Teile; Wiederholer zahlen die Hälfte (erst Mitgliederpreis, dann halbieren); Betrag von Hand mit Grund; Haken „bezahlt“ mit Datum. Bestehende Kurse bekommen die drei bisherigen Pakete mit 0 € – Preise bitte eintragen.

## km-Abrechnung

- Einsätze entstehen über die Anwesenheitslisten (Gehorsam, Niederwild) und auf der Terminseite von Teil 2 (Vorbereitung samstags, Training sonntags).
- Vergütet werden Hin- und Rückweg je Einsatztag mit dem km-Satz aus den Einstellungen (Standard 0,30 €). Doppelrollen am selben Tag (z. B. Ausbilder und Prüfer) zählen als eine Fahrt.
- Die einfache Strecke berechnet der Routendienst von der Wohnadresse des Ausbilders zum Ort (Koordinaten von Revier bzw. Übungsort). Strecken werden höchstens 7 Tage zwischengespeichert, danach neu abgefragt. Eine Korrektur von Hand (z. B. Umleitung) hat Vorrang.
- Seite **km**: Zeitraum wählen, fehlende km berechnen, korrigieren, je Ausbilder drucken (eine Seite pro Person mit Unterschriftszeile).

### Routendienst einrichten

Kostenloser Schlüssel von openrouteservice (HeiGIT). Einmalig im Webprojekt als „User Secret“ hinterlegen – er liegt dann im Windows-Profil, nicht im Repository:

```powershell
cd src/Hundekurs.Web
dotnet user-secrets set "Routendienst:Schluessel" "<schlüssel>"
```

An den Dienst gehen nur Adressen (einmalig, zur Umrechnung in Koordinaten) und Koordinaten – keine Namen. Ohne Schlüssel funktioniert alles außer der automatischen Berechnung.

## Regeln der Schweißplanung

Hart (werden nie verletzt, sonst roter Befund):
- Ein Hund läuft jede Fährte (Revier + Nummer) höchstens einmal. Abbruch zählt als gelaufen, „nicht angetreten“ nicht.
- Pro Termin höchstens eine Fährte doppelt; Kapazität je Schweißblock = nutzbare Fährten + 1 (Standard 7).
- Parallel laufen nur die Paare des Reviers (Standard 1+4, 2+5, 3+6, je Revier änderbar).
- Mindestens 14 Tage Ruhe zwischen zwei Nutzungen eines Reviers; Prüfungsreviere und inaktive Reviere werden nicht eingeplant.
- Zwei Helfer je Termin aus der Gruppe, die an dem Tag nicht auf Schweiß ist. Sie legen am Vorabend die Fährten und gehen als Beobachter mit. Helferdienste lassen sich nur innerhalb dieser Gruppe übergeben oder tauschen.
- Abgemeldete laufen nicht; gesperrte Fährten werden nicht genutzt.
- Nachholen geht nur, solange der Schweißblock nicht voll ist (bei 7 Hunden also nur, wenn sich jemand abgemeldet hat).
- Termintausch zwischen zwei Gespannen, auch über die Gruppen hinweg: Beide übernehmen nach Möglichkeit die Fährte des anderen, für alle übrigen ändert sich dann nichts. Abgelehnt, wenn einer am Zieltermin Helferdienst hat.
- Ein Helferdienst an einem Tag, an dem die eigene Gruppe auf Schweiß ist, ist nicht erlaubt.

Weich (gelber Befund bzw. Optimierungsziel):
- 21 Tage Ruhe je Revier, jede Gruppe kommt gleichmäßig in alle Reviere.
- Helferdienste gleichmäßig verteilt, doppelte Fährte reihum.
- Bei Änderungen ändert sich ein bestehender Plan so wenig wie möglich.

Ablauf einer Änderung: Auf der Terminseite eine Aktion wählen → „Prüfen“ zeigt die Vorher/Nachher-Liste und Befunde → „Übernehmen“ oder „Verwerfen“. Jede übernommene Änderung steht in der Historie.

## Wochenmail

Seite „Wochenmail“: Plan für den nächsten Trainingstag samt Vorbereitungsabend, mit eigenem Einleitungs- und Grußtext. Alle Empfänger stehen im BCC.
Versandwege: „Im Mailprogramm öffnen“ (mailto-Link), „Als Entwurf herunterladen (.eml)“ (Outlook öffnet ihn als bearbeitbaren Entwurf) oder Text und Empfänger kopieren.
Die App verschickt selbst noch keine Mails; dafür bräuchte sie ein Absenderkonto (SMTP).

## Aufbau des Planungskerns

- `Schweissplaner.NeuPlanen` arbeitet die Termine chronologisch ab. Durchgeführte und fixierte Teile bleiben, alles andere wird mit Stabilitätskosten neu berechnet.
- Die Fährtenzuteilung je Termin ist eine exakte Optimierung (Tiefensuche mit Schranke, höchstens 7 Hunde × 7 Möglichkeiten).
- `Tauschdienst` liefert für jede Änderung einen `Aenderungsvorschlag`, verändert den bestehenden Plan aber nie selbst.
- `PlanPruefer` prüft einen beliebigen Plan gegen alle Regeln.

## Nächste Schritte

1. Migration „Auslagen“ erzeugen (siehe oben), dann mit echten Daten testen
2. km-Abrechnungszeitraum abschließen (danach gesperrt)
3. Nachweise je Gespann, Prüfungen samt Kostenerstattung Prüfer, Druckliste Prüfung
4. Login und Rollen, sobald die App gehostet wird
