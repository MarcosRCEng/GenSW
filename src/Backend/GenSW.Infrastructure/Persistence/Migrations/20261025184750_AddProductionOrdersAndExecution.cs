using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrdersAndExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrdensProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceitaVersaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tamanho = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropriedadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PropriedadeNome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    SnapshotPlanejadoJson = table.Column<string>(type: "jsonb", nullable: true),
                    PlanoHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlanejadaEmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IniciadaEmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmadaEmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensProducao", x => x.Id);
                    table.CheckConstraint("CK_OrdensProducao_Estado", "\"Estado\" IN ('Rascunho','Planejada','Iniciada','Confirmada','Cancelada','AbortadaSemConsumo','Revertida')");
                    table.CheckConstraint("CK_OrdensProducao_Revisao", "\"Revisao\">0");
                    table.ForeignKey(
                        name: "FK_OrdensProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdensProducao_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdensProducao_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrdensProducao_ReceitasVersoes_ReceitaVersaoId",
                        column: x => x.ReceitaVersaoId,
                        principalTable: "ReceitasVersoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComandosProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashPayload = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusHttp = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RespostaJson = table.Column<string>(type: "text", nullable: false),
                    ExigeAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosProducao", x => x.Id);
                    table.CheckConstraint("CK_ComandosProducao_Chave", "length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\")=64 AND \"StatusHttp\" IN (200,201)");
                    table.ForeignKey(
                        name: "FK_ComandosProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComandosProducao_OrdensProducao_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecucoesProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    DataObservada = table.Column<DateOnly>(type: "date", nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecucoesProducao", x => x.Id);
                    table.CheckConstraint("CK_ExecucoesProducao_Data", "\"DataObservada\" IS NULL OR \"DataObservada\"<=\"DataOperacional\"");
                    table.CheckConstraint("CK_ExecucoesProducao_Resultado", "\"Resultado\" IN ('Normal','ComDesvio','PerdaTotal','ConsumoIrregular','FatoApto')");
                    table.ForeignKey(
                        name: "FK_ExecucoesProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecucoesProducao_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecucoesProducao_OrdensProducao_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AntesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DepoisJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoProducao", x => x.Id);
                    table.CheckConstraint("CK_HistoricoProducao_Motivo", "length(trim(\"Motivo\"))>0");
                    table.ForeignKey(
                        name: "FK_HistoricoProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricoProducao_OrdensProducao_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OcorrenciasProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataObservada = table.Column<DateOnly>(type: "date", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HouveConsumoFisico = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcorrenciasProducao", x => x.Id);
                    table.CheckConstraint("CK_OcorrenciasProducao_Data", "\"DataObservada\" IS NULL OR \"DataObservada\"<=(\"CreatedAtUtc\" AT TIME ZONE 'America/Sao_Paulo')::date");
                    table.CheckConstraint("CK_OcorrenciasProducao_Evidencia", "length(trim(\"Motivo\"))>0 AND length(trim(\"Evidencia\"))>0");
                    table.ForeignKey(
                        name: "FK_OcorrenciasProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OcorrenciasProducao_OrdensProducao_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QuantidadeOriginal = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    QuantidadeAtiva = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    QuantidadeConsumida = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    QuantidadeLiberada = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasProducao", x => x.Id);
                    table.CheckConstraint("CK_ReservasProducao_Quantidades", "\"QuantidadeOriginal\">0 AND \"QuantidadeAtiva\">=0 AND \"QuantidadeConsumida\">=0 AND \"QuantidadeLiberada\">=0 AND \"QuantidadeOriginal\"=\"QuantidadeAtiva\"+\"QuantidadeConsumida\"+\"QuantidadeLiberada\"");
                    table.CheckConstraint("CK_ReservasProducao_Revisao", "\"Revisao\">0");
                    table.CheckConstraint("CK_ReservasProducao_Unidade", "\"Unidade\" IN ('kg','L','un') AND (\"Unidade\"<>'un' OR (\"QuantidadeOriginal\"=trunc(\"QuantidadeOriginal\") AND \"QuantidadeAtiva\"=trunc(\"QuantidadeAtiva\") AND \"QuantidadeConsumida\"=trunc(\"QuantidadeConsumida\") AND \"QuantidadeLiberada\"=trunc(\"QuantidadeLiberada\")))");
                    table.ForeignKey(
                        name: "FK_ReservasProducao_OrdensProducao_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasProducao_PosicoesEstoque_LoteId_LocalId",
                        columns: x => new { x.LoteId, x.LocalId },
                        principalTable: "PosicoesEstoque",
                        principalColumns: new[] { "LoteId", "LocalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecucaoMovimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlocacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovimentoEstoqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecucaoMovimentos", x => x.Id);
                    table.CheckConstraint("CK_ExecucaoMovimentos_Papel", "\"Papel\" IN ('Entrada','Saida','Compensacao')");
                    table.ForeignKey(
                        name: "FK_ExecucaoMovimentos_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecucaoMovimentos_MovimentosEstoque_MovimentoEstoqueId",
                        column: x => x.MovimentoEstoqueId,
                        principalTable: "MovimentosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GenealogiaMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteEntradaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteSaidaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenealogiaMateriais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GenealogiaMateriais_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenealogiaMateriais_LotesMateriais_LoteEntradaId",
                        column: x => x.LoteEntradaId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenealogiaMateriais_LotesMateriais_LoteSaidaId",
                        column: x => x.LoteSaidaId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReversoesProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReversoesProducao", x => x.Id);
                    table.CheckConstraint("CK_ReversoesProducao_Evidencia", "length(trim(\"Motivo\"))>0 AND length(trim(\"Evidencia\"))>0");
                    table.ForeignKey(
                        name: "FK_ReversoesProducao_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReversoesProducao_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaidasProducao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    RevisaoLoteOriginal = table.Column<int>(type: "integer", nullable: false),
                    SituacaoOriginal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidadeOriginal = table.Column<DateOnly>(type: "date", nullable: true),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaidasProducao", x => x.Id);
                    table.CheckConstraint("CK_SaidasProducao_Quantidade", "\"Quantidade\">0");
                    table.ForeignKey(
                        name: "FK_SaidasProducao_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaidasProducao_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaidasProducao_LocaisEstoque_LocalId",
                        column: x => x.LocalId,
                        principalTable: "LocaisEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaidasProducao_LotesMateriais_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComandosProducao_AutorId",
                table: "ComandosProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosProducao_OrdemId_Operacao_Chave",
                table: "ComandosProducao",
                columns: new[] { "OrdemId", "Operacao", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucaoMovimentos_ExecucaoId",
                table: "ExecucaoMovimentos",
                column: "ExecucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecucaoMovimentos_MovimentoEstoqueId",
                table: "ExecucaoMovimentos",
                column: "MovimentoEstoqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesProducao_AutorId",
                table: "ExecucoesProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesProducao_OrdemId",
                table: "ExecucoesProducao",
                column: "OrdemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesProducao_ResponsavelId",
                table: "ExecucoesProducao",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogiaMateriais_ExecucaoId_LoteEntradaId_LoteSaidaId",
                table: "GenealogiaMateriais",
                columns: new[] { "ExecucaoId", "LoteEntradaId", "LoteSaidaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenealogiaMateriais_LoteEntradaId",
                table: "GenealogiaMateriais",
                column: "LoteEntradaId");

            migrationBuilder.CreateIndex(
                name: "IX_GenealogiaMateriais_LoteSaidaId",
                table: "GenealogiaMateriais",
                column: "LoteSaidaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoProducao_AutorId",
                table: "HistoricoProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoProducao_OrdemId_CreatedAtUtc_Id",
                table: "HistoricoProducao",
                columns: new[] { "OrdemId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OcorrenciasProducao_AutorId",
                table: "OcorrenciasProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_OcorrenciasProducao_OrdemId_CreatedAtUtc_Id",
                table: "OcorrenciasProducao",
                columns: new[] { "OrdemId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_AutorId",
                table: "OrdensProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_CodigoNormalizado",
                table: "OrdensProducao",
                column: "CodigoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_Estado_CreatedAtUtc_Id",
                table: "OrdensProducao",
                columns: new[] { "Estado", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_PropriedadeId",
                table: "OrdensProducao",
                column: "PropriedadeId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_ReceitaVersaoId",
                table: "OrdensProducao",
                column: "ReceitaVersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensProducao_ResponsavelId",
                table: "OrdensProducao",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasProducao_LoteId_LocalId",
                table: "ReservasProducao",
                columns: new[] { "LoteId", "LocalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasProducao_OrdemId_LinhaId",
                table: "ReservasProducao",
                columns: new[] { "OrdemId", "LinhaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReversoesProducao_AutorId",
                table: "ReversoesProducao",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReversoesProducao_ExecucaoId",
                table: "ReversoesProducao",
                column: "ExecucaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaidasProducao_EventoId",
                table: "SaidasProducao",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasProducao_ExecucaoId",
                table: "SaidasProducao",
                column: "ExecucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasProducao_LocalId",
                table: "SaidasProducao",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasProducao_LoteId",
                table: "SaidasProducao",
                column: "LoteId",
                unique: true);
            migrationBuilder.Sql(ProductionGuardSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("F02 conserva fatos físicos e reservas. Use correção aditiva ou restauração coordenada; rollback destrutivo é vedado.");
        }
    }
}
