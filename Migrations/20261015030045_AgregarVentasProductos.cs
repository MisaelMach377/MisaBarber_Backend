using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarVentasProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvisoVencimientoEnviado",
                table: "ReservasProductos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VentasProductos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductoNombre = table.Column<string>(type: "text", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClienteNombre = table.Column<string>(type: "text", nullable: true),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    ReservaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentasProductos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentasProductos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VentasProductos_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentasProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VentasProductos_ClienteId",
                table: "VentasProductos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_VentasProductos_NegocioId_Fecha",
                table: "VentasProductos",
                columns: new[] { "NegocioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_VentasProductos_ProductoId",
                table: "VentasProductos",
                column: "ProductoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VentasProductos");

            migrationBuilder.DropColumn(
                name: "AvisoVencimientoEnviado",
                table: "ReservasProductos");
        }
    }
}
