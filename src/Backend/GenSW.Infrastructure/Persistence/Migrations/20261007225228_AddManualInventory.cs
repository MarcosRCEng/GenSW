using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SequenciaEstoque");

            migrationBuilder.CreateTable(
                name: "EventosEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataOperacional = table.Column<DateOnly>(type: "date", nullable: false),
                    DataObservada = table.Column<DateOnly>(type: "date", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Documento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EventoReferenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Algoritmo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosEstoque", x => x.Id);
                    table.CheckConstraint("CK_EventosEstoque_Data", "\"DataObservada\" IS NULL OR \"DataObservada\" <= \"DataOperacional\"");
                    table.CheckConstraint("CK_EventosEstoque_Sequencia", "\"Sequencia\" > 0");
                    table.ForeignKey(
                        name: "FK_EventosEstoque_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosEstoque_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosEstoque_EventosEstoque_EventoReferenciaId",
                        column: x => x.EventoReferenciaId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistroId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Operacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AntesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DepoisJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoEstoque", x => x.Id);
                    table.CheckConstraint("CK_HistoricoEstoque_Sequencia", "\"Sequencia\" > 0");
                    table.ForeignKey(
                        name: "FK_HistoricoEstoque_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocaisEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PropriedadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Finalidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocaisEstoque", x => x.Id);
                    table.CheckConstraint("CK_LocaisEstoque_Finalidade", "\"Finalidade\" IN ('Ordinario','Segregacao')");
                    table.CheckConstraint("CK_LocaisEstoque_Revisao", "\"Revisao\" > 0");
                    table.ForeignKey(
                        name: "FK_LocaisEstoque_Propriedades_PropriedadeId",
                        column: x => x.PropriedadeId,
                        principalTable: "Propriedades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LotesMateriais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoExterno = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Fonte = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataOrigem = table.Column<DateOnly>(type: "date", nullable: true),
                    Fabricacao = table.Column<DateOnly>(type: "date", nullable: true),
                    Coleta = table.Column<DateOnly>(type: "date", nullable: true),
                    Validade = table.Column<DateOnly>(type: "date", nullable: true),
                    FonteValidade = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResponsavelValidadeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PerfilNutricionalId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversaoItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Aplicabilidade = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesMateriais", x => x.Id);
                    table.CheckConstraint("CK_LotesMateriais_Revisao", "\"Revisao\" > 0");
                    table.CheckConstraint("CK_LotesMateriais_Situacao", "\"Situacao\" IN ('Liberado','Bloqueado','Encerrado')");
                    table.CheckConstraint("CK_LotesMateriais_Unidade", "\"Unidade\" IN ('kg','L','un')");
                    table.CheckConstraint("CK_LotesMateriais_Validade", "\"Validade\" IS NULL OR (\"FonteValidade\" IS NOT NULL AND length(trim(\"FonteValidade\")) > 0 AND \"ResponsavelValidadeId\" IS NOT NULL AND (\"DataOrigem\" IS NULL OR \"Validade\" >= \"DataOrigem\") AND (\"Fabricacao\" IS NULL OR \"Validade\" >= \"Fabricacao\") AND (\"Coleta\" IS NULL OR \"Validade\" >= \"Coleta\"))");
                    table.ForeignKey(
                        name: "FK_LotesMateriais_AspNetUsers_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesMateriais_AspNetUsers_ResponsavelValidadeId",
                        column: x => x.ResponsavelValidadeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesMateriais_ConversoesItens_ConversaoItemId",
                        column: x => x.ConversaoItemId,
                        principalTable: "ConversoesItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesMateriais_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesMateriais_PerfisNutricionais_PerfilNutricionalId",
                        column: x => x.PerfilNutricionalId,
                        principalTable: "PerfisNutricionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComandosEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecursoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashPayload = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StatusHttp = table.Column<int>(type: "integer", nullable: false),
                    ExigeAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RespostaJson = table.Column<string>(type: "text", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistroId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComandosEstoque", x => x.Id);
                    table.CheckConstraint("CK_ComandosEstoque_Sucesso", "\"StatusHttp\" IN (200,201) AND length(\"Chave\") BETWEEN 1 AND 100 AND length(\"HashPayload\") = 64");
                    table.ForeignKey(
                        name: "FK_ComandosEstoque_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComandosEstoque_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PosicoesEstoque",
                columns: table => new
                {
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosicoesEstoque", x => new { x.LoteId, x.LocalId });
                    table.CheckConstraint("CK_PosicoesEstoque_Quantidade", "\"Quantidade\" >= 0");
                    table.CheckConstraint("CK_PosicoesEstoque_Revisao", "\"Revisao\" > 0");
                    table.CheckConstraint("CK_PosicoesEstoque_Unidade", "\"Unidade\" IN ('kg','L','un') AND (\"Unidade\" <> 'un' OR \"Quantidade\" = round(\"Quantidade\",0))");
                    table.ForeignKey(
                        name: "FK_PosicoesEstoque_LocaisEstoque_LocalId",
                        column: x => x.LocalId,
                        principalTable: "LocaisEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosicoesEstoque_LotesMateriais_LoteId",
                        column: x => x.LoteId,
                        principalTable: "LotesMateriais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentosEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    LoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Sentido = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    QuantidadeDeclarada = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    UnidadeDeclarada = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Residuo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SaldoAnterior = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    SaldoPosterior = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ConversaoItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PerfilNutricionalId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosEstoque", x => x.Id);
                    table.CheckConstraint("CK_MovimentosEstoque_Declarado", "\"QuantidadeDeclarada\" ~ '^[0-9]{1,14}(\\.[0-9]{1,6})?$' AND (\"QuantidadeDeclarada\"::numeric > 0 OR (\"TipoEvento\" = 'Ajuste' AND \"QuantidadeDeclarada\"::numeric = 0)) AND (\"UnidadeDeclarada\" <> 'un' OR \"QuantidadeDeclarada\"::numeric = trunc(\"QuantidadeDeclarada\"::numeric)) AND \"Residuo\" ~ '^-?[0-9]+(\\.[0-9]+)?$'");
                    table.CheckConstraint("CK_MovimentosEstoque_Ordinal", "\"Ordinal\" > 0");
                    table.CheckConstraint("CK_MovimentosEstoque_Quantidade", "\"Quantidade\" > 0 AND \"SaldoAnterior\" >= 0 AND \"SaldoPosterior\" >= 0");
                    table.CheckConstraint("CK_MovimentosEstoque_Sentido", "\"Sentido\" IN ('Entrada','Saida') AND \"SaldoPosterior\" = \"SaldoAnterior\" + CASE WHEN \"Sentido\" = 'Entrada' THEN \"Quantidade\" ELSE -\"Quantidade\" END");
                    table.CheckConstraint("CK_MovimentosEstoque_Unidade", "\"Unidade\" IN ('kg','L','un') AND \"UnidadeDeclarada\" IN ('kg','g','L','mL','un') AND (\"Unidade\" <> 'un' OR (\"Quantidade\" = round(\"Quantidade\",0) AND \"SaldoAnterior\" = round(\"SaldoAnterior\",0) AND \"SaldoPosterior\" = round(\"SaldoPosterior\",0)))");
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_ConversoesItens_ConversaoItemId",
                        column: x => x.ConversaoItemId,
                        principalTable: "ConversoesItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_EventosEstoque_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosEstoque",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_PerfisNutricionais_PerfilNutricionalId",
                        column: x => x.PerfilNutricionalId,
                        principalTable: "PerfisNutricionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_PosicoesEstoque_LoteId_LocalId",
                        columns: x => new { x.LoteId, x.LocalId },
                        principalTable: "PosicoesEstoque",
                        principalColumns: new[] { "LoteId", "LocalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComandosEstoque_AutorId_Operacao_RecursoId_Chave",
                table: "ComandosEstoque",
                columns: new[] { "AutorId", "Operacao", "RecursoId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComandosEstoque_EventoId",
                table: "ComandosEstoque",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosEstoque_AutorId",
                table: "EventosEstoque",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosEstoque_EventoReferenciaId",
                table: "EventosEstoque",
                column: "EventoReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosEstoque_ResponsavelId",
                table: "EventosEstoque",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosEstoque_Sequencia",
                table: "EventosEstoque",
                column: "Sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventosEstoque_Tipo_DataOperacional_Id",
                table: "EventosEstoque",
                columns: new[] { "Tipo", "DataOperacional", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoEstoque_AutorId",
                table: "HistoricoEstoque",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoEstoque_RegistroId_Sequencia_Id",
                table: "HistoricoEstoque",
                columns: new[] { "RegistroId", "Sequencia", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoEstoque_Sequencia",
                table: "HistoricoEstoque",
                column: "Sequencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocaisEstoque_Ativo_Nome_Id",
                table: "LocaisEstoque",
                columns: new[] { "Ativo", "Nome", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LocaisEstoque_CodigoNormalizado",
                table: "LocaisEstoque",
                column: "CodigoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocaisEstoque_PropriedadeId",
                table: "LocaisEstoque",
                column: "PropriedadeId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_CodigoNormalizado",
                table: "LotesMateriais",
                column: "CodigoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_ConversaoItemId",
                table: "LotesMateriais",
                column: "ConversaoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_ItemId_Situacao_Validade_Id",
                table: "LotesMateriais",
                columns: new[] { "ItemId", "Situacao", "Validade", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_PerfilNutricionalId",
                table: "LotesMateriais",
                column: "PerfilNutricionalId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_ResponsavelId",
                table: "LotesMateriais",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesMateriais_ResponsavelValidadeId",
                table: "LotesMateriais",
                column: "ResponsavelValidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_ConversaoItemId",
                table: "MovimentosEstoque",
                column: "ConversaoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_EventoId_Ordinal",
                table: "MovimentosEstoque",
                columns: new[] { "EventoId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_ItemId",
                table: "MovimentosEstoque",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_LoteId_LocalId_EventoId",
                table: "MovimentosEstoque",
                columns: new[] { "LoteId", "LocalId", "EventoId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_PerfilNutricionalId",
                table: "MovimentosEstoque",
                column: "PerfilNutricionalId");

            migrationBuilder.CreateIndex(
                name: "UX_MovimentosEstoque_Abertura",
                table: "MovimentosEstoque",
                columns: new[] { "LoteId", "LocalId" },
                unique: true,
                filter: "\"TipoEvento\" = 'Abertura'");

            migrationBuilder.CreateIndex(
                name: "IX_PosicoesEstoque_LocalId",
                table: "PosicoesEstoque",
                column: "LocalId");

            migrationBuilder.Sql(InventoryGuardSql.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InventoryGuardSql.Down);
            migrationBuilder.DropTable(
                name: "ComandosEstoque");

            migrationBuilder.DropTable(
                name: "HistoricoEstoque");

            migrationBuilder.DropTable(
                name: "MovimentosEstoque");

            migrationBuilder.DropTable(
                name: "EventosEstoque");

            migrationBuilder.DropTable(
                name: "PosicoesEstoque");

            migrationBuilder.DropTable(
                name: "LocaisEstoque");

            migrationBuilder.DropTable(
                name: "LotesMateriais");

            migrationBuilder.DropSequence(
                name: "SequenciaEstoque");
        }
    }
}
