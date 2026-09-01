using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEmailBarberoYAuditoriaGeneral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Barberos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditoriaGeneral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Entidad = table.Column<string>(type: "text", nullable: false),
                    EntidadId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntidadNombre = table.Column<string>(type: "text", nullable: false),
                    Accion = table.Column<string>(type: "text", nullable: false),
                    Detalle = table.Column<string>(type: "text", nullable: true),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorNombre = table.Column<string>(type: "text", nullable: false),
                    FechaHoraEvento = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaGeneral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaGeneral_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaGeneral_Entidad",
                table: "AuditoriaGeneral",
                column: "Entidad");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaGeneral_NegocioId_FechaHoraEvento",
                table: "AuditoriaGeneral",
                columns: new[] { "NegocioId", "FechaHoraEvento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaGeneral");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Barberos");
        }
    }
}
