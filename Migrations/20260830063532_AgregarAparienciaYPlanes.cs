using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAparienciaYPlanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModulosBarbero",
                table: "Negocios",
                type: "text",
                nullable: false,
                defaultValue: "Citas,Clientes,Historial");

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Negocios",
                type: "text",
                nullable: false,
                defaultValue: "Pro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModulosBarbero",
                table: "Negocios");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Negocios");
        }
    }
}
