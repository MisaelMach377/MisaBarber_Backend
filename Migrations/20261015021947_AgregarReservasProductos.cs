using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReservasProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockReservado",
                table: "Productos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReservasProductos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaVence = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResueltoPorNombre = table.Column<string>(type: "text", nullable: true),
                    Nota = table.Column<string>(type: "text", nullable: true),
                    CreadaPorAdmin = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasProductos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasProductos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasProductos_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasProductos_ClienteId_Estado",
                table: "ReservasProductos",
                columns: new[] { "ClienteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasProductos_NegocioId_Estado_FechaVence",
                table: "ReservasProductos",
                columns: new[] { "NegocioId", "Estado", "FechaVence" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasProductos_ProductoId",
                table: "ReservasProductos",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservasProductos");

            migrationBuilder.DropColumn(
                name: "StockReservado",
                table: "Productos");
        }
    }
}
