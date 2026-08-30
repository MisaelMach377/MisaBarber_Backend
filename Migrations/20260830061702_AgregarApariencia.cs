using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarApariencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorPrimario",
                table: "Negocios",
                type: "text",
                nullable: false,
                defaultValue: "#2563eb");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Negocios",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorPrimario",
                table: "Negocios");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Negocios");
        }
    }
}
