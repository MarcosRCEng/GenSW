using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCiclosReprodutivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CiclosReprodutivos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CruzamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DataPostura = table.Column<DateOnly>(type: "date", nullable: true),
                    DataInicioIncubacao = table.Column<DateOnly>(type: "date", nullable: true),
                    DataEclosao = table.Column<DateOnly>(type: "date", nullable: true),
                    OvosPostos = table.Column<int>(type: "integer", nullable: true),
                    OvosFerteis = table.Column<int>(type: "integer", nullable: true),
                    OvosIncubados = table.Column<int>(type: "integer", nullable: true),
                    OvosEclodidos = table.Column<int>(type: "integer", nullable: true),
                    OvosInviaveis = table.Column<int>(type: "integer", nullable: true),
                    PesoMedioOvoGramas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DataInicioGestacao = table.Column<DateOnly>(type: "date", nullable: true),
                    DataPrevistaParto = table.Column<DateOnly>(type: "date", nullable: true),
                    DataParto = table.Column<DateOnly>(type: "date", nullable: true),
                    Nascidos = table.Column<int>(type: "integer", nullable: true),
                    NascidosVivos = table.Column<int>(type: "integer", nullable: true),
                    NascidosMortos = table.Column<int>(type: "integer", nullable: true),
                    PesoAoNascerGramas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiclosReprodutivos", x => x.Id);
                    table.CheckConstraint("CK_CiclosReprodutivos_Pesos", "(\"PesoMedioOvoGramas\" IS NULL OR \"PesoMedioOvoGramas\" > 0) AND (\"PesoAoNascerGramas\" IS NULL OR \"PesoAoNascerGramas\" > 0)");
                    table.CheckConstraint("CK_CiclosReprodutivos_Quantidades", "(\"OvosPostos\" IS NULL OR \"OvosPostos\" >= 0) AND (\"OvosFerteis\" IS NULL OR \"OvosFerteis\" >= 0) AND (\"OvosIncubados\" IS NULL OR \"OvosIncubados\" >= 0) AND (\"OvosEclodidos\" IS NULL OR \"OvosEclodidos\" >= 0) AND (\"OvosInviaveis\" IS NULL OR \"OvosInviaveis\" >= 0) AND (\"Nascidos\" IS NULL OR \"Nascidos\" >= 0) AND (\"NascidosVivos\" IS NULL OR \"NascidosVivos\" >= 0) AND (\"NascidosMortos\" IS NULL OR \"NascidosMortos\" >= 0)");
                    table.CheckConstraint("CK_CiclosReprodutivos_Status", "\"Status\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_CiclosReprodutivos_Tipo", "\"Tipo\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CiclosReprodutivos_Cruzamentos_CruzamentoId",
                        column: x => x.CruzamentoId,
                        principalTable: "Cruzamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CiclosReprodutivos_CruzamentoId_Status",
                table: "CiclosReprodutivos",
                columns: new[] { "CruzamentoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CiclosReprodutivos_Tipo",
                table: "CiclosReprodutivos",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CiclosReprodutivos");
        }
    }
}
