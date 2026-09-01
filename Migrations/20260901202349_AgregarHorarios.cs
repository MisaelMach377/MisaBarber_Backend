using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace misabarber.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHorarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HorariosBarbero",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BarberoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    Trabaja = table.Column<bool>(type: "boolean", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "interval", nullable: true),
                    HoraFin = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosBarbero", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorariosBarbero_Barberos_BarberoId",
                        column: x => x.BarberoId,
                        principalTable: "Barberos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HorariosNegocio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NegocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    Abierto = table.Column<bool>(type: "boolean", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "interval", nullable: false),
                    HoraFin = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosNegocio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorariosNegocio_Negocios_NegocioId",
                        column: x => x.NegocioId,
                        principalTable: "Negocios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HorariosBarbero_BarberoId_DiaSemana",
                table: "HorariosBarbero",
                columns: new[] { "BarberoId", "DiaSemana" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HorariosNegocio_NegocioId_DiaSemana",
                table: "HorariosNegocio",
                columns: new[] { "NegocioId", "DiaSemana" },
                unique: true);

            // Backfill para los Negocios/Barberos que ya existían antes de esta
            // migración -- Utils/Horarios.cs siembra las 7 filas para los que se
            // creen de acá en adelante (NegociosController.Create /
            // BarberosController.Create), pero eso no alcanza a los que ya
            // estaban en la base. Sin este backfill, CitasController.
            // GetDisponibilidad los trataría como "sin horario = negocio
            // cerrado todos los días" y nadie podría agendar con nadie.
            // Mismos valores default que ya usaba el código antes de que esto
            // existiera (9am-7pm, todos los días, sin horario propio de
            // barbero) para no cambiarle el comportamiento a nadie de un día
            // para el otro.
            migrationBuilder.Sql(@"
                INSERT INTO ""HorariosNegocio"" (""Id"", ""NegocioId"", ""DiaSemana"", ""Abierto"", ""HoraInicio"", ""HoraFin"")
                SELECT gen_random_uuid(), n.""Id"", dia, true, '09:00:00'::interval, '19:00:00'::interval
                FROM ""Negocios"" n
                CROSS JOIN generate_series(0, 6) AS dia;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""HorariosBarbero"" (""Id"", ""BarberoId"", ""DiaSemana"", ""Trabaja"", ""HoraInicio"", ""HoraFin"")
                SELECT gen_random_uuid(), b.""Id"", dia, true, NULL, NULL
                FROM ""Barberos"" b
                CROSS JOIN generate_series(0, 6) AS dia;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HorariosBarbero");

            migrationBuilder.DropTable(
                name: "HorariosNegocio");
        }
    }
}
