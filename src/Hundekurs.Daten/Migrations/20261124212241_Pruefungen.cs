using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hundekurs.Daten.Migrations
{
    /// <inheritdoc />
    public partial class Pruefungen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PruefungsleitungId",
                table: "Pruefungstage",
                newName: "RichterobmannId");

            migrationBuilder.RenameColumn(
                name: "NenngeldBezahlt",
                table: "Nennungen",
                newName: "NachsucheSchalenwild");

            migrationBuilder.RenameColumn(
                name: "Faecher",
                table: "Nennungen",
                newName: "NachsucheNiederwild");

            migrationBuilder.AddColumn<string>(
                name: "Bemerkung",
                table: "Pruefungstage",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxHunde",
                table: "Pruefungstage",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Meldeschluss",
                table: "Pruefungstage",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrueferAmVortag",
                table: "Pruefungstage",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "WeitereHelfer",
                table: "Pruefungstage",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "KursteilnahmeId",
                table: "Nennungen",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Bemerkung",
                table: "Nennungen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FuehrerId",
                table: "Nennungen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Gaeste",
                table: "Nennungen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Gehorsam",
                table: "Nennungen",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GenanntAm",
                table: "Nennungen",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "HundId",
                table: "Nennungen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Nenngeld",
                table: "Nennungen",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NenngeldBezahltAm",
                table: "Nennungen",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Nenngeld",
                table: "Kurse",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungstage_HelferStandruheId",
                table: "Pruefungstage",
                column: "HelferStandruheId");

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungstage_Richter2Id",
                table: "Pruefungstage",
                column: "Richter2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungstage_Richter3Id",
                table: "Pruefungstage",
                column: "Richter3Id");

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungstage_RichterobmannId",
                table: "Pruefungstage",
                column: "RichterobmannId");

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungstage_TerminId",
                table: "Pruefungstage",
                column: "TerminId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pruefungsergebnisse_NennungId",
                table: "Pruefungsergebnisse",
                column: "NennungId");

            migrationBuilder.CreateIndex(
                name: "IX_Nennungen_FuehrerId",
                table: "Nennungen",
                column: "FuehrerId");

            migrationBuilder.CreateIndex(
                name: "IX_Nennungen_HundId",
                table: "Nennungen",
                column: "HundId");

            migrationBuilder.CreateIndex(
                name: "IX_Nennungen_KursteilnahmeId",
                table: "Nennungen",
                column: "KursteilnahmeId");

            migrationBuilder.CreateIndex(
                name: "IX_Nennungen_PruefungstagId_HundId",
                table: "Nennungen",
                columns: new[] { "PruefungstagId", "HundId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Nennungen_Hunde_HundId",
                table: "Nennungen",
                column: "HundId",
                principalTable: "Hunde",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Nennungen_Kursteilnahmen_KursteilnahmeId",
                table: "Nennungen",
                column: "KursteilnahmeId",
                principalTable: "Kursteilnahmen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Nennungen_Personen_FuehrerId",
                table: "Nennungen",
                column: "FuehrerId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Nennungen_Pruefungstage_PruefungstagId",
                table: "Nennungen",
                column: "PruefungstagId",
                principalTable: "Pruefungstage",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungsergebnisse_Nennungen_NennungId",
                table: "Pruefungsergebnisse",
                column: "NennungId",
                principalTable: "Nennungen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungstage_Personen_HelferStandruheId",
                table: "Pruefungstage",
                column: "HelferStandruheId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungstage_Personen_Richter2Id",
                table: "Pruefungstage",
                column: "Richter2Id",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungstage_Personen_Richter3Id",
                table: "Pruefungstage",
                column: "Richter3Id",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungstage_Personen_RichterobmannId",
                table: "Pruefungstage",
                column: "RichterobmannId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Pruefungstage_Termine_TerminId",
                table: "Pruefungstage",
                column: "TerminId",
                principalTable: "Termine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Nennungen_Hunde_HundId",
                table: "Nennungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Nennungen_Kursteilnahmen_KursteilnahmeId",
                table: "Nennungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Nennungen_Personen_FuehrerId",
                table: "Nennungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Nennungen_Pruefungstage_PruefungstagId",
                table: "Nennungen");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungsergebnisse_Nennungen_NennungId",
                table: "Pruefungsergebnisse");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungstage_Personen_HelferStandruheId",
                table: "Pruefungstage");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungstage_Personen_Richter2Id",
                table: "Pruefungstage");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungstage_Personen_Richter3Id",
                table: "Pruefungstage");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungstage_Personen_RichterobmannId",
                table: "Pruefungstage");

            migrationBuilder.DropForeignKey(
                name: "FK_Pruefungstage_Termine_TerminId",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungstage_HelferStandruheId",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungstage_Richter2Id",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungstage_Richter3Id",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungstage_RichterobmannId",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungstage_TerminId",
                table: "Pruefungstage");

            migrationBuilder.DropIndex(
                name: "IX_Pruefungsergebnisse_NennungId",
                table: "Pruefungsergebnisse");

            migrationBuilder.DropIndex(
                name: "IX_Nennungen_FuehrerId",
                table: "Nennungen");

            migrationBuilder.DropIndex(
                name: "IX_Nennungen_HundId",
                table: "Nennungen");

            migrationBuilder.DropIndex(
                name: "IX_Nennungen_KursteilnahmeId",
                table: "Nennungen");

            migrationBuilder.DropIndex(
                name: "IX_Nennungen_PruefungstagId_HundId",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "Bemerkung",
                table: "Pruefungstage");

            migrationBuilder.DropColumn(
                name: "MaxHunde",
                table: "Pruefungstage");

            migrationBuilder.DropColumn(
                name: "Meldeschluss",
                table: "Pruefungstage");

            migrationBuilder.DropColumn(
                name: "PrueferAmVortag",
                table: "Pruefungstage");

            migrationBuilder.DropColumn(
                name: "WeitereHelfer",
                table: "Pruefungstage");

            migrationBuilder.DropColumn(
                name: "Bemerkung",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "FuehrerId",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "Gaeste",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "Gehorsam",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "GenanntAm",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "HundId",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "Nenngeld",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "NenngeldBezahltAm",
                table: "Nennungen");

            migrationBuilder.DropColumn(
                name: "Nenngeld",
                table: "Kurse");

            migrationBuilder.RenameColumn(
                name: "RichterobmannId",
                table: "Pruefungstage",
                newName: "PruefungsleitungId");

            migrationBuilder.RenameColumn(
                name: "NachsucheSchalenwild",
                table: "Nennungen",
                newName: "NenngeldBezahlt");

            migrationBuilder.RenameColumn(
                name: "NachsucheNiederwild",
                table: "Nennungen",
                newName: "Faecher");

            migrationBuilder.AlterColumn<int>(
                name: "KursteilnahmeId",
                table: "Nennungen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
