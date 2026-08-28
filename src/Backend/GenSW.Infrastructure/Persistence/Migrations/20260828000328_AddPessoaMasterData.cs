using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPessoaMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Pessoas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Pessoas",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeFantasia",
                table: "Pessoas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoPessoa",
                table: "Pessoas",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"Pessoas\" SET \"TipoPessoa\" = 1, \"Ativo\" = TRUE WHERE \"TipoPessoa\" IS NULL OR \"Ativo\" IS NULL;");

            migrationBuilder.AlterColumn<bool>(
                name: "Ativo",
                table: "Pessoas",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TipoPessoa",
                table: "Pessoas",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pessoas_TipoPessoa",
                table: "Pessoas",
                sql: "\"TipoPessoa\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Pessoas_TipoPessoa",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "NomeFantasia",
                table: "Pessoas");

            migrationBuilder.DropColumn(
                name: "TipoPessoa",
                table: "Pessoas");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Pessoas",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
