using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAndFormulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NomeNormalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasItens", x => x.Id);
                    table.CheckConstraint("CK_CategoriasItens_Revisao", "\"Revisao\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "HistoricoFormulacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistroId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Operacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AntesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DepoisJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoFormulacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receitas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Finalidade = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receitas", x => x.Id);
                    table.CheckConstraint("CK_Receitas_Revisao", "\"Revisao\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "SnapshotsFormulacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConteudoJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotsFormulacao", x => x.Id);
                    table.CheckConstraint("CK_SnapshotsFormulacao_Tipo", "\"Tipo\" IN ('Simulacao','Comparacao')");
                });

            migrationBuilder.CreateTable(
                name: "Itens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CategoriaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Classe = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PodeEntrar = table.Column<bool>(type: "boolean", nullable: false),
                    PodeProduzir = table.Column<bool>(type: "boolean", nullable: false),
                    UsoInterno = table.Column<bool>(type: "boolean", nullable: false),
                    Venda = table.Column<bool>(type: "boolean", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    UnidadeFixada = table.Column<bool>(type: "boolean", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Itens", x => x.Id);
                    table.CheckConstraint("CK_Itens_Classe", "\"Classe\" IN ('Alimentar', 'OutroMaterialIncorporado', 'Embalagem', 'Consumivel')");
                    table.CheckConstraint("CK_Itens_Revisao", "\"Revisao\" > 0");
                    table.CheckConstraint("CK_Itens_Unidade", "\"Unidade\" IN ('kg', 'L', 'un')");
                    table.ForeignKey(
                        name: "FK_Itens_CategoriasItens_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceitasVersoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceitaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConteudoJson = table.Column<string>(type: "jsonb", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceitasVersoes", x => x.Id);
                    table.CheckConstraint("CK_ReceitasVersoes_Estado", "\"Estado\" IN ('Rascunho','Publicado','Inativo')");
                    table.CheckConstraint("CK_ReceitasVersoes_Versao", "\"Numero\" > 0 AND \"Revisao\" > 0");
                    table.ForeignKey(
                        name: "FK_ReceitasVersoes_Receitas_ReceitaId",
                        column: x => x.ReceitaId,
                        principalTable: "Receitas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversoesItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Origem = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Destino = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Fator = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Fonte = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Metodo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataFonte = table.Column<DateOnly>(type: "date", nullable: false),
                    Contexto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenciaAmostra = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Proveniencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversoesItens", x => x.Id);
                    table.CheckConstraint("CK_ConversoesItens_Fator", "\"Fator\" > 0");
                    table.CheckConstraint("CK_ConversoesItens_Numero", "\"Numero\" > 0");
                    table.CheckConstraint("CK_ConversoesItens_Unidades", "\"Origem\" IN ('kg','g','L','mL','un') AND \"Destino\" IN ('kg','g','L','mL','un') AND \"Origem\" <> \"Destino\"");
                    table.ForeignKey(
                        name: "FK_ConversoesItens_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerfisNutricionais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Revisao = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConteudoJson = table.Column<string>(type: "jsonb", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisNutricionais", x => x.Id);
                    table.CheckConstraint("CK_PerfisNutricionais_Estado", "\"Estado\" IN ('Rascunho','Publicado','Inativo')");
                    table.CheckConstraint("CK_PerfisNutricionais_Versao", "\"Numero\" > 0 AND \"Revisao\" > 0");
                    table.ForeignKey(
                        name: "FK_PerfisNutricionais_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceitasReferencias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PerfilId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubVersaoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceitasReferencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceitasReferencias_ConversoesItens_ConversaoId",
                        column: x => x.ConversaoId,
                        principalTable: "ConversoesItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceitasReferencias_Itens_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Itens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceitasReferencias_PerfisNutricionais_PerfilId",
                        column: x => x.PerfilId,
                        principalTable: "PerfisNutricionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceitasReferencias_ReceitasVersoes_SubVersaoId",
                        column: x => x.SubVersaoId,
                        principalTable: "ReceitasVersoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceitasReferencias_ReceitasVersoes_VersaoId",
                        column: x => x.VersaoId,
                        principalTable: "ReceitasVersoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasItens_NomeNormalizado",
                table: "CategoriasItens",
                column: "NomeNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversoesItens_ItemId_Numero",
                table: "ConversoesItens",
                columns: new[] { "ItemId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoFormulacao_RegistroId_CreatedAtUtc_Id",
                table: "HistoricoFormulacao",
                columns: new[] { "RegistroId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Itens_Ativo_Nome_Id",
                table: "Itens",
                columns: new[] { "Ativo", "Nome", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Itens_CategoriaId",
                table: "Itens",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Itens_CodigoNormalizado",
                table: "Itens",
                column: "CodigoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerfisNutricionais_ItemId_Numero",
                table: "PerfisNutricionais",
                columns: new[] { "ItemId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receitas_Ativo_Nome_Id",
                table: "Receitas",
                columns: new[] { "Ativo", "Nome", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Receitas_CodigoNormalizado",
                table: "Receitas",
                column: "CodigoNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasReferencias_ConversaoId",
                table: "ReceitasReferencias",
                column: "ConversaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasReferencias_ItemId",
                table: "ReceitasReferencias",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasReferencias_PerfilId",
                table: "ReceitasReferencias",
                column: "PerfilId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasReferencias_SubVersaoId",
                table: "ReceitasReferencias",
                column: "SubVersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasReferencias_VersaoId",
                table: "ReceitasReferencias",
                column: "VersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasVersoes_ReceitaId_Numero",
                table: "ReceitasVersoes",
                columns: new[] { "ReceitaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotsFormulacao_AutorId_Tipo_Chave",
                table: "SnapshotsFormulacao",
                columns: new[] { "AutorId", "Tipo", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotsFormulacao_Tipo_CreatedAtUtc_Id",
                table: "SnapshotsFormulacao",
                columns: new[] { "Tipo", "CreatedAtUtc", "Id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION "FormulationImmutable"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Published formulation record is immutable' USING ERRCODE = '23514';
                END; $$;
                CREATE TRIGGER "TR_SnapshotsFormulacao_Immutable" BEFORE UPDATE OR DELETE ON "SnapshotsFormulacao"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
                CREATE TRIGGER "TR_HistoricoFormulacao_Immutable" BEFORE UPDATE OR DELETE ON "HistoricoFormulacao"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
                CREATE TRIGGER "TR_ConversoesItens_Immutable" BEFORE UPDATE OR DELETE ON "ConversoesItens"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
                CREATE TRIGGER "TR_ReceitasReferencias_Immutable" BEFORE UPDATE OR DELETE ON "ReceitasReferencias"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
                CREATE FUNCTION "FormulationVersionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."Estado" <> 'Rascunho' THEN
                        IF TG_OP = 'DELETE' THEN
                            RAISE EXCEPTION 'Published version cannot be deleted' USING ERRCODE = '23514';
                        END IF;
                        IF OLD."Estado" <> 'Publicado' OR NEW."Estado" <> 'Inativo'
                            OR (to_jsonb(NEW) - 'Estado' - 'Revisao') <> (to_jsonb(OLD) - 'Estado' - 'Revisao')
                            OR NEW."Revisao" <> OLD."Revisao" + 1 THEN
                            RAISE EXCEPTION 'Published version is immutable' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER "TR_PerfisNutricionais_VersionGuard" BEFORE UPDATE OR DELETE ON "PerfisNutricionais"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationVersionGuard"();
                CREATE TRIGGER "TR_ReceitasVersoes_VersionGuard" BEFORE UPDATE OR DELETE ON "ReceitasVersoes"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationVersionGuard"();
                CREATE FUNCTION "FormulationItemUnitGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD."UnidadeFixada" AND (NOT NEW."UnidadeFixada" OR NEW."Unidade" <> OLD."Unidade") THEN
                        RAISE EXCEPTION 'Published canonical unit is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER "TR_Itens_UnitGuard" BEFORE UPDATE ON "Itens"
                    FOR EACH ROW EXECUTE FUNCTION "FormulationItemUnitGuard"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_SnapshotsFormulacao_Immutable" ON "SnapshotsFormulacao";
                DROP TRIGGER "TR_HistoricoFormulacao_Immutable" ON "HistoricoFormulacao";
                DROP TRIGGER "TR_ConversoesItens_Immutable" ON "ConversoesItens";
                DROP TRIGGER "TR_ReceitasReferencias_Immutable" ON "ReceitasReferencias";
                DROP TRIGGER "TR_PerfisNutricionais_VersionGuard" ON "PerfisNutricionais";
                DROP TRIGGER "TR_ReceitasVersoes_VersionGuard" ON "ReceitasVersoes";
                DROP TRIGGER "TR_Itens_UnitGuard" ON "Itens";
                DROP FUNCTION "FormulationImmutable"();
                DROP FUNCTION "FormulationVersionGuard"();
                DROP FUNCTION "FormulationItemUnitGuard"();
                """);
            migrationBuilder.DropTable(
                name: "HistoricoFormulacao");

            migrationBuilder.DropTable(
                name: "ReceitasReferencias");

            migrationBuilder.DropTable(
                name: "SnapshotsFormulacao");

            migrationBuilder.DropTable(
                name: "ConversoesItens");

            migrationBuilder.DropTable(
                name: "PerfisNutricionais");

            migrationBuilder.DropTable(
                name: "ReceitasVersoes");

            migrationBuilder.DropTable(
                name: "Itens");

            migrationBuilder.DropTable(
                name: "Receitas");

            migrationBuilder.DropTable(
                name: "CategoriasItens");
        }
    }
}
