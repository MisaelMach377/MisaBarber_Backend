using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class PromoverSuperAdminPrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Con el paso de EsSuperAdmin (bool oculto) a Rol = "SuperAdmin" (opcion
            // real en el select), esta migracion promueve al Admin de la barberia
            // principal (EsPrincipal = true) para que quede al menos una cuenta
            // SuperAdmin -- la migracion anterior (ConvertirSuperAdminARol) ya borro
            // la columna vieja sin hacer este backfill.
            migrationBuilder.Sql(@"
                UPDATE ""Usuarios""
                SET ""Rol"" = 'SuperAdmin'
                WHERE ""Rol"" = 'Admin'
                  AND ""NegocioId"" = (SELECT ""Id"" FROM ""Negocios"" WHERE ""EsPrincipal"" = true LIMIT 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Usuarios""
                SET ""Rol"" = 'Admin'
                WHERE ""Rol"" = 'SuperAdmin'
                  AND ""NegocioId"" = (SELECT ""Id"" FROM ""Negocios"" WHERE ""EsPrincipal"" = true LIMIT 1);
            ");
        }
    }
}
