using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEggBridge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AncorasConciliacaoOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revisao = table.Column<long>(type: "bigint", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SequenciaInicial = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AncorasConciliacaoOvos", x => x.Id);
                    table.CheckConstraint("CK_AncorasConciliacaoOvos_Revisao", "\"Revisao\">=0");
                    table.ForeignKey(
                        name: "FK_AncorasConciliacaoOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AncorasConciliacaoOvos_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProvasEntradaOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MovimentoEstoqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contagem = table.Column<long>(type: "bigint", nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvasEntradaOvos", x => x.Id);
                    table.UniqueConstraint("AK_ProvasEntradaOvos_Id_MovimentoEstoqueId", x => new { x.Id, x.MovimentoEstoqueId });
                    table.CheckConstraint("CK_ProvasEntradaOvos_Contagem", "\"Contagem\" BETWEEN 1 AND 99999999999999 AND length(trim(\"Evidencia\"))>0");
                    table.ForeignKey(
                        name: "FK_ProvasEntradaOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvasEntradaOvos_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvasEntradaOvos_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvasEntradaOvos_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvasEntradaOvos_MovimentosEstoque_MovimentoEstoqueId",
                        column: x => x.MovimentoEstoqueId,
                        principalTable: "MovimentosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecortesConciliacaoOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisaoAncora = table.Column<long>(type: "bigint", nullable: false),
                    SequenciaAte = table.Column<long>(type: "bigint", nullable: false),
                    Fronteira = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FontesConsultadas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Lacunas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrdemAprovacao = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecortesConciliacaoOvos", x => x.Id);
                    table.CheckConstraint("CK_RecortesConciliacaoOvos_Ordem", "\"OrdemAprovacao\">0");
                    table.CheckConstraint("CK_RecortesConciliacaoOvos_Conferencia", "\"RevisaoAncora\">=0 AND \"SequenciaAte\">=0 AND length(trim(\"Fronteira\"))>0 AND length(trim(\"FontesConsultadas\"))>0 AND length(trim(\"Motivo\"))>0");
                    table.ForeignKey(
                        name: "FK_RecortesConciliacaoOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecortesConciliacaoOvos_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecortesConciliacaoOvos_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DecisoesConciliacaoOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecorteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducaoOvoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decisao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IndividualComprovado = table.Column<bool>(type: "boolean", nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    HashOrigem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrigemUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisoesConciliacaoOvos", x => x.Id);
                    table.CheckConstraint("CK_DecisoesConciliacaoOvos_Decisao", "\"Decisao\" IN ('NaoRecebidoComprovado','RecebidoComEquivalencia','Pendente') AND (\"Decisao\"='Pendente' OR \"IndividualComprovado\")");
                    table.CheckConstraint("CK_DecisoesConciliacaoOvos_Hash", "length(\"HashOrigem\")=64");
                    table.ForeignKey(
                        name: "FK_DecisoesConciliacaoOvos_ProducoesOvos_ProducaoOvoId",
                        column: x => x.ProducaoOvoId,
                        principalTable: "ProducoesOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DecisoesConciliacaoOvos_RecortesConciliacaoOvos_RecorteId",
                        column: x => x.RecorteId,
                        principalTable: "RecortesConciliacaoOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GruposPonteOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecorteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvaId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovimentoEstoqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<long>(type: "bigint", nullable: false),
                    RevisaoLoteOriginal = table.Column<int>(type: "integer", nullable: false),
                    SituacaoOriginal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidadeOriginal = table.Column<DateOnly>(type: "date", nullable: true),
                    SnapshotLoteJson = table.Column<string>(type: "jsonb", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposPonteOvos", x => x.Id);
                    table.CheckConstraint("CK_GruposPonteOvos_Operacao", "(\"Operacao\"='Entrada' AND \"ProvaId\" IS NULL) OR (\"Operacao\"='Associacao' AND \"ProvaId\" IS NOT NULL)");
                    table.CheckConstraint("CK_GruposPonteOvos_Quantidade", "\"Quantidade\" BETWEEN 1 AND 99999999999999");
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_LocaisEstoque_LocalId",
                        column: x => x.LocalId,
                        principalTable: "LocaisEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_LotesMateriais_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_MovimentosEstoque_MovimentoEstoqueId",
                        column: x => x.MovimentoEstoqueId,
                        principalTable: "MovimentosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_ProvasEntradaOvos_ProvaId",
                        column: x => x.ProvaId,
                        principalTable: "ProvasEntradaOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GruposPonteOvos_RecortesConciliacaoOvos_RecorteId",
                        column: x => x.RecorteId,
                        principalTable: "RecortesConciliacaoOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrigensRecebimentoOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecorteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigensRecebimentoOvos", x => x.Id);
                    table.CheckConstraint("CK_OrigensRecebimentoOvos_Evidencia", "length(trim(\"Evidencia\"))>0");
                    table.ForeignKey(
                        name: "FK_OrigensRecebimentoOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrigensRecebimentoOvos_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrigensRecebimentoOvos_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrigensRecebimentoOvos_RecortesConciliacaoOvos_RecorteId",
                        column: x => x.RecorteId,
                        principalTable: "RecortesConciliacaoOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComandosPonteOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashPayload = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExigeAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    StatusHttp = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RespostaJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosPonteOvos", x => x.Id);
                    table.CheckConstraint("CK_ComandosPonteOvos_Chave", "length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\")=64 AND \"StatusHttp\" IN (200,201)");
                    table.ForeignKey(
                        name: "FK_ComandosPonteOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComandosPonteOvos_GruposPonteOvos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GruposPonteOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrigensLoteOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigensLoteOvos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrigensLoteOvos_GruposPonteOvos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GruposPonteOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrigensLoteOvos_LotesMateriais_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PontesProducaoOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducaoOvoId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    HashOrigem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrigemUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PontesProducaoOvos", x => x.Id);
                    table.CheckConstraint("CK_PontesProducaoOvos_Snapshot", "length(\"HashOrigem\")=64 AND \"Algoritmo\"='gensw-egg-bridge/1.0'");
                    table.ForeignKey(
                        name: "FK_PontesProducaoOvos_GruposPonteOvos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GruposPonteOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PontesProducaoOvos_ProducoesOvos_ProducaoOvoId",
                        column: x => x.ProducaoOvoId,
                        principalTable: "ProducoesOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TratamentosPonteOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Evidencia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TratamentosPonteOvos", x => x.Id);
                    table.CheckConstraint("CK_TratamentosPonteOvos_Tipo", "\"Tipo\" IN ('Anotacao','Invalidacao','Compensacao') AND ((\"Tipo\"='Compensacao')=(\"EventoId\" IS NOT NULL)) AND length(trim(\"Evidencia\"))>0");
                    table.ForeignKey(
                        name: "FK_TratamentosPonteOvos_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TratamentosPonteOvos_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TratamentosPonteOvos_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TratamentosPonteOvos_GruposPonteOvos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GruposPonteOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlocacoesProvaOvos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PonteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProvaId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovimentoEstoqueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlocacoesProvaOvos", x => x.Id);
                    table.CheckConstraint("CK_AlocacoesProvaOvos_Ordinal", "\"Ordinal\" BETWEEN 1 AND 99999999999999");
                    table.ForeignKey(
                        name: "FK_AlocacoesProvaOvos_PontesProducaoOvos_PonteId",
                        column: x => x.PonteId,
                        principalTable: "PontesProducaoOvos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlocacoesProvaOvos_ProvasEntradaOvos_ProvaId_MovimentoEstoq~",
                        columns: x => new { x.ProvaId, x.MovimentoEstoqueId },
                        principalTable: "ProvasEntradaOvos",
                        principalColumns: new[] { "Id", "MovimentoEstoqueId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesProvaOvos_MovimentoEstoqueId_Ordinal",
                table: "AlocacoesProvaOvos",
                columns: new[] { "MovimentoEstoqueId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesProvaOvos_PonteId",
                table: "AlocacoesProvaOvos",
                column: "PonteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlocacoesProvaOvos_ProvaId_MovimentoEstoqueId",
                table: "AlocacoesProvaOvos",
                columns: new[] { "ProvaId", "MovimentoEstoqueId" });

            migrationBuilder.CreateIndex(
                name: "IX_AncorasConciliacaoOvos_AutorId",
                table: "AncorasConciliacaoOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_AncorasConciliacaoOvos_ItemId",
                table: "AncorasConciliacaoOvos",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComandosPonteOvos_AutorId",
                table: "ComandosPonteOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosPonteOvos_GrupoId",
                table: "ComandosPonteOvos",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosPonteOvos_Operacao_Chave",
                table: "ComandosPonteOvos",
                columns: new[] { "Operacao", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisoesConciliacaoOvos_ProducaoOvoId",
                table: "DecisoesConciliacaoOvos",
                column: "ProducaoOvoId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisoesConciliacaoOvos_RecorteId_ProducaoOvoId",
                table: "DecisoesConciliacaoOvos",
                columns: new[] { "RecorteId", "ProducaoOvoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_AutorId",
                table: "GruposPonteOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_EventoId",
                table: "GruposPonteOvos",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_ItemId_CreatedAtUtc_Id",
                table: "GruposPonteOvos",
                columns: new[] { "ItemId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_LocalId",
                table: "GruposPonteOvos",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_LoteId",
                table: "GruposPonteOvos",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_MovimentoEstoqueId",
                table: "GruposPonteOvos",
                column: "MovimentoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_ProvaId",
                table: "GruposPonteOvos",
                column: "ProvaId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_RecorteId",
                table: "GruposPonteOvos",
                column: "RecorteId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposPonteOvos_ResponsavelId",
                table: "GruposPonteOvos",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_OrigensLoteOvos_GrupoId",
                table: "OrigensLoteOvos",
                column: "GrupoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrigensLoteOvos_LoteId",
                table: "OrigensLoteOvos",
                column: "LoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrigensRecebimentoOvos_AutorId",
                table: "OrigensRecebimentoOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_OrigensRecebimentoOvos_EventoId",
                table: "OrigensRecebimentoOvos",
                column: "EventoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrigensRecebimentoOvos_ItemId",
                table: "OrigensRecebimentoOvos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrigensRecebimentoOvos_RecorteId",
                table: "OrigensRecebimentoOvos",
                column: "RecorteId");

            migrationBuilder.CreateIndex(
                name: "IX_PontesProducaoOvos_GrupoId",
                table: "PontesProducaoOvos",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_PontesProducaoOvos_ProducaoOvoId",
                table: "PontesProducaoOvos",
                column: "ProducaoOvoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvasEntradaOvos_AutorId",
                table: "ProvasEntradaOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvasEntradaOvos_EventoId",
                table: "ProvasEntradaOvos",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvasEntradaOvos_ItemId",
                table: "ProvasEntradaOvos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvasEntradaOvos_MovimentoEstoqueId",
                table: "ProvasEntradaOvos",
                column: "MovimentoEstoqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvasEntradaOvos_ResponsavelId",
                table: "ProvasEntradaOvos",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_RecortesConciliacaoOvos_AutorId",
                table: "RecortesConciliacaoOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_RecortesConciliacaoOvos_ItemId_CreatedAtUtc_Id",
                table: "RecortesConciliacaoOvos",
                columns: new[] { "ItemId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RecortesConciliacaoOvos_ResponsavelId",
                table: "RecortesConciliacaoOvos",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamentosPonteOvos_AutorId",
                table: "TratamentosPonteOvos",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamentosPonteOvos_EventoId",
                table: "TratamentosPonteOvos",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_TratamentosPonteOvos_GrupoId_Tipo",
                table: "TratamentosPonteOvos",
                columns: new[] { "GrupoId", "Tipo" },
                unique: true,
                filter: "\"Tipo\" IN ('Invalidacao','Compensacao')");

            migrationBuilder.CreateIndex(
                name: "IX_TratamentosPonteOvos_ResponsavelId",
                table: "TratamentosPonteOvos",
                column: "ResponsavelId");
            migrationBuilder.CreateSequence<long>(name: "SequenciaConciliacaoOvos");
            migrationBuilder.CreateIndex(
                name: "IX_RecortesConciliacaoOvos_OrdemAprovacao",
                table: "RecortesConciliacaoOvos",
                column: "OrdemAprovacao",
                unique: true);
            migrationBuilder.Sql(EggBridgeGuardSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN RAISE EXCEPTION 'Egg bridge facts cannot be downgraded destructively; use an authorized forward fix or restore' USING ERRCODE='23514'; END $$;");
        }
    }
}