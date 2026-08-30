using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class ConvertirSuperAdminARol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsSuperAdmin",
                table: "Usuarios");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsSuperAdmin",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
