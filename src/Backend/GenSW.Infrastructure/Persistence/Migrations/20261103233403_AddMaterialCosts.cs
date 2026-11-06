using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SequenciaCustoMaterial");

            migrationBuilder.CreateTable(
                name: "ApuracoesCustoExecucoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValorConhecido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Cobertura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    CorteFisico = table.Column<long>(type: "bigint", nullable: false),
                    CorteMonetario = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Correlacao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApuracaoOriginalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApuracoesCustoExecucoes", x => x.Id);
                    table.CheckConstraint("CK_ApuracaoMaterial_Dados", "\"ValorConhecido\">=0 AND (\"ValorTotal\" IS NULL OR (\"Cobertura\"='Completo' AND \"ValorTotal\"=\"ValorConhecido\")) AND (\"Cobertura\"='Completo' OR \"ValorTotal\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ApuracoesCustoExecucoes_ApuracoesCustoExecucoes_ApuracaoOri~",
                        column: x => x.ApuracaoOriginalId,
                        principalTable: "ApuracoesCustoExecucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApuracoesCustoExecucoes_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApuracoesCustoExecucoes_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApuracoesCustoExecucoes_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosCustoMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Correlacao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    EventoEstoqueId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecucaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventoOriginalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosCustoMateriais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosCustoMateriais_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosCustoMateriais_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosCustoMateriais_EventosCustoMateriais_EventoOriginalId",
                        column: x => x.EventoOriginalId,
                        principalTable: "EventosCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosCustoMateriais_EventosEstoque_EventoEstoqueId",
                        column: x => x.EventoEstoqueId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosCustoMateriais_ExecucoesProducao_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "ExecucoesProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenciasCustoMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fonte = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReferenciaProtegida = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DataFonte = table.Column<DateOnly>(type: "date", nullable: false),
                    Moeda = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    Metodo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Correlacao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashDocumento = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenciasCustoMateriais", x => x.Id);
                    table.CheckConstraint("CK_EvidenciaMaterial_Dados", "\"Moeda\"='BRL' AND \"Quantidade\">0 AND \"Total\">=0");
                    table.ForeignKey(
                        name: "FK_EvidenciasCustoMateriais_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenciasCustoMateriais_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvidenciasCustoMateriais_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrecosReferenciaMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fonte = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReferenciaProtegida = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DataFonte = table.Column<DateOnly>(type: "date", nullable: false),
                    Moeda = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    QuantidadeBase = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    VigenciaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaFim = table.Column<DateOnly>(type: "date", nullable: true),
                    Aplicabilidade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Correlacao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubstituiId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revogada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecosReferenciaMateriais", x => x.Id);
                    table.CheckConstraint("CK_PrecoMaterial_Dados", "\"Moeda\"='BRL' AND \"QuantidadeBase\">0 AND \"ValorUnitario\">=0 AND (\"VigenciaFim\" IS NULL OR \"VigenciaFim\">\"VigenciaInicio\")");
                    table.ForeignKey(
                        name: "FK_PrecosReferenciaMateriais_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecosReferenciaMateriais_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecosReferenciaMateriais_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecosReferenciaMateriais_PrecosReferenciaMateriais_Substit~",
                        column: x => x.SubstituiId,
                        principalTable: "PrecosReferenciaMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComandosCustoMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashPayload = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExigeAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    StatusHttp = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RespostaJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistroId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosCustoMateriais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComandosCustoMateriais_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComandosCustoMateriais_EventosCustoMateriais_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CamadasCustoMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    QuantidadeOriginal = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ValorConhecido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TipoOrigem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SequenciaOrigem = table.Column<long>(type: "bigint", nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Correlacao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Algoritmo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MovimentoOrigemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SaidaProducaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CamadaReferenciaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamadasCustoMateriais", x => x.Id);
                    table.CheckConstraint("CK_CamadaMaterial_Dados", "\"QuantidadeOriginal\">0 AND \"ValorConhecido\">=0 AND \"Estado\" IN ('Completo','Parcial','Desconhecido') AND \"Unidade\" IN ('kg','L','un') AND (\"Unidade\"<>'un' OR trunc(\"QuantidadeOriginal\")=\"QuantidadeOriginal\")");
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_CamadasCustoMateriais_CamadaReferenci~",
                        column: x => x.CamadaReferenciaId,
                        principalTable: "CamadasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_EvidenciasCustoMateriais_EvidenciaId",
                        column: x => x.EvidenciaId,
                        principalTable: "EvidenciasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_LotesMateriais_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_MovimentosEstoque_MovimentoOrigemId",
                        column: x => x.MovimentoOrigemId,
                        principalTable: "MovimentosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamadasCustoMateriais_SaidasProducao_SaidaProducaoId",
                        column: x => x.SaidaProducaoId,
                        principalTable: "SaidasProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DestinosCustoExecucoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApuracaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ValorConhecido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoUnitarioCompleto = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    Cobertura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    OrdemRateio = table.Column<int>(type: "integer", nullable: false),
                    RestoRateio = table.Column<string>(type: "jsonb", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SaidaProducaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerdaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CamadaSaidaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinosCustoExecucoes", x => x.Id);
                    table.CheckConstraint("CK_DestinoMaterial_Dados", "\"Percentual\" BETWEEN 0 AND 100 AND \"ValorConhecido\">=0 AND \"Tipo\" IN ('Saida','Perda') AND (\"ValorTotal\" IS NULL OR \"Cobertura\"='Completo') AND (\"Tipo\"<>'Perda' OR (\"CustoUnitarioCompleto\" IS NULL AND \"SaidaProducaoId\" IS NULL)) AND (\"Quantidade\">0 OR (\"Percentual\"=0 AND \"CustoUnitarioCompleto\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_DestinosCustoExecucoes_ApuracoesCustoExecucoes_ApuracaoId",
                        column: x => x.ApuracaoId,
                        principalTable: "ApuracoesCustoExecucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DestinosCustoExecucoes_CamadasCustoMateriais_CamadaSaidaId",
                        column: x => x.CamadaSaidaId,
                        principalTable: "CamadasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DestinosCustoExecucoes_SaidasProducao_SaidaProducaoId",
                        column: x => x.SaidaProducaoId,
                        principalTable: "SaidasProducao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParcelasCustoMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoCustoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CamadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sentido = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ValorConhecido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    MovimentoEstoqueId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParcelaOriginalId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelasCustoMateriais", x => x.Id);
                    table.CheckConstraint("CK_ParcelaMaterial_Dados", "\"Quantidade\">0 AND \"ValorConhecido\">=0 AND \"Sentido\" IN ('Entrada','Saida')");
                    table.ForeignKey(
                        name: "FK_ParcelasCustoMateriais_CamadasCustoMateriais_CamadaId",
                        column: x => x.CamadaId,
                        principalTable: "CamadasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParcelasCustoMateriais_EventosCustoMateriais_EventoCustoId",
                        column: x => x.EventoCustoId,
                        principalTable: "EventosCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParcelasCustoMateriais_LocaisEstoque_LocalId",
                        column: x => x.LocalId,
                        principalTable: "LocaisEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParcelasCustoMateriais_MovimentosEstoque_MovimentoEstoqueId",
                        column: x => x.MovimentoEstoqueId,
                        principalTable: "MovimentosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParcelasCustoMateriais_ParcelasCustoMateriais_ParcelaOrigin~",
                        column: x => x.ParcelaOriginalId,
                        principalTable: "ParcelasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosicoesCustoMateriais",
                columns: table => new
                {
                    CamadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ValorConhecido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComponentesJson = table.Column<string>(type: "jsonb", nullable: false),
                    LacunasJson = table.Column<string>(type: "jsonb", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosicoesCustoMateriais", x => new { x.CamadaId, x.LocalId });
                    table.CheckConstraint("CK_PosicaoMaterial_Dados", "\"Quantidade\">=0 AND \"ValorConhecido\">=0 AND (\"Quantidade\">0 OR \"ValorConhecido\"=0) AND \"Estado\" IN ('Completo','Parcial','Desconhecido')");
                    table.ForeignKey(
                        name: "FK_PosicoesCustoMateriais_CamadasCustoMateriais_CamadaId",
                        column: x => x.CamadaId,
                        principalTable: "CamadasCustoMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosicoesCustoMateriais_LocaisEstoque_LocalId",
                        column: x => x.LocalId,
                        principalTable: "LocaisEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApuracoesCustoExecucoes_ApuracaoOriginalId",
                table: "ApuracoesCustoExecucoes",
                column: "ApuracaoOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApuracoesCustoExecucoes_AutorId",
                table: "ApuracoesCustoExecucoes",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ApuracoesCustoExecucoes_ExecucaoId",
                table: "ApuracoesCustoExecucoes",
                column: "ExecucaoId",
                unique: true,
                filter: "\"ApuracaoOriginalId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApuracoesCustoExecucoes_ResponsavelId",
                table: "ApuracoesCustoExecucoes",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_AutorId",
                table: "CamadasCustoMateriais",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_CamadaReferenciaId",
                table: "CamadasCustoMateriais",
                column: "CamadaReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_EvidenciaId",
                table: "CamadasCustoMateriais",
                column: "EvidenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_ItemId",
                table: "CamadasCustoMateriais",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_LoteId_SequenciaOrigem_Id",
                table: "CamadasCustoMateriais",
                columns: new[] { "LoteId", "SequenciaOrigem", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_MovimentoOrigemId",
                table: "CamadasCustoMateriais",
                column: "MovimentoOrigemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_ResponsavelId",
                table: "CamadasCustoMateriais",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_CamadasCustoMateriais_SaidaProducaoId",
                table: "CamadasCustoMateriais",
                column: "SaidaProducaoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComandosCustoMateriais_AutorId",
                table: "ComandosCustoMateriais",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosCustoMateriais_EventoId",
                table: "ComandosCustoMateriais",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComandosCustoMateriais_RecursoId_Operacao_Chave",
                table: "ComandosCustoMateriais",
                columns: new[] { "RecursoId", "Operacao", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DestinosCustoExecucoes_ApuracaoId_DestinoId",
                table: "DestinosCustoExecucoes",
                columns: new[] { "ApuracaoId", "DestinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DestinosCustoExecucoes_CamadaSaidaId",
                table: "DestinosCustoExecucoes",
                column: "CamadaSaidaId");

            migrationBuilder.CreateIndex(
                name: "IX_DestinosCustoExecucoes_SaidaProducaoId",
                table: "DestinosCustoExecucoes",
                column: "SaidaProducaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_AutorId",
                table: "EventosCustoMateriais",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_EventoEstoqueId",
                table: "EventosCustoMateriais",
                column: "EventoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_EventoOriginalId",
                table: "EventosCustoMateriais",
                column: "EventoOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_ExecucaoId",
                table: "EventosCustoMateriais",
                column: "ExecucaoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_ResponsavelId",
                table: "EventosCustoMateriais",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosCustoMateriais_Sequencia",
                table: "EventosCustoMateriais",
                column: "Sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasCustoMateriais_AutorId",
                table: "EvidenciasCustoMateriais",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasCustoMateriais_ItemId",
                table: "EvidenciasCustoMateriais",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasCustoMateriais_ResponsavelId",
                table: "EvidenciasCustoMateriais",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCustoMateriais_CamadaId_LocalId_EventoCustoId",
                table: "ParcelasCustoMateriais",
                columns: new[] { "CamadaId", "LocalId", "EventoCustoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCustoMateriais_EventoCustoId",
                table: "ParcelasCustoMateriais",
                column: "EventoCustoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCustoMateriais_LocalId",
                table: "ParcelasCustoMateriais",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCustoMateriais_MovimentoEstoqueId",
                table: "ParcelasCustoMateriais",
                column: "MovimentoEstoqueId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCustoMateriais_ParcelaOriginalId",
                table: "ParcelasCustoMateriais",
                column: "ParcelaOriginalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosicoesCustoMateriais_LocalId",
                table: "PosicoesCustoMateriais",
                column: "LocalId");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosReferenciaMateriais_AutorId",
                table: "PrecosReferenciaMateriais",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosReferenciaMateriais_ItemId_VigenciaInicio_Id",
                table: "PrecosReferenciaMateriais",
                columns: new[] { "ItemId", "VigenciaInicio", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PrecosReferenciaMateriais_ResponsavelId",
                table: "PrecosReferenciaMateriais",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_PrecosReferenciaMateriais_SubstituiId",
                table: "PrecosReferenciaMateriais",
                column: "SubstituiId",
                unique: true);
            migrationBuilder.Sql(MaterialCostGuardSql.Create());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(MaterialCostGuardSql.BeforeDown());
            migrationBuilder.DropTable(
                name: "ComandosCustoMateriais");

            migrationBuilder.DropTable(
                name: "DestinosCustoExecucoes");

            migrationBuilder.DropTable(
                name: "ParcelasCustoMateriais");

            migrationBuilder.DropTable(
                name: "PosicoesCustoMateriais");

            migrationBuilder.DropTable(
                name: "PrecosReferenciaMateriais");

            migrationBuilder.DropTable(
                name: "ApuracoesCustoExecucoes");

            migrationBuilder.DropTable(
                name: "EventosCustoMateriais");

            migrationBuilder.DropTable(
                name: "CamadasCustoMateriais");

            migrationBuilder.DropTable(
                name: "EvidenciasCustoMateriais");

            migrationBuilder.DropSequence(
                name: "SequenciaCustoMaterial");
            migrationBuilder.Sql(MaterialCostGuardSql.AfterDown());
        }
    }
}
