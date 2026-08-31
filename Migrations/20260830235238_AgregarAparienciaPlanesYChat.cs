using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAparienciaPlanesYChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMensajes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteNombre = table.Column<string>(type: "text", nullable: false),
                    AutorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutorNombre = table.Column<string>(type: "text", nullable: false),
                    AutorRol = table.Column<string>(type: "text", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LeidoPorStaff = table.Column<bool>(type: "boolean", nullable: false),
                    LeidoPorCliente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMensajes_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMensajes_NegocioId_ClienteId_FechaEnvio",
                table: "ChatMensajes",
                columns: new[] { "NegocioId", "ClienteId", "FechaEnvio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMensajes");
        }
    }
}
