using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hundekurs.Daten.Migrations
{
    /// <inheritdoc />
    public partial class Start : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aenderungsprotokoll",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Zeitpunkt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Benutzer = table.Column<string>(type: "TEXT", nullable: false),
                    Entitaet = table.Column<string>(type: "TEXT", nullable: false),
                    EntitaetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktion = table.Column<string>(type: "TEXT", nullable: false),
                    WerteVorher = table.Column<string>(type: "TEXT", nullable: true),
                    WerteNachher = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aenderungsprotokoll", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Beobachtungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Disziplin = table.Column<int>(type: "INTEGER", nullable: false),
                    Bewertung = table.Column<int>(type: "INTEGER", nullable: false),
                    MerkmalCodes = table.Column<string>(type: "TEXT", nullable: false),
                    Notiz = table.Column<string>(type: "TEXT", nullable: true),
                    ErfasstVonId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beobachtungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Einsatzabrechnungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    Von = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Bis = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    KmSatz = table.Column<decimal>(type: "TEXT", nullable: false),
                    AbgeschlossenAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Einsatzabrechnungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Forderungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TarifId = table.Column<int>(type: "INTEGER", nullable: true),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    Faellig = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forderungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kurse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Beginn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Ende = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MaxTrainingsplaetzeSchweiss = table.Column<int>(type: "INTEGER", nullable: false),
                    HelferProTermin = table.Column<int>(type: "INTEGER", nullable: false),
                    HelferAusAndererGruppe = table.Column<bool>(type: "INTEGER", nullable: false),
                    KmSatz = table.Column<decimal>(type: "TEXT", nullable: false),
                    GehorsamGelaendeId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApportierGelaendeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Jahr = table.Column<int>(type: "INTEGER", nullable: true),
                    MailEinleitung = table.Column<string>(type: "TEXT", nullable: true),
                    MailGruss = table.Column<string>(type: "TEXT", nullable: true),
                    MailAbgewaehlt = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kurse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Merkmale",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Disziplin = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merkmale", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nennungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PruefungstagId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Faecher = table.Column<int>(type: "INTEGER", nullable: false),
                    NenngeldBezahlt = table.Column<bool>(type: "INTEGER", nullable: false),
                    Losnummer = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nennungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Personen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Anrede = table.Column<int>(type: "INTEGER", nullable: false),
                    Nachname = table.Column<string>(type: "TEXT", nullable: false),
                    Vorname = table.Column<string>(type: "TEXT", nullable: false),
                    Strasse = table.Column<string>(type: "TEXT", nullable: true),
                    Plz = table.Column<string>(type: "TEXT", nullable: true),
                    Ort = table.Column<string>(type: "TEXT", nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", nullable: true),
                    EMail = table.Column<string>(type: "TEXT", nullable: true),
                    Vereinsmitglied = table.Column<bool>(type: "INTEGER", nullable: false),
                    Breitengrad = table.Column<double>(type: "REAL", nullable: true),
                    Laengengrad = table.Column<double>(type: "REAL", nullable: true),
                    GeokodierteAdresse = table.Column<string>(type: "TEXT", nullable: true),
                    Richternummer = table.Column<string>(type: "TEXT", nullable: true),
                    AnonymisiertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pruefungsergebnisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NennungId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fach = table.Column<int>(type: "INTEGER", nullable: false),
                    Ergebnis = table.Column<string>(type: "TEXT", nullable: true),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pruefungsergebnisse", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pruefungstage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    PruefungsleitungId = table.Column<int>(type: "INTEGER", nullable: true),
                    Richter2Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Richter3Id = table.Column<int>(type: "INTEGER", nullable: true),
                    HelferStandruheId = table.Column<int>(type: "INTEGER", nullable: true),
                    Treiber = table.Column<string>(type: "TEXT", nullable: true),
                    Lokal = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pruefungstage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rassen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kuerzel = table.Column<string>(type: "TEXT", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rassen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviere",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kuerzel = table.Column<string>(type: "TEXT", nullable: false),
                    Treffpunkt = table.Column<string>(type: "TEXT", nullable: true),
                    Hinweise = table.Column<string>(type: "TEXT", nullable: true),
                    Verwendung = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinRuhetage = table.Column<int>(type: "INTEGER", nullable: false),
                    ZielRuhetage = table.Column<int>(type: "INTEGER", nullable: false),
                    Breitengrad = table.Column<double>(type: "REAL", nullable: true),
                    Laengengrad = table.Column<double>(type: "REAL", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviere", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tarife",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    NurMitglieder = table.Column<bool>(type: "INTEGER", nullable: true),
                    Faecher = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarife", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tauschvorgaenge",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZweiterTerminId = table.Column<int>(type: "INTEGER", nullable: true),
                    BeteiligteTeilnahmeIds = table.Column<string>(type: "TEXT", nullable: false),
                    Auswirkungen = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tauschvorgaenge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Verfuegbarkeiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verfuegbarkeiten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zahlungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ForderungId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zahlungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zahlungen_Forderungen_ForderungId",
                        column: x => x.ForderungId,
                        principalTable: "Forderungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Kursrollen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rolle = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kursrollen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kursrollen_Kurse_KursId",
                        column: x => x.KursId,
                        principalTable: "Kurse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Kursrollen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Standardstrecken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: true),
                    UebungsgelaendeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Kilometer = table.Column<double>(type: "REAL", nullable: false),
                    BerechnetAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standardstrecken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Standardstrecken_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Hunde",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Rufname = table.Column<string>(type: "TEXT", nullable: false),
                    Zuchtbuchname = table.Column<string>(type: "TEXT", nullable: true),
                    RasseId = table.Column<int>(type: "INTEGER", nullable: true),
                    Wurfdatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Geschlecht = table.Column<int>(type: "INTEGER", nullable: true),
                    ChipNummer = table.Column<string>(type: "TEXT", nullable: true),
                    Zuchtbuchnummer = table.Column<string>(type: "TEXT", nullable: true),
                    HeimtierausweisNummer = table.Column<string>(type: "TEXT", nullable: true),
                    FuehrerId = table.Column<int>(type: "INTEGER", nullable: false),
                    EigentuemerId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunde", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hunde_Personen_EigentuemerId",
                        column: x => x.EigentuemerId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hunde_Personen_FuehrerId",
                        column: x => x.FuehrerId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hunde_Rassen_RasseId",
                        column: x => x.RasseId,
                        principalTable: "Rassen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Faehrten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faehrten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faehrten_Reviere_RevierId",
                        column: x => x.RevierId,
                        principalTable: "Reviere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faehrtenauffrischungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Faehrten = table.Column<string>(type: "TEXT", nullable: false),
                    BeteiligtePersonIds = table.Column<string>(type: "TEXT", nullable: false),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faehrtenauffrischungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faehrtenauffrischungen_Reviere_RevierId",
                        column: x => x.RevierId,
                        principalTable: "Reviere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sequenzpaare",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ErsteFaehrte = table.Column<int>(type: "INTEGER", nullable: false),
                    ZweiteFaehrte = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sequenzpaare", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sequenzpaare_Reviere_RevierId",
                        column: x => x.RevierId,
                        principalTable: "Reviere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Uebungsgelaende",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: true),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Treffpunkt = table.Column<string>(type: "TEXT", nullable: true),
                    Breitengrad = table.Column<double>(type: "REAL", nullable: true),
                    Laengengrad = table.Column<double>(type: "REAL", nullable: true),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeeigneteDisziplinen = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uebungsgelaende", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uebungsgelaende_Reviere_RevierId",
                        column: x => x.RevierId,
                        principalTable: "Reviere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Kursteilnahmen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    HundId = table.Column<int>(type: "INTEGER", nullable: false),
                    FuehrerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    GehorsamGebucht = table.Column<bool>(type: "INTEGER", nullable: false),
                    JagdlichGebucht = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApportierGebucht = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZulassungAusnahme = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZulassungBegruendung = table.Column<string>(type: "TEXT", nullable: true),
                    Faecher = table.Column<int>(type: "INTEGER", nullable: false),
                    Gruppe = table.Column<int>(type: "INTEGER", nullable: true),
                    Pruefungsziel = table.Column<string>(type: "TEXT", nullable: true),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kursteilnahmen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kursteilnahmen_Hunde_HundId",
                        column: x => x.HundId,
                        principalTable: "Hunde",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Kursteilnahmen_Kurse_KursId",
                        column: x => x.KursId,
                        principalTable: "Kurse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Kursteilnahmen_Personen_FuehrerId",
                        column: x => x.FuehrerId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Termine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GruppeSchweiss = table.Column<int>(type: "INTEGER", nullable: true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: true),
                    UhrzeitSchweiss = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    RevierFixiert = table.Column<bool>(type: "INTEGER", nullable: false),
                    HelferFixiert = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZuteilungFixiert = table.Column<bool>(type: "INTEGER", nullable: false),
                    UebungsgelaendeId = table.Column<int>(type: "INTEGER", nullable: true),
                    UhrzeitUebrigeFaecher = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Notiz = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Termine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Termine_Kurse_KursId",
                        column: x => x.KursId,
                        principalTable: "Kurse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Termine_Reviere_RevierId",
                        column: x => x.RevierId,
                        principalTable: "Reviere",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Termine_Uebungsgelaende_UebungsgelaendeId",
                        column: x => x.UebungsgelaendeId,
                        principalTable: "Uebungsgelaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Nachweise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GeprueftAm = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    GeprueftVonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Dateipfad = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nachweise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nachweise_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Anwesenheiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Anwesend = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notiz = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anwesenheiten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anwesenheiten_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Anwesenheiten_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Einsaetze",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: true),
                    UebungsgelaendeId = table.Column<int>(type: "INTEGER", nullable: true),
                    KmBerechnet = table.Column<double>(type: "REAL", nullable: true),
                    KmBerechnetAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    KmKorrigiert = table.Column<double>(type: "REAL", nullable: true),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    AbrechnungId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Einsaetze", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Einsaetze_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Einsaetze_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faehrtenlaeufe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FaehrteNummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Ergebnis = table.Column<int>(type: "INTEGER", nullable: false),
                    Notiz = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faehrtenlaeufe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faehrtenlaeufe_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Faehrtenlaeufe_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faehrtenzuteilungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FaehrteNummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequenz = table.Column<int>(type: "INTEGER", nullable: false),
                    ZweiterLaufAufFaehrte = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faehrtenzuteilungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faehrtenzuteilungen_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Faehrtenzuteilungen_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Helferdienste",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Helferdienste", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Helferdienste_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Helferdienste_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Terminabweichungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminabweichungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Terminabweichungen_Kursteilnahmen_KursteilnahmeId",
                        column: x => x.KursteilnahmeId,
                        principalTable: "Kursteilnahmen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Terminabweichungen_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vorbereitungstermine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Uhrzeit = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    RevierId = table.Column<int>(type: "INTEGER", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vorbereitungstermine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vorbereitungstermine_Termine_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aenderungsprotokoll_Zeitpunkt",
                table: "Aenderungsprotokoll",
                column: "Zeitpunkt");

            migrationBuilder.CreateIndex(
                name: "IX_Anwesenheiten_KursteilnahmeId",
                table: "Anwesenheiten",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Anwesenheiten_TerminId_KursteilnahmeId",
                table: "Anwesenheiten",
                columns: new[] { "TerminId", "KursteilnahmeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Einsaetze_Datum",
                table: "Einsaetze",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Einsaetze_PersonId",
                table: "Einsaetze",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Einsaetze_TerminId_PersonId_Art",
                table: "Einsaetze",
                columns: new[] { "TerminId", "PersonId", "Art" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faehrten_RevierId_Nummer",
                table: "Faehrten",
                columns: new[] { "RevierId", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faehrtenauffrischungen_RevierId",
                table: "Faehrtenauffrischungen",
                column: "RevierId");

            migrationBuilder.CreateIndex(
                name: "IX_Faehrtenlaeufe_KursteilnahmeId",
                table: "Faehrtenlaeufe",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Faehrtenlaeufe_TerminId",
                table: "Faehrtenlaeufe",
                column: "TerminId");

            migrationBuilder.CreateIndex(
                name: "IX_Faehrtenzuteilungen_KursteilnahmeId",
                table: "Faehrtenzuteilungen",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Faehrtenzuteilungen_TerminId",
                table: "Faehrtenzuteilungen",
                column: "TerminId");

            migrationBuilder.CreateIndex(
                name: "IX_Helferdienste_KursteilnahmeId",
                table: "Helferdienste",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Helferdienste_TerminId",
                table: "Helferdienste",
                column: "TerminId");

            migrationBuilder.CreateIndex(
                name: "IX_Hunde_EigentuemerId",
                table: "Hunde",
                column: "EigentuemerId");

            migrationBuilder.CreateIndex(
                name: "IX_Hunde_FuehrerId",
                table: "Hunde",
                column: "FuehrerId");

            migrationBuilder.CreateIndex(
                name: "IX_Hunde_RasseId",
                table: "Hunde",
                column: "RasseId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursrollen_KursId",
                table: "Kursrollen",
                column: "KursId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursrollen_PersonId",
                table: "Kursrollen",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursteilnahmen_FuehrerId",
                table: "Kursteilnahmen",
                column: "FuehrerId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursteilnahmen_HundId",
                table: "Kursteilnahmen",
                column: "HundId");

            migrationBuilder.CreateIndex(
                name: "IX_Kursteilnahmen_KursId",
                table: "Kursteilnahmen",
                column: "KursId");

            migrationBuilder.CreateIndex(
                name: "IX_Nachweise_KursteilnahmeId",
                table: "Nachweise",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviere_Kuerzel",
                table: "Reviere",
                column: "Kuerzel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sequenzpaare_RevierId",
                table: "Sequenzpaare",
                column: "RevierId");

            migrationBuilder.CreateIndex(
                name: "IX_Standardstrecken_PersonId_RevierId_UebungsgelaendeId",
                table: "Standardstrecken",
                columns: new[] { "PersonId", "RevierId", "UebungsgelaendeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Terminabweichungen_KursteilnahmeId",
                table: "Terminabweichungen",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Terminabweichungen_TerminId",
                table: "Terminabweichungen",
                column: "TerminId");

            migrationBuilder.CreateIndex(
                name: "IX_Termine_KursId_Datum",
                table: "Termine",
                columns: new[] { "KursId", "Datum" });

            migrationBuilder.CreateIndex(
                name: "IX_Termine_RevierId",
                table: "Termine",
                column: "RevierId");

            migrationBuilder.CreateIndex(
                name: "IX_Termine_UebungsgelaendeId",
                table: "Termine",
                column: "UebungsgelaendeId");

            migrationBuilder.CreateIndex(
                name: "IX_Uebungsgelaende_RevierId",
                table: "Uebungsgelaende",
                column: "RevierId");

            migrationBuilder.CreateIndex(
                name: "IX_Vorbereitungstermine_TerminId",
                table: "Vorbereitungstermine",
                column: "TerminId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zahlungen_ForderungId",
                table: "Zahlungen",
                column: "ForderungId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aenderungsprotokoll");

            migrationBuilder.DropTable(
                name: "Anwesenheiten");

            migrationBuilder.DropTable(
                name: "Beobachtungen");

            migrationBuilder.DropTable(
                name: "Einsaetze");

            migrationBuilder.DropTable(
                name: "Einsatzabrechnungen");

            migrationBuilder.DropTable(
                name: "Faehrten");

            migrationBuilder.DropTable(
                name: "Faehrtenauffrischungen");

            migrationBuilder.DropTable(
                name: "Faehrtenlaeufe");

            migrationBuilder.DropTable(
                name: "Faehrtenzuteilungen");

            migrationBuilder.DropTable(
                name: "Helferdienste");

            migrationBuilder.DropTable(
                name: "Kursrollen");

            migrationBuilder.DropTable(
                name: "Merkmale");

            migrationBuilder.DropTable(
                name: "Nachweise");

            migrationBuilder.DropTable(
                name: "Nennungen");

            migrationBuilder.DropTable(
                name: "Pruefungsergebnisse");

            migrationBuilder.DropTable(
                name: "Pruefungstage");

            migrationBuilder.DropTable(
                name: "Sequenzpaare");

            migrationBuilder.DropTable(
                name: "Standardstrecken");

            migrationBuilder.DropTable(
                name: "Tarife");

            migrationBuilder.DropTable(
                name: "Tauschvorgaenge");

            migrationBuilder.DropTable(
                name: "Terminabweichungen");

            migrationBuilder.DropTable(
                name: "Verfuegbarkeiten");

            migrationBuilder.DropTable(
                name: "Vorbereitungstermine");

            migrationBuilder.DropTable(
                name: "Zahlungen");

            migrationBuilder.DropTable(
                name: "Kursteilnahmen");

            migrationBuilder.DropTable(
                name: "Termine");

            migrationBuilder.DropTable(
                name: "Forderungen");

            migrationBuilder.DropTable(
                name: "Hunde");

            migrationBuilder.DropTable(
                name: "Kurse");

            migrationBuilder.DropTable(
                name: "Uebungsgelaende");

            migrationBuilder.DropTable(
                name: "Personen");

            migrationBuilder.DropTable(
                name: "Rassen");

            migrationBuilder.DropTable(
                name: "Reviere");
        }
    }
}
