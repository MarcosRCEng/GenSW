using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Proles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CicloReprodutivoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteOrigemId = table.Column<Guid>(type: "uuid", nullable: true),
                    TipoRegistro = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeDesdobrada = table.Column<int>(type: "integer", nullable: false),
                    Origem = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    PesoGramas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Sexo = table.Column<int>(type: "integer", nullable: false),
                    Condicao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proles", x => x.Id);
                    table.CheckConstraint("CK_Proles_Origem", "\"Origem\" IN (1, 2)");
                    table.CheckConstraint("CK_Proles_Peso", "\"PesoGramas\" IS NULL OR \"PesoGramas\" > 0");
                    table.CheckConstraint("CK_Proles_Quantidade", "\"Quantidade\" > 0 AND (\"TipoRegistro\" <> 1 OR \"Quantidade\" = 1) AND \"QuantidadeDesdobrada\" >= 0 AND \"QuantidadeDesdobrada\" <= \"Quantidade\"");
                    table.CheckConstraint("CK_Proles_Sexo", "\"Sexo\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Proles_TipoRegistro", "\"TipoRegistro\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Proles_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proles_CiclosReprodutivos_CicloReprodutivoId",
                        column: x => x.CicloReprodutivoId,
                        principalTable: "CiclosReprodutivos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proles_Proles_LoteOrigemId",
                        column: x => x.LoteOrigemId,
                        principalTable: "Proles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proles_CicloReprodutivoId_LoteOrigemId",
                table: "Proles",
                columns: new[] { "CicloReprodutivoId", "LoteOrigemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Proles_LoteOrigemId",
                table: "Proles",
                column: "LoteOrigemId");

            migrationBuilder.CreateIndex(
                name: "UX_Proles_AnimalId",
                table: "Proles",
                column: "AnimalId",
                unique: true,
                filter: "\"AnimalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Proles");
        }
    }
}
