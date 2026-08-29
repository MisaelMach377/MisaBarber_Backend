using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AddCitaServicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Servicios_ServicioId",
                table: "Citas");

            migrationBuilder.DropIndex(
                name: "IX_Citas_ServicioId",
                table: "Citas");

            migrationBuilder.DropColumn(
                name: "ServicioId",
                table: "Citas");

            migrationBuilder.CreateTable(
                name: "CitaServicios",
                columns: table => new
                {
                    CitaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicioId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CitaServicios", x => new { x.CitaId, x.ServicioId });
                    table.ForeignKey(
                        name: "FK_CitaServicios_Citas_CitaId",
                        column: x => x.CitaId,
                        principalTable: "Citas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CitaServicios_Servicios_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "Servicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CitaServicios_ServicioId",
                table: "CitaServicios",
                column: "ServicioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CitaServicios");

            migrationBuilder.AddColumn<Guid>(
                name: "ServicioId",
                table: "Citas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Citas_ServicioId",
                table: "Citas",
                column: "ServicioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Servicios_ServicioId",
                table: "Citas",
                column: "ServicioId",
                principalTable: "Servicios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
