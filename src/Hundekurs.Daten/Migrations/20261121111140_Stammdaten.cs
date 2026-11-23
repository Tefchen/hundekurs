using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hundekurs.Daten.Migrations
{
    /// <inheritdoc />
    public partial class Stammdaten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hunde_Personen_EigentuemerId",
                table: "Hunde");

            migrationBuilder.DropForeignKey(
                name: "FK_Hunde_Personen_FuehrerId",
                table: "Hunde");

            migrationBuilder.DropTable(
                name: "Tarife");

            migrationBuilder.DropTable(
                name: "Zahlungen");

            migrationBuilder.DropTable(
                name: "Forderungen");

            migrationBuilder.DropColumn(
                name: "Art",
                table: "Uebungsgelaende");

            migrationBuilder.DropColumn(
                name: "GeeigneteDisziplinen",
                table: "Uebungsgelaende");

            migrationBuilder.DropColumn(
                name: "Art",
                table: "Kursteilnahmen");

            // Von Hand korrigiert: EF hatte die alte Spalte „Faecher“ in „Wiederholer“ umbenannt,
            // dadurch wären alte Einträge zu Wiederholern geworden. Richtig: alte Spalte weg, neue mit false.
            migrationBuilder.DropColumn(
                name: "Faecher",
                table: "Kursteilnahmen");

            migrationBuilder.AddColumn<bool>(
                name: "Wiederholer",
                table: "Kursteilnahmen",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BezahltAm",
                table: "Kursteilnahmen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GebuehrHinweis",
                table: "Kursteilnahmen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GebuehrVonHand",
                table: "Kursteilnahmen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "FuehrerId",
                table: "Hunde",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Bemerkung",
                table: "Hunde",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brauchbarkeiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HundId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Ort = table.Column<string>(type: "TEXT", nullable: true),
                    FachIds = table.Column<string>(type: "TEXT", nullable: false),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brauchbarkeiten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Brauchbarkeiten_Hunde_HundId",
                        column: x => x.HundId,
                        principalTable: "Hunde",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faecher",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Bereich = table.Column<int>(type: "INTEGER", nullable: false),
                    Reihenfolge = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktiv = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faecher", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gebuehrenpakete",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Gehorsam = table.Column<bool>(type: "INTEGER", nullable: false),
                    NachsucheSchalenwild = table.Column<bool>(type: "INTEGER", nullable: false),
                    NachsucheNiederwild = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preis = table.Column<decimal>(type: "TEXT", nullable: false),
                    Mitgliederpreis = table.Column<decimal>(type: "TEXT", nullable: false),
                    Reihenfolge = table.Column<int>(type: "INTEGER", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gebuehrenpakete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gebuehrenpakete_Kurse_KursId",
                        column: x => x.KursId,
                        principalTable: "Kurse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GelaendeFaecher",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UebungsgelaendeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FachId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GelaendeFaecher", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GelaendeFaecher_Faecher_FachId",
                        column: x => x.FachId,
                        principalTable: "Faecher",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GelaendeFaecher_Uebungsgelaende_UebungsgelaendeId",
                        column: x => x.UebungsgelaendeId,
                        principalTable: "Uebungsgelaende",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rassen_Bezeichnung",
                table: "Rassen",
                column: "Bezeichnung",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brauchbarkeiten_HundId",
                table: "Brauchbarkeiten",
                column: "HundId");

            migrationBuilder.CreateIndex(
                name: "IX_Faecher_Bezeichnung",
                table: "Faecher",
                column: "Bezeichnung",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gebuehrenpakete_KursId",
                table: "Gebuehrenpakete",
                column: "KursId");

            migrationBuilder.CreateIndex(
                name: "IX_GelaendeFaecher_FachId",
                table: "GelaendeFaecher",
                column: "FachId");

            migrationBuilder.CreateIndex(
                name: "IX_GelaendeFaecher_UebungsgelaendeId_FachId",
                table: "GelaendeFaecher",
                columns: new[] { "UebungsgelaendeId", "FachId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Hunde_Personen_EigentuemerId",
                table: "Hunde",
                column: "EigentuemerId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Hunde_Personen_FuehrerId",
                table: "Hunde",
                column: "FuehrerId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hunde_Personen_EigentuemerId",
                table: "Hunde");

            migrationBuilder.DropForeignKey(
                name: "FK_Hunde_Personen_FuehrerId",
                table: "Hunde");

            migrationBuilder.DropTable(
                name: "Brauchbarkeiten");

            migrationBuilder.DropTable(
                name: "Gebuehrenpakete");

            migrationBuilder.DropTable(
                name: "GelaendeFaecher");

            migrationBuilder.DropTable(
                name: "Faecher");

            migrationBuilder.DropIndex(
                name: "IX_Rassen_Bezeichnung",
                table: "Rassen");

            migrationBuilder.DropColumn(
                name: "BezahltAm",
                table: "Kursteilnahmen");

            migrationBuilder.DropColumn(
                name: "GebuehrHinweis",
                table: "Kursteilnahmen");

            migrationBuilder.DropColumn(
                name: "GebuehrVonHand",
                table: "Kursteilnahmen");

            migrationBuilder.DropColumn(
                name: "Bemerkung",
                table: "Hunde");

            migrationBuilder.DropColumn(
                name: "Wiederholer",
                table: "Kursteilnahmen");

            migrationBuilder.AddColumn<int>(
                name: "Faecher",
                table: "Kursteilnahmen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Art",
                table: "Uebungsgelaende",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GeeigneteDisziplinen",
                table: "Uebungsgelaende",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Art",
                table: "Kursteilnahmen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "FuehrerId",
                table: "Hunde",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Forderungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    Faellig = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true),
                    KursteilnahmeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TarifId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Forderungen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tarife",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    Faecher = table.Column<int>(type: "INTEGER", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    NurMitglieder = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarife", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zahlungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    ForderungId = table.Column<int>(type: "INTEGER", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_Zahlungen_ForderungId",
                table: "Zahlungen",
                column: "ForderungId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hunde_Personen_EigentuemerId",
                table: "Hunde",
                column: "EigentuemerId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Hunde_Personen_FuehrerId",
                table: "Hunde",
                column: "FuehrerId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
