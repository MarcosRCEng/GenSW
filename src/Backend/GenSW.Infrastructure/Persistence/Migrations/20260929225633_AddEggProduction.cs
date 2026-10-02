using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEggProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ovipara",
                table: "Especies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PesoPadraoOvoGramas",
                table: "Especies",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProducoesOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataPostura = table.Column<DateOnly>(type: "date", nullable: false),
                    PesoGramas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProducoesOvos", x => x.Id);
                    table.CheckConstraint("CK_ProducoesOvos_PesoGramas", "\"PesoGramas\" > 0");
                    table.ForeignKey(
                        name: "FK_ProducoesOvos_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProducoesOvos_AnimalId_DataPostura",
                table: "ProducoesOvos",
                columns: new[] { "AnimalId", "DataPostura" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProducoesOvos");

            migrationBuilder.DropColumn(
                name: "Ovipara",
                table: "Especies");

            migrationBuilder.DropColumn(
                name: "PesoPadraoOvoGramas",
                table: "Especies");
        }
    }
}
