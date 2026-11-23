using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hundekurs.Daten.Migrations
{
    /// <inheritdoc />
    public partial class Auslagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Titel",
                table: "Personen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Auslagen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KursId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    Bezeichnung = table.Column<string>(type: "TEXT", nullable: false),
                    Betrag = table.Column<decimal>(type: "TEXT", nullable: false),
                    PersonId = table.Column<int>(type: "INTEGER", nullable: true),
                    Anlass = table.Column<string>(type: "TEXT", nullable: true),
                    BelegLiegtVor = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErstattetAm = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Bemerkung = table.Column<string>(type: "TEXT", nullable: true),
                    ErstelltAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ErstelltVon = table.Column<string>(type: "TEXT", nullable: true),
                    GeaendertAm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GeaendertVon = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auslagen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auslagen_Kurse_KursId",
                        column: x => x.KursId,
                        principalTable: "Kurse",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Auslagen_Personen_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auslagen_KursId",
                table: "Auslagen",
                column: "KursId");

            migrationBuilder.CreateIndex(
                name: "IX_Auslagen_PersonId",
                table: "Auslagen",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auslagen");

            migrationBuilder.DropColumn(
                name: "Titel",
                table: "Personen");
        }
    }
}
