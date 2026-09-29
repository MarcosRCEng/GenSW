using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiliacoesAnimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiliacoesAnimal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgenitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoFiliacao = table.Column<int>(type: "integer", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DataRegistro = table.Column<DateOnly>(type: "date", nullable: true),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiliacoesAnimal", x => x.Id);
                    table.CheckConstraint("CK_FiliacoesAnimal_Ativa_DataFim", "(\"Ativa\" AND \"DataFim\" IS NULL) OR (NOT \"Ativa\")");
                    table.CheckConstraint("CK_FiliacoesAnimal_Tipo", "\"TipoFiliacao\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_FiliacoesAnimal_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiliacoesAnimal_Animais_ProgenitorId",
                        column: x => x.ProgenitorId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiliacoesAnimal_ProgenitorId",
                table: "FiliacoesAnimal",
                column: "ProgenitorId");

            migrationBuilder.CreateIndex(
                name: "UX_FiliacoesAnimal_Animal_Tipo_Ativa",
                table: "FiliacoesAnimal",
                columns: new[] { "AnimalId", "TipoFiliacao" },
                unique: true,
                filter: "\"Ativa\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiliacoesAnimal");
        }
    }
}
