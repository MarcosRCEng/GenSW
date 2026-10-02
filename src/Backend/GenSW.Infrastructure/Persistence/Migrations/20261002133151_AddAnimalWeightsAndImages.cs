using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalWeightsAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImagensAnimal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Legenda = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataCaptura = table.Column<DateOnly>(type: "date", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Representativa = table.Column<bool>(type: "boolean", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    ArquivoKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MiniaturaKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mime = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Largura = table.Column<int>(type: "integer", nullable: false),
                    Altura = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagensAnimal", x => x.Id);
                    table.CheckConstraint("CK_ImagensAnimal_Dimensoes", "\"Largura\" BETWEEN 1 AND 2048 AND \"Altura\" BETWEEN 1 AND 2048 AND \"TamanhoBytes\" > 0");
                    table.CheckConstraint("CK_ImagensAnimal_Ordem", "\"Ordem\" >= 0");
                    table.CheckConstraint("CK_ImagensAnimal_Representativa", "NOT \"Representativa\" OR \"Ativa\"");
                    table.ForeignKey(
                        name: "FK_ImagensAnimal_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImagensVariedade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariedadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Legenda = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataCaptura = table.Column<DateOnly>(type: "date", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Representativa = table.Column<bool>(type: "boolean", nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    ArquivoKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MiniaturaKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mime = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Largura = table.Column<int>(type: "integer", nullable: false),
                    Altura = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagensVariedade", x => x.Id);
                    table.CheckConstraint("CK_ImagensVariedade_Dimensoes", "\"Largura\" BETWEEN 1 AND 2048 AND \"Altura\" BETWEEN 1 AND 2048 AND \"TamanhoBytes\" > 0");
                    table.CheckConstraint("CK_ImagensVariedade_Ordem", "\"Ordem\" >= 0");
                    table.CheckConstraint("CK_ImagensVariedade_Representativa", "NOT \"Representativa\" OR \"Ativa\"");
                    table.ForeignKey(
                        name: "FK_ImagensVariedade_Variedades_VariedadeId",
                        column: x => x.VariedadeId,
                        principalTable: "Variedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PesagensAnimal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataMedicao = table.Column<DateOnly>(type: "date", nullable: false),
                    PesoGramas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TipoMarco = table.Column<int>(type: "integer", nullable: false),
                    DescricaoMarco = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdadeReferenciaDias = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesagensAnimal", x => x.Id);
                    table.CheckConstraint("CK_PesagensAnimal_Idade", "(\"TipoMarco\" = 3 AND \"IdadeReferenciaDias\" IS NOT NULL AND \"IdadeReferenciaDias\" >= 0) OR (\"TipoMarco\" <> 3 AND \"IdadeReferenciaDias\" IS NULL)");
                    table.CheckConstraint("CK_PesagensAnimal_Marco", "\"TipoMarco\" BETWEEN 1 AND 6");
                    table.CheckConstraint("CK_PesagensAnimal_Outro", "\"TipoMarco\" <> 6 OR (\"DescricaoMarco\" IS NOT NULL AND length(trim(\"DescricaoMarco\")) > 0)");
                    table.CheckConstraint("CK_PesagensAnimal_Peso", "\"PesoGramas\" BETWEEN 0.01 AND 99999999.99");
                    table.ForeignKey(
                        name: "FK_PesagensAnimal_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImagensAnimal_AnimalId_Ordem_CreatedAtUtc_Id",
                table: "ImagensAnimal",
                columns: new[] { "AnimalId", "Ordem", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_ImagensAnimal_Representativa",
                table: "ImagensAnimal",
                column: "AnimalId",
                unique: true,
                filter: "\"Ativa\" AND \"Representativa\"");

            migrationBuilder.CreateIndex(
                name: "IX_ImagensVariedade_VariedadeId_Ordem_CreatedAtUtc_Id",
                table: "ImagensVariedade",
                columns: new[] { "VariedadeId", "Ordem", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_ImagensVariedade_Representativa",
                table: "ImagensVariedade",
                column: "VariedadeId",
                unique: true,
                filter: "\"Ativa\" AND \"Representativa\"");

            migrationBuilder.CreateIndex(
                name: "IX_PesagensAnimal_AnimalId_DataMedicao_Id",
                table: "PesagensAnimal",
                columns: new[] { "AnimalId", "DataMedicao", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImagensAnimal");

            migrationBuilder.DropTable(
                name: "ImagensVariedade");

            migrationBuilder.DropTable(
                name: "PesagensAnimal");
        }
    }
}
