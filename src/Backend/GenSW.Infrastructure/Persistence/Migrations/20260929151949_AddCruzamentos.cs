using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCruzamentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cruzamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FemeaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    DataFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Objetivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cruzamentos", x => x.Id);
                    table.CheckConstraint("CK_Cruzamentos_Animais_Distintos", "\"MachoId\" <> \"FemeaId\"");
                    table.CheckConstraint("CK_Cruzamentos_DataFim", "\"DataFim\" IS NULL OR \"DataInicio\" IS NULL OR \"DataFim\" >= \"DataInicio\"");
                    table.CheckConstraint("CK_Cruzamentos_Status", "\"Status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Cruzamentos_Animais_FemeaId",
                        column: x => x.FemeaId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cruzamentos_Animais_MachoId",
                        column: x => x.MachoId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cruzamentos_FemeaId",
                table: "Cruzamentos",
                column: "FemeaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cruzamentos_MachoId",
                table: "Cruzamentos",
                column: "MachoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cruzamentos_Status_DataInicio",
                table: "Cruzamentos",
                columns: new[] { "Status", "DataInicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cruzamentos");
        }
    }
}
