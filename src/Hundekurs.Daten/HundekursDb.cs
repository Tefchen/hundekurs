using Hundekurs.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten;

/// <summary>Kursdaten und Anmeldekonten (Identity-Tabellen „AspNet…“) in einer SQLite-Datei.</summary>
public class HundekursDb(DbContextOptions<HundekursDb> options) : IdentityDbContext<Benutzerkonto>(options)
{
    // Stammdaten
    public DbSet<Person> Personen => Set<Person>();
    public DbSet<Rasse> Rassen => Set<Rasse>();
    public DbSet<Hund> Hunde => Set<Hund>();
    public DbSet<Brauchbarkeit> Brauchbarkeiten => Set<Brauchbarkeit>();
    public DbSet<Fach> Faecher => Set<Fach>();
    public DbSet<Revier> Reviere => Set<Revier>();
    public DbSet<Faehrte> Faehrten => Set<Faehrte>();
    public DbSet<Sequenzpaar> Sequenzpaare => Set<Sequenzpaar>();
    public DbSet<Uebungsgelaende> Uebungsgelaende => Set<Uebungsgelaende>();
    public DbSet<GelaendeFach> GelaendeFaecher => Set<GelaendeFach>();
    public DbSet<Faehrtenauffrischung> Faehrtenauffrischungen => Set<Faehrtenauffrischung>();

    // Kurs
    public DbSet<Kurs> Kurse => Set<Kurs>();
    public DbSet<Kursrolle> Kursrollen => Set<Kursrolle>();
    public DbSet<Kursteilnahme> Kursteilnahmen => Set<Kursteilnahme>();
    public DbSet<Nachweis> Nachweise => Set<Nachweis>();
    public DbSet<Gebuehrenpaket> Gebuehrenpakete => Set<Gebuehrenpaket>();
    public DbSet<Auslage> Auslagen => Set<Auslage>();

    // Termine und Schweiß
    public DbSet<Termin> Termine => Set<Termin>();
    public DbSet<Vorbereitungstermin> Vorbereitungstermine => Set<Vorbereitungstermin>();
    public DbSet<Helferdienst> Helferdienste => Set<Helferdienst>();
    public DbSet<Terminabweichung> Terminabweichungen => Set<Terminabweichung>();
    public DbSet<Faehrtenzuteilung> Faehrtenzuteilungen => Set<Faehrtenzuteilung>();
    public DbSet<Faehrtenlauf> Faehrtenlaeufe => Set<Faehrtenlauf>();
    public DbSet<Tauschvorgang> Tauschvorgaenge => Set<Tauschvorgang>();
    public DbSet<Einsatz> Einsaetze => Set<Einsatz>();
    public DbSet<Standardstrecke> Standardstrecken => Set<Standardstrecke>();
    public DbSet<Verfuegbarkeit> Verfuegbarkeiten => Set<Verfuegbarkeit>();
    public DbSet<Anwesenheit> Anwesenheiten => Set<Anwesenheit>();

    // Training, Prüfung, Abrechnung
    public DbSet<Merkmal> Merkmale => Set<Merkmal>();
    public DbSet<Beobachtung> Beobachtungen => Set<Beobachtung>();
    public DbSet<Pruefungstag> Pruefungstage => Set<Pruefungstag>();
    public DbSet<Nennung> Nennungen => Set<Nennung>();
    public DbSet<Pruefungsergebnis> Pruefungsergebnisse => Set<Pruefungsergebnis>();
    public DbSet<Einsatzabrechnung> Einsatzabrechnungen => Set<Einsatzabrechnung>();

    // Historie
    public DbSet<Aenderungsprotokoll> Aenderungsprotokoll => Set<Aenderungsprotokoll>();

    /// <summary>Name für ErstelltVon/GeaendertVon und die Historie: der angemeldete Benutzer, sonst der Betriebssystem-Benutzer.</summary>
    public string Benutzer { get; set; } = Benutzerkontext.Aktuell?.Name ?? Environment.UserName;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        SchreibrechtPruefen();
        Stempeln();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SchreibrechtPruefen();
        Stempeln();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Serverseitige Sperre: Konten mit reinem Leserecht dürfen keine Kursdaten ändern, auch wenn die Oberfläche einmal etwas anbietet.
    /// Anmeldedaten (eigenes Passwort, Fehlversuche) sind ausgenommen.
    /// </summary>
    private void SchreibrechtPruefen()
    {
        if (Benutzerkontext.Aktuell is not { DarfBearbeiten: false } benutzer) return;
        var kursdaten = typeof(Entitaet).Assembly;
        var betroffen = ChangeTracker.Entries().Any(e =>
            (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            e.Entity.GetType().Assembly == kursdaten);
        if (betroffen)
            throw new InvalidOperationException($"{benutzer.Name} hat nur Leserecht und kann keine Kursdaten ändern.");
    }

    private void Stempeln()
    {
        var jetzt = DateTimeOffset.Now;
        foreach (var e in ChangeTracker.Entries<Entitaet>())
        {
            if (e.State == EntityState.Added)
            {
                e.Entity.ErstelltAm = jetzt;
                e.Entity.ErstelltVon = Benutzer;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.GeaendertAm = jetzt;
                e.Entity.GeaendertVon = Benutzer;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Identity-Tabellen (Konten, Rollen, Anmeldungen)
        base.OnModelCreating(mb);
        mb.Entity<Benutzerkonto>().Property(b => b.Anzeigename).HasMaxLength(100);

        // Personen und Hunde: Löschen einer Person darf keine Hunde mitreißen
        mb.Entity<Hund>().HasOne(h => h.Fuehrer).WithMany().HasForeignKey(h => h.FuehrerId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Hund>().HasOne(h => h.Eigentuemer).WithMany().HasForeignKey(h => h.EigentuemerId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Hund>().HasMany(h => h.Brauchbarkeiten).WithOne().HasForeignKey(b => b.HundId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Rasse>().HasIndex(r => r.Bezeichnung).IsUnique();
        mb.Entity<Fach>().HasIndex(f => f.Bezeichnung).IsUnique();
        mb.Entity<Uebungsgelaende>().HasMany(g => g.Faecher).WithOne().HasForeignKey(x => x.UebungsgelaendeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<GelaendeFach>().HasOne<Fach>().WithMany().HasForeignKey(x => x.FachId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<GelaendeFach>().HasIndex(x => new { x.UebungsgelaendeId, x.FachId }).IsUnique();
        mb.Entity<Hund>().HasOne(h => h.Rasse).WithMany().HasForeignKey(h => h.RasseId).OnDelete(DeleteBehavior.SetNull);

        // Reviere
        mb.Entity<Revier>().HasIndex(r => r.Kuerzel).IsUnique();
        mb.Entity<Revier>().HasMany(r => r.Faehrten).WithOne(f => f.Revier).HasForeignKey(f => f.RevierId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Faehrte>().HasIndex(f => new { f.RevierId, f.Nummer }).IsUnique();
        mb.Entity<Revier>().HasMany(r => r.Sequenzpaare).WithOne().HasForeignKey(p => p.RevierId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Revier>().HasMany(r => r.Uebungsgelaende).WithOne().HasForeignKey(u => u.RevierId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Faehrtenauffrischung>().HasOne(a => a.Revier).WithMany().HasForeignKey(a => a.RevierId).OnDelete(DeleteBehavior.Cascade);

        // Kurs
        mb.Entity<Kurs>().HasMany(k => k.Teilnahmen).WithOne(t => t.Kurs).HasForeignKey(t => t.KursId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Kurs>().HasMany(k => k.Rollen).WithOne().HasForeignKey(r => r.KursId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Kursrolle>().HasOne(r => r.Person).WithMany().HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Kursteilnahme>().HasOne(t => t.Hund).WithMany().HasForeignKey(t => t.HundId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Kursteilnahme>().HasOne(t => t.Fuehrer).WithMany().HasForeignKey(t => t.FuehrerId).OnDelete(DeleteBehavior.Restrict);
        // Die Ausbildungsteile hießen in der ersten Fassung „…Gebucht“; die Spalten behalten ihren Namen, damit keine Daten verloren gehen.
        mb.Entity<Kursteilnahme>().Property(t => t.Gehorsam).HasColumnName("GehorsamGebucht");
        mb.Entity<Kursteilnahme>().Property(t => t.NachsucheSchalenwild).HasColumnName("JagdlichGebucht");
        mb.Entity<Kursteilnahme>().Property(t => t.NachsucheNiederwild).HasColumnName("ApportierGebucht");
        mb.Entity<Auslage>().HasOne<Kurs>().WithMany().HasForeignKey(a => a.KursId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Auslage>().HasOne<Person>().WithMany().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Gebuehrenpaket>().HasOne<Kurs>().WithMany().HasForeignKey(p => p.KursId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Kursteilnahme>().HasMany(t => t.Nachweise).WithOne().HasForeignKey(n => n.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);

        // Termine: alles, was an einem Termin hängt, wird mit ihm gelöscht
        mb.Entity<Termin>().HasOne<Kurs>().WithMany().HasForeignKey(t => t.KursId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Termin>().HasOne(t => t.Revier).WithMany().HasForeignKey(t => t.RevierId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Termin>().HasOne(t => t.Vorbereitung).WithOne().HasForeignKey<Vorbereitungstermin>(v => v.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Termin>().HasIndex(t => new { t.KursId, t.Datum });

        mb.Entity<Helferdienst>().HasOne<Termin>().WithMany().HasForeignKey(x => x.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Helferdienst>().HasOne<Kursteilnahme>().WithMany().HasForeignKey(x => x.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Terminabweichung>().HasOne<Termin>().WithMany().HasForeignKey(x => x.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Terminabweichung>().HasOne<Kursteilnahme>().WithMany().HasForeignKey(x => x.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Faehrtenzuteilung>().HasOne<Termin>().WithMany().HasForeignKey(x => x.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Faehrtenzuteilung>().HasOne<Kursteilnahme>().WithMany().HasForeignKey(x => x.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Faehrtenlauf>().HasOne<Termin>().WithMany().HasForeignKey(x => x.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Faehrtenlauf>().HasOne<Kursteilnahme>().WithMany().HasForeignKey(x => x.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Termin>().HasOne<Uebungsgelaende>().WithMany().HasForeignKey(t => t.UebungsgelaendeId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Anwesenheit>().HasOne<Termin>().WithMany().HasForeignKey(x => x.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Anwesenheit>().HasOne<Kursteilnahme>().WithMany().HasForeignKey(x => x.KursteilnahmeId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Anwesenheit>().HasIndex(x => new { x.TerminId, x.KursteilnahmeId }).IsUnique();

        mb.Entity<Einsatz>().HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Einsatz>().HasOne<Termin>().WithMany().HasForeignKey(e => e.TerminId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Einsatz>().HasIndex(e => new { e.TerminId, e.PersonId, e.Art }).IsUnique();
        mb.Entity<Einsatz>().HasIndex(e => e.Datum);
        mb.Entity<Standardstrecke>().HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Standardstrecke>().HasIndex(x => new { x.PersonId, x.RevierId, x.UebungsgelaendeId });

        mb.Entity<Aenderungsprotokoll>().HasIndex(a => a.Zeitpunkt);
    }
}
