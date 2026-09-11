using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimalBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Variedades_Id_EspecieId",
                table: "Variedades",
                columns: new[] { "Id", "EspecieId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Racas_Id_EspecieId",
                table: "Racas",
                columns: new[] { "Id", "EspecieId" });

            migrationBuilder.CreateTable(
                name: "Animais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoInterno = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EspecieId = table.Column<Guid>(type: "uuid", nullable: false),
                    RacaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariedadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sexo = table.Column<int>(type: "integer", nullable: false),
                    DataNascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    Escopo = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Animais", x => x.Id);
                    table.CheckConstraint("CK_Animais_CodigoInterno_Canonical", "\"CodigoInterno\" <> '' AND \"CodigoInterno\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"CodigoInterno\" !~ '(^ | $|  )'");
                    table.CheckConstraint("CK_Animais_Escopo", "\"Escopo\" IN (1, 2)");
                    table.CheckConstraint("CK_Animais_Nome_Canonical", "\"Nome\" IS NULL OR (\"Nome\" <> '' AND \"Nome\" !~ U&'[\\0009-\\000D\\0085\\00A0\\1680\\2000-\\200A\\2028\\2029\\202F\\205F\\3000]' AND \"Nome\" !~ '(^ | $|  )')");
                    table.CheckConstraint("CK_Animais_Sexo", "\"Sexo\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Animais_Especies_EspecieId",
                        column: x => x.EspecieId,
                        principalTable: "Especies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Animais_Racas_RacaId_EspecieId",
                        columns: x => new { x.RacaId, x.EspecieId },
                        principalTable: "Racas",
                        principalColumns: new[] { "Id", "EspecieId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Animais_Variedades_VariedadeId_EspecieId",
                        columns: x => new { x.VariedadeId, x.EspecieId },
                        principalTable: "Variedades",
                        principalColumns: new[] { "Id", "EspecieId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Animais_EspecieId",
                table: "Animais",
                column: "EspecieId");

            migrationBuilder.CreateIndex(
                name: "IX_Animais_RacaId_EspecieId",
                table: "Animais",
                columns: new[] { "RacaId", "EspecieId" });

            migrationBuilder.CreateIndex(
                name: "IX_Animais_VariedadeId_EspecieId",
                table: "Animais",
                columns: new[] { "VariedadeId", "EspecieId" });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "UX_Animais_CodigoInterno_CaseInsensitive"
                ON "Animais" (lower("CodigoInterno"));
                """);
            migrationBuilder.Sql("""
                CREATE SEQUENCE "AnimalCodigoInternoSequence"
                AS bigint
                START WITH 1
                INCREMENT BY 1
                MINVALUE 1
                NO CYCLE
                CACHE 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Animais");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Variedades_Id_EspecieId",
                table: "Variedades");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Racas_Id_EspecieId",
                table: "Racas");

            migrationBuilder.Sql("DROP SEQUENCE \"AnimalCodigoInternoSequence\";");
        }
    }
}
