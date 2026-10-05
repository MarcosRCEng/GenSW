using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Propriedades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeNormalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Localizacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propriedades", x => x.Id);
                    table.CheckConstraint("CK_Propriedades_Nome_Canonical", "\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )'");
                });

            migrationBuilder.CreateTable(
                name: "VinculosAnimalPropriedade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropriedadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosAnimalPropriedade", x => x.Id);
                    table.CheckConstraint("CK_VinculosAnimalPropriedade_Periodo", "\"DataFim\" IS NULL OR \"DataFim\" >= \"DataInicio\"");
                    table.ForeignKey(
                        name: "FK_VinculosAnimalPropriedade_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculosAnimalPropriedade_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Propriedades_Ativo_Nome_Id",
                table: "Propriedades",
                columns: new[] { "Ativo", "Nome", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculosAnimalPropriedade_AnimalId_DataInicio_CreatedAtUtc_~",
                table: "VinculosAnimalPropriedade",
                columns: new[] { "AnimalId", "DataInicio", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculosAnimalPropriedade_PropriedadeId_DataFim",
                table: "VinculosAnimalPropriedade",
                columns: new[] { "PropriedadeId", "DataFim" });

            migrationBuilder.CreateIndex(
                name: "UX_VinculosAnimalPropriedade_Animal_Atual",
                table: "VinculosAnimalPropriedade",
                column: "AnimalId",
                unique: true,
                filter: "\"DataFim\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Propriedades_Nome_CaseInsensitive",
                table: "Propriedades",
                column: "NomeNormalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VinculosAnimalPropriedade");

            migrationBuilder.DropTable(
                name: "Propriedades");
        }
    }
}
