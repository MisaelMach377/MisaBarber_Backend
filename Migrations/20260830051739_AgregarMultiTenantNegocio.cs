using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMultiTenantNegocio : Migration
    {
        // Id fijo del negocio "principal" (tu barbería original) -- se
        // inserta acá mismo, en la migración, para que todos los datos que
        // ya existen en la base (Usuarios/Clientes/Barberos/Servicios/
        // Citas/CitasAuditoria) puedan quedar asignados a él de una sola
        // vez a través del DEFAULT de cada columna NegocioId (ver más abajo).
        // Coincide con el que buscará Program.cs al arrancar (por
        // EsPrincipal = true, no por este Id a mano) -- no hace falta que
        // el código C# conozca este valor.
        private static readonly Guid NegocioPrincipalId = new("11111111-1111-1111-1111-111111111111");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.AddColumn<bool>(
                name: "EsSuperAdmin",
                table: "Usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // La tabla Negocios y la fila del negocio principal se crean
            // ANTES de agregar las columnas NegocioId -- así el DEFAULT de
            // cada AddColumn (más abajo) puede apuntar a un Id que ya
            // existe de verdad, y las citas/clientes/etc. que ya tenías
            // quedan asignadas a tu propio negocio automáticamente, sin
            // pasos manuales aparte.
            migrationBuilder.CreateTable(
                name: "Negocios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: true),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Negocios", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Negocios",
                columns: new[] { "Id", "Nombre", "Slug", "EsPrincipal", "Estado", "FechaCreacion" },
                // La columna es "timestamp without time zone" (ver
                // MisaBarberContext.ConfigureConventions) -- el generador de
                // SQL de la migracion no puede armar un literal a partir de
                // un DateTime con Kind = Utc para ese tipo de columna, hay
                // que pasarlo como Unspecified (mismo Kind que ya usa el
                // conversor de la app en tiempo de ejecucion).
                values: new object[] { NegocioPrincipalId, "MisaBarber", null, true, "Activo", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) });

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "Usuarios",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "Servicios",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "Clientes",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "CitasAuditoria",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "Citas",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.AddColumn<Guid>(
                name: "NegocioId",
                table: "Barberos",
                type: "uuid",
                nullable: false,
                defaultValue: NegocioPrincipalId);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NegocioId",
                table: "Usuarios",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NegocioId_Email",
                table: "Usuarios",
                columns: new[] { "NegocioId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servicios_NegocioId",
                table: "Servicios",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_NegocioId",
                table: "Clientes",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_CitasAuditoria_NegocioId",
                table: "CitasAuditoria",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Citas_NegocioId",
                table: "Citas",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Barberos_NegocioId",
                table: "Barberos",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Negocios_Slug",
                table: "Negocios",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Barberos_Negocios_NegocioId",
                table: "Barberos",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Citas_Negocios_NegocioId",
                table: "Citas",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CitasAuditoria_Negocios_NegocioId",
                table: "CitasAuditoria",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Negocios_NegocioId",
                table: "Clientes",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Servicios_Negocios_NegocioId",
                table: "Servicios",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Negocios_NegocioId",
                table: "Usuarios",
                column: "NegocioId",
                principalTable: "Negocios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Barberos_Negocios_NegocioId",
                table: "Barberos");

            migrationBuilder.DropForeignKey(
                name: "FK_Citas_Negocios_NegocioId",
                table: "Citas");

            migrationBuilder.DropForeignKey(
                name: "FK_CitasAuditoria_Negocios_NegocioId",
                table: "CitasAuditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Negocios_NegocioId",
                table: "Clientes");

            migrationBuilder.DropForeignKey(
                name: "FK_Servicios_Negocios_NegocioId",
                table: "Servicios");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Negocios_NegocioId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Negocios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_NegocioId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_NegocioId_Email",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Servicios_NegocioId",
                table: "Servicios");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_NegocioId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_CitasAuditoria_NegocioId",
                table: "CitasAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_Citas_NegocioId",
                table: "Citas");

            migrationBuilder.DropIndex(
                name: "IX_Barberos_NegocioId",
                table: "Barberos");

            migrationBuilder.DropColumn(
                name: "EsSuperAdmin",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "Servicios");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "CitasAuditoria");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "Citas");

            migrationBuilder.DropColumn(
                name: "NegocioId",
                table: "Barberos");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }
    }
}
