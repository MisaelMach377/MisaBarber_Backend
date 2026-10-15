using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMovimientosStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockMinimo",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    StockResultante = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_NegocioId",
                table: "MovimientosStock",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_ProductoId_Fecha",
                table: "MovimientosStock",
                columns: new[] { "ProductoId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropColumn(
                name: "StockMinimo",
                table: "Productos");
        }
    }
}
