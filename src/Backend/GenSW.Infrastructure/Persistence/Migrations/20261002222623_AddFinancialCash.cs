using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasFinanceiras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NomeNormalizado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Natureza = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasFinanceiras", x => x.Id);
                    table.CheckConstraint("CK_Categoria_Natureza", "\"Natureza\" IN (1,2)");
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesCaixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    SaldoInicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesCaixa", x => x.Id);
                    table.CheckConstraint("CK_Caixa_Singleton", "\"Id\" = 1 AND EXTRACT(DAY FROM \"DataInicio\") = 1");
                });

            migrationBuilder.CreateTable(
                name: "IdempotenciasFinanceiras",
                columns: table => new
                {
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RespostaJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotenciasFinanceiras", x => new { x.AutorId, x.Operacao, x.Chave });
                });

            migrationBuilder.CreateTable(
                name: "MesesCaixa",
                columns: table => new
                {
                    Mes = table.Column<DateOnly>(type: "date", nullable: false),
                    Fechado = table.Column<bool>(type: "boolean", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MesesCaixa", x => x.Mes);
                });

            migrationBuilder.CreateTable(
                name: "LancamentosCaixa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    DataMovimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FormaPagamento = table.Column<int>(type: "integer", nullable: false),
                    PessoaId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnimalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Cancelado = table.Column<bool>(type: "boolean", nullable: false),
                    Versao = table.Column<int>(type: "integer", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Origem = table.Column<int>(type: "integer", nullable: false),
                    LancamentoOriginalId = table.Column<Guid>(type: "uuid", nullable: true),
                    MotivoAjuste = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LancamentosCaixa", x => x.Id);
                    table.CheckConstraint("CK_Lancamento_Enums", "\"Tipo\" IN (1,2) AND \"FormaPagamento\" BETWEEN 1 AND 5 AND \"Origem\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Lancamento_Origem", "(\"Origem\" = 1 AND \"LancamentoOriginalId\" IS NULL) OR (\"Origem\" <> 1 AND \"LancamentoOriginalId\" IS NOT NULL AND length(trim(\"MotivoAjuste\")) > 0)");
                    table.CheckConstraint("CK_Lancamento_Valor", "\"Valor\" > 0");
                    table.ForeignKey(
                        name: "FK_LancamentosCaixa_Animais_AnimalId",
                        column: x => x.AnimalId,
                        principalTable: "Animais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LancamentosCaixa_CategoriasFinanceiras_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LancamentosCaixa_LancamentosCaixa_LancamentoOriginalId",
                        column: x => x.LancamentoOriginalId,
                        principalTable: "LancamentosCaixa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LancamentosCaixa_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FechamentosCaixa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mes = table.Column<DateOnly>(type: "date", nullable: false),
                    SaldoAbertura = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Receitas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Despesas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoFinal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantidadeReceitas = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeDespesas = table.Column<int>(type: "integer", nullable: false),
                    SaldoConferido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FechamentosCaixa_MesesCaixa_Mes",
                        column: x => x.Mes,
                        principalTable: "MesesCaixa",
                        principalColumn: "Mes",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriasLancamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AntesJson = table.Column<string>(type: "text", nullable: true),
                    DepoisJson = table.Column<string>(type: "text", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasLancamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriasLancamento_LancamentosCaixa_LancamentoId",
                        column: x => x.LancamentoId,
                        principalTable: "LancamentosCaixa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CategoriasFinanceiras",
                columns: new[] { "Id", "Ativa", "Codigo", "CreatedAtUtc", "Natureza", "Nome", "NomeNormalizado", "UpdatedAtUtc", "Versao" },
                values: new object[,]
                {
                    { new Guid("37600000-0000-0000-0000-000000000001"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Insumos", "INSUMOS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000002"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Alimentação", "ALIMENTAÇÃO", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000003"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Saúde animal", "SAÚDE ANIMAL", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000004"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Serviços", "SERVIÇOS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000005"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, "Outras despesas", "OUTRAS DESPESAS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000006"), true, "VENDA_ANIMAIS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "Venda de animais", "VENDA DE ANIMAIS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 },
                    { new Guid("37600000-0000-0000-0000-000000000007"), true, null, new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, "Outras receitas", "OUTRAS RECEITAS", new DateTimeOffset(new DateTime(2026, 10, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasLancamento_LancamentoId_CreatedAtUtc",
                table: "AuditoriasLancamento",
                columns: new[] { "LancamentoId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinanceiras_Codigo",
                table: "CategoriasFinanceiras",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasFinanceiras_Natureza_NomeNormalizado",
                table: "CategoriasFinanceiras",
                columns: new[] { "Natureza", "NomeNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosCaixa_Mes",
                table: "FechamentosCaixa",
                column: "Mes",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosCaixa_AnimalId",
                table: "LancamentosCaixa",
                column: "AnimalId");

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosCaixa_CategoriaId",
                table: "LancamentosCaixa",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosCaixa_DataMovimento_Id",
                table: "LancamentosCaixa",
                columns: new[] { "DataMovimento", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LancamentosCaixa_PessoaId",
                table: "LancamentosCaixa",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "UX_Caixa_ReversaoOriginal",
                table: "LancamentosCaixa",
                column: "LancamentoOriginalId",
                unique: true,
                filter: "\"Origem\" = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasLancamento");

            migrationBuilder.DropTable(
                name: "ConfiguracoesCaixa");

            migrationBuilder.DropTable(
                name: "FechamentosCaixa");

            migrationBuilder.DropTable(
                name: "IdempotenciasFinanceiras");

            migrationBuilder.DropTable(
                name: "LancamentosCaixa");

            migrationBuilder.DropTable(
                name: "MesesCaixa");

            migrationBuilder.DropTable(
                name: "CategoriasFinanceiras");
        }
    }
}
