using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Auditoría general de acciones administrativas (Barberos, Clientes,
// Usuarios, Mi Negocio) -- ver Models/AuditoriaGeneral.cs. Distinta de
// /api/citas/auditoria (CitasController), que es específica del dominio
// de Citas y ya existía antes de esta -- se mantienen separadas en vez de
// fusionarlas para no forzar una forma genérica sobre CitaAuditoria (que
// ya tiene su propia pantalla en Historial.jsx, tab "Auditoría"). Mismo
// criterio de acceso que el resto de Historial: solo Admin, y solo si el
// Plan de la barbería incluye el módulo "Historial" (ver Modulos.cs).
[ApiController]
[Route("api/auditoria")]
[RequiereAuth(Rol = "Admin")]
public class AuditoriaController : ControllerBase
{
    private readonly MisaBarberContext _db;

    public AuditoriaController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static AuditoriaGeneralDto ToDto(AuditoriaGeneral a) => new(
        a.Id, a.Entidad, a.EntidadId, a.EntidadNombre, a.Accion, a.Detalle, a.AutorNombre, a.FechaHoraEvento
    );

    private async Task<bool> PlanPermiteHistorial()
    {
        var plan = await _db.Negocios.Where(n => n.Id == NegocioId).Select(n => n.Plan).FirstOrDefaultAsync();
        return Modulos.DeNegocio(plan).Contains("Historial");
    }

    // Log de eventos (creado / editado / cambio de estado / eliminado)
    // sobre Barberos, Clientes, Usuarios y Mi Negocio, más reciente
    // primero. Independiente de si la ficha todavía existe.
    [HttpGet]
    public async Task<ActionResult<List<AuditoriaGeneralDto>>> GetAll(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? entidad)
    {
        if (!await PlanPermiteHistorial()) return Forbid();

        var query = _db.AuditoriaGeneral.Where(a => a.NegocioId == NegocioId);

        if (desde.HasValue)
            query = query.Where(a => a.FechaHoraEvento >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(a => a.FechaHoraEvento < hasta.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(entidad))
            query = query.Where(a => a.Entidad == entidad);

        var lista = await query
            .OrderByDescending(a => a.FechaHoraEvento)
            .Take(500)
            .ToListAsync();

        return Ok(lista.Select(ToDto));
    }
}
