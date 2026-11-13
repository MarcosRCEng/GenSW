using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormulationOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecucoesOtimizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerToken = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    EspecieId = table.Column<Guid>(type: "uuid", nullable: true),
                    Fase = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PedidoJson = table.Column<string>(type: "jsonb", nullable: false),
                    EntradasJson = table.Column<string>(type: "jsonb", nullable: false),
                    ModeloJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecucoesOtimizacao", x => x.Id);
                    table.CheckConstraint("CK_ExecucoesOtimizacao_Estado", "(\"Estado\" = 'EmCalculo' AND \"Revisao\" = 1) OR (\"Estado\" = 'Finalizada' AND \"Revisao\" = 2)");
                    table.CheckConstraint("CK_ExecucoesOtimizacao_Hash", "length(\"Hash\") = 64 AND length(\"Chave\") > 0");
                    table.CheckConstraint("CK_ExecucoesOtimizacao_Prazo", "\"DeadlineUtc\" > \"CreatedAtUtc\"");
                    table.ForeignKey(
                        name: "FK_ExecucoesOtimizacao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosOtimizacao",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Desfecho = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConteudoJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosOtimizacao", x => x.ExecutionId);
                    table.CheckConstraint("CK_ResultadosOtimizacao_Desfecho", "\"Desfecho\" IN ('SolucaoViavel','InviabilidadeDemonstrada','DadosIncompletos','DadosIncompativeis','LimiteTempo','ErroAlgoritmo','Inconclusivo')");
                    table.ForeignKey(
                        name: "FK_ResultadosOtimizacao_ExecucoesOtimizacao_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "ExecucoesOtimizacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DerivacoesOtimizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReceitaId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConteudoJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DerivacoesOtimizacao", x => x.Id);
                    table.CheckConstraint("CK_DerivacoesOtimizacao_Hash", "length(\"Hash\") = 64 AND length(\"Chave\") > 0");
                    table.ForeignKey(
                        name: "FK_DerivacoesOtimizacao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DerivacoesOtimizacao_ReceitasVersoes_VersaoId",
                        column: x => x.VersaoId,
                        principalTable: "ReceitasVersoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DerivacoesOtimizacao_Receitas_ReceitaId",
                        column: x => x.ReceitaId,
                        principalTable: "Receitas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DerivacoesOtimizacao_ResultadosOtimizacao_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "ResultadosOtimizacao",
                        principalColumn: "ExecutionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DerivacoesOtimizacao_AutorId_Chave",
                table: "DerivacoesOtimizacao",
                columns: new[] { "AutorId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DerivacoesOtimizacao_ExecutionId",
                table: "DerivacoesOtimizacao",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_DerivacoesOtimizacao_ReceitaId",
                table: "DerivacoesOtimizacao",
                column: "ReceitaId");

            migrationBuilder.CreateIndex(
                name: "IX_DerivacoesOtimizacao_VersaoId",
                table: "DerivacoesOtimizacao",
                column: "VersaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesOtimizacao_AutorId_Chave",
                table: "ExecucoesOtimizacao",
                columns: new[] { "AutorId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesOtimizacao_CreatedAtUtc_Id",
                table: "ExecucoesOtimizacao",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesOtimizacao_Estado_DeadlineUtc_Id",
                table: "ExecucoesOtimizacao",
                columns: new[] { "Estado", "DeadlineUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosOtimizacao_Desfecho",
                table: "ResultadosOtimizacao",
                column: "Desfecho");

            migrationBuilder.Sql(OptimizationGuardSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(OptimizationGuardSql.PrepareDown);

            migrationBuilder.DropTable(
                name: "DerivacoesOtimizacao");

            migrationBuilder.DropTable(
                name: "ResultadosOtimizacao");

            migrationBuilder.DropTable(
                name: "ExecucoesOtimizacao");
        }
    }
}
