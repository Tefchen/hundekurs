using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hundekurs.Daten;

/// <summary>Ablageort, Einrichtung und Sicherung der SQLite-Datenbank.</summary>
public static class Datenbank
{
    /// <summary>
    /// Standardpfad außerhalb des Projektordners, damit echte Daten nie ins Repository geraten.
    /// Windows: %LOCALAPPDATA%\Hundekurs\hundekurs.db (Demodaten: hundekurs-demo.db)
    /// </summary>
    public static string StandardPfad(bool demodaten = false) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hundekurs",
            demodaten ? "hundekurs-demo.db" : "hundekurs.db");

    public static string Verbindung(string pfad) => new SqliteConnectionStringBuilder { DataSource = pfad, ForeignKeys = true }.ToString();

    /// <summary>
    /// Legt die Datenbank an bzw. bringt sie auf den aktuellen Stand.
    /// Ohne Migrationen kann EnsureCreated eine bestehende Datei nicht anpassen. Passt das Schema nicht zum Programm,
    /// wird eine Demodatenbank neu angelegt; bei echten Daten bricht der Start mit einer verständlichen Meldung ab.
    /// </summary>
    /// <returns>Hinweis für das Protokoll, falls die Demodatenbank neu angelegt wurde, sonst null.</returns>
    public static string? Vorbereiten(HundekursDb db, bool demodaten = false)
    {
        // Sobald es Migrationen gibt (dotnet ef migrations add …), werden sie angewendet.
        if (db.Database.GetMigrations().Any())
        {
            // Eine Datei, die noch ohne Migrationen angelegt wurde, hat Tabellen, aber keine Migrationshistorie.
            // Migrate würde dann mit „table already exists“ scheitern.
            string? hinweis = null;
            if (!db.Database.GetAppliedMigrations().Any() && HatTabellen(db))
            {
                if (!demodaten)
                    throw new InvalidOperationException(
                        "Die Datenbank wurde noch ohne Migrationen angelegt und kann deshalb nicht übernommen werden. " +
                        "Enthält sie noch keine echten Daten, die Datei löschen und neu starten. " +
                        "Eine Sicherung liegt im Unterordner „Sicherungen“.");
                db.Database.EnsureDeleted();
                hinweis = "Demodatenbank stammte aus der Zeit vor den Migrationen und wurde neu angelegt.";
            }
            db.Database.Migrate();
            return hinweis;
        }

        db.Database.EnsureCreated();
        var fehlt = FehlendeSpalten(db);
        if (fehlt.Count == 0) return null;

        var liste = string.Join(", ", fehlt.Take(5)) + (fehlt.Count > 5 ? $" und {fehlt.Count - 5} weitere" : "");
        if (demodaten)
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            return $"Demodatenbank passte nicht zum Programmstand (fehlte: {liste}) und wurde neu angelegt.";
        }

        throw new InvalidOperationException(
            $"Die Datenbank passt nicht zum Programmstand (fehlt: {liste}). " +
            "Es gibt noch keine Migrationen, deshalb kann sie nicht automatisch angepasst werden. " +
            "Enthält sie noch keine echten Daten, die Datei löschen und neu starten; sonst zuerst eine Migration anlegen (siehe README). " +
            "Eine Sicherung liegt im Unterordner „Sicherungen“.");
    }

    /// <summary>Enthält die Datei bereits Tabellen (außer SQLite-internen und der Migrationshistorie)?</summary>
    private static bool HatTabellen(HundekursDb db)
    {
        var verbindung = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory'";
            return Convert.ToInt64(befehl.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    /// <summary>Vergleicht Tabellen und Spalten des Modells mit der Datei („Tabelle.Spalte“ für jede fehlende Spalte, „Tabelle“ für fehlende Tabellen).</summary>
    public static IReadOnlyList<string> FehlendeSpalten(HundekursDb db)
    {
        var fehlt = new List<string>();
        var verbindung = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            foreach (var typ in db.Model.GetEntityTypes())
            {
                var tabelle = typ.GetTableName();
                if (tabelle is null) continue;

                var vorhanden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var befehl = verbindung.CreateCommand())
                {
                    befehl.CommandText = "SELECT name FROM pragma_table_info($tabelle)";
                    var parameter = befehl.CreateParameter();
                    parameter.ParameterName = "$tabelle";
                    parameter.Value = tabelle;
                    befehl.Parameters.Add(parameter);
                    using var leser = befehl.ExecuteReader();
                    while (leser.Read()) vorhanden.Add(leser.GetString(0));
                }

                if (vorhanden.Count == 0)
                {
                    fehlt.Add(tabelle);
                    continue;
                }

                var objekt = StoreObjectIdentifier.Table(tabelle, typ.GetSchema());
                foreach (var eigenschaft in typ.GetProperties())
                {
                    var spalte = eigenschaft.GetColumnName(objekt);
                    if (spalte is not null && !vorhanden.Contains(spalte)) fehlt.Add($"{tabelle}.{spalte}");
                }
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
        return fehlt.Distinct().ToList();
    }

    /// <summary>
    /// Legt höchstens einmal pro Tag eine Sicherung im Unterordner „Sicherungen“ an und behält die neuesten.
    /// Nutzt die Backup-Funktion von SQLite, die auch bei laufender App eine konsistente Kopie erzeugt.
    /// </summary>
    public static string? Sichern(string pfad, int behalten = 14)
    {
        if (!File.Exists(pfad)) return null;

        var ordner = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pfad))!, "Sicherungen");
        Directory.CreateDirectory(ordner);

        var name = Path.GetFileNameWithoutExtension(pfad);
        var heute = DateTime.Now.ToString("yyyy-MM-dd");
        if (Directory.EnumerateFiles(ordner, $"{name}_{heute}*.db").Any()) return null;

        var ziel = Path.Combine(ordner, $"{name}_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
        using (var quelle = new SqliteConnection(Verbindung(pfad)))
        using (var kopie = new SqliteConnection(Verbindung(ziel)))
        {
            quelle.Open();
            kopie.Open();
            quelle.BackupDatabase(kopie);
            SqliteConnection.ClearPool(kopie);
        }

        foreach (var alt in Directory.EnumerateFiles(ordner, $"{name}_*.db").OrderByDescending(f => f).Skip(behalten))
        {
            try { File.Delete(alt); } catch (IOException) { /* beim nächsten Mal */ }
        }
        return ziel;
    }
}
