using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrosAnimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosAnimal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoRegistro = table.Column<int>(type: "integer", nullable: false),
                    NumeroRegistro = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAnimal", x => x.Id);
                    table.CheckConstraint("CK_RegistrosAnimal_Ativo_DataFim", "(\"Ativo\" AND \"DataFim\" IS NULL) OR (NOT \"Ativo\" AND \"DataFim\" IS NOT NULL)");
                    table.CheckConstraint("CK_RegistrosAnimal_DataFim", "\"DataFim\" IS NULL OR \"DataFim\" >= \"DataInicio\"");
                    table.CheckConstraint("CK_RegistrosAnimal_NumeroRegistro_Canonical", "\"NumeroRegistro\" <> '' AND \"NumeroRegistro\" = btrim(\"NumeroRegistro\")");
                    table.CheckConstraint("CK_RegistrosAnimal_TipoRegistro", "\"TipoRegistro\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_RegistrosAnimal_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAnimal_AnimalId",
                table: "RegistrosAnimal",
                column: "AnimalId");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"UX_RegistrosAnimal_TipoRegistro_NumeroRegistro_CaseInsensitive\" ON \"RegistrosAnimal\" (\"TipoRegistro\", lower(\"NumeroRegistro\"));");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"UX_RegistrosAnimal_Animal_TipoRegistro_Ativo\" ON \"RegistrosAnimal\" (\"AnimalId\", \"TipoRegistro\") WHERE \"Ativo\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_RegistrosAnimal_Animal_TipoRegistro_Ativo\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_RegistrosAnimal_TipoRegistro_NumeroRegistro_CaseInsensitive\";");
            migrationBuilder.DropTable(
                name: "RegistrosAnimal");
        }
    }
}
