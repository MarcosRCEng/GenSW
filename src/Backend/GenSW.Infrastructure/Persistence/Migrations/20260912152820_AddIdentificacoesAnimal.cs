using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentificacoesAnimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentificacoesAnimal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    DescricaoTipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Valor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Principal = table.Column<bool>(type: "boolean", nullable: false),
                    DataAplicacao = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentificacoesAnimal", x => x.Id);
                    table.CheckConstraint("CK_IdentificacoesAnimal_DescricaoTipo_Semantics", "(\"Tipo\" IN (1, 2, 3, 4, 5) AND \"DescricaoTipo\" IS NULL) OR (\"Tipo\" = 6 AND \"DescricaoTipo\" IS NOT NULL AND \"DescricaoTipo\" <> '' AND \"DescricaoTipo\" = btrim(\"DescricaoTipo\"))");
                    table.CheckConstraint("CK_IdentificacoesAnimal_Tipo", "\"Tipo\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_IdentificacoesAnimal_Valor_Canonical", "\"Valor\" <> '' AND \"Valor\" = btrim(\"Valor\")");
                    table.ForeignKey(
                        name: "FK_IdentificacoesAnimal_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentificacoesAnimal_AnimalId",
                table: "IdentificacoesAnimal",
                column: "AnimalId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive"
                ON "IdentificacoesAnimal" ("Tipo", lower("Valor"))
                WHERE "Tipo" <> 6;
                """);
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensiti"
                ON "IdentificacoesAnimal" ("Tipo", lower("DescricaoTipo"), lower("Valor"))
                WHERE "Tipo" = 6;
                """);
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_IdentificacoesAnimal_Animal_PrincipalAtiva"
                ON "IdentificacoesAnimal" ("AnimalId")
                WHERE "Ativo" AND "Principal";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_IdentificacoesAnimal_Animal_PrincipalAtiva\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_IdentificacoesAnimal_Outro_DescricaoTipo_Valor_CaseInsensiti\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_IdentificacoesAnimal_Tipo_Valor_CaseInsensitive\";");
            migrationBuilder.DropTable(
                name: "IdentificacoesAnimal");
        }
    }
}
