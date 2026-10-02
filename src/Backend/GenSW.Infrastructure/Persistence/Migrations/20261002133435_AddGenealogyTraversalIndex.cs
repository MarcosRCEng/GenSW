using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenSW.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenealogyTraversalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FiliacoesAnimal_DescendentesAtivos",
                table: "FiliacoesAnimal",
                columns: new[] { "ProgenitorId", "AnimalId" },
                filter: "\"Ativa\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiliacoesAnimal_DescendentesAtivos",
                table: "FiliacoesAnimal");

        }
    }
}
