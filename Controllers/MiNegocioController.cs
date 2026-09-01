using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Cada Admin (o SuperAdmin) configurando la apariencia de SU PROPIA
// barbería -- self-service, sin depender de que el SuperAdmin se lo
// cargue desde NegociosController. Deliberadamente separado de
// NegociosController (que es "el SuperAdmin administrando la plataforma")
// porque acá no hay ningún id en la ruta: siempre opera sobre el
// NegocioId del token de quien llama, nunca sobre otro.
[ApiController]
[Route("api/mi-negocio")]
[RequiereAuth(Rol = "Admin")]
public class MiNegocioController : ControllerBase
{
    private readonly MisaBarberContext _db;

    public MiNegocioController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    // Apariencia es un módulo de Plan Pro (ver Utils/Modulos.cs) -- un
    // negocio en Free no puede ni leer ni guardar su marca por acá, aunque
    // alguien le pegue directo a la API (Layout.jsx ya le esconde el link,
    // esto es el mismo fence pero del lado del servidor).
    private async Task<Negocio?> NegocioConModulo(string modulo)
    {
        var negocio = await _db.Negocios.FindAsync(NegocioId);
        if (negocio is null) return null;
        return Modulos.DeNegocio(negocio.Plan).Contains(modulo) ? negocio : null;
    }

    [HttpGet("apariencia")]
    public async Task<ActionResult<AparienciaDto>> GetApariencia()
    {
        var negocio = await NegocioConModulo("Apariencia");
        if (negocio is null) return NotFound();
        return Ok(new AparienciaDto(negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario, negocio.Slug, negocio.EsPrincipal));
    }

    [HttpPut("apariencia")]
    public async Task<ActionResult<AparienciaDto>> ActualizarApariencia(ActualizarAparienciaDto dto)
    {
        if (!Regex.IsMatch(dto.ColorPrimario ?? "", "^#[0-9a-fA-F]{6}$"))
            return BadRequest("El color debe ser un código hexadecimal válido, ej: #2563eb.");

        var negocio = await NegocioConModulo("Apariencia");
        if (negocio is null) return NotFound();

        negocio.LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl;
        negocio.ColorPrimario = dto.ColorPrimario;
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Negocio", negocio.Id, negocio.Nombre, "Apariencia actualizada");
        await _db.SaveChangesAsync();

        return Ok(new AparienciaDto(negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario, negocio.Slug, negocio.EsPrincipal));
    }

    // "Roles": qué módulos (de los que además permita el Plan, ver
    // Utils/Modulos.cs) puede ver un Barbero de este negocio -- el propio
    // Admin lo configura para su equipo, ver Roles.jsx. ModulosDisponibles
    // viaja en la respuesta para que esa pantalla sepa qué mostrar
    // bloqueado en vez de dejar tildar un módulo que el Plan ni tiene.
    [HttpGet("roles")]
    public async Task<ActionResult<RolesDto>> GetRoles()
    {
        var negocio = await _db.Negocios.FindAsync(NegocioId);
        if (negocio is null) return NotFound();

        var disponibles = Modulos.DeNegocio(negocio.Plan);
        var actuales = (negocio.ModulosBarbero ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Intersect(disponibles)
            .ToArray();

        return Ok(new RolesDto(actuales, disponibles));
    }

    [HttpPut("roles")]
    public async Task<ActionResult<RolesDto>> ActualizarRoles(ActualizarRolesDto dto)
    {
        var negocio = await _db.Negocios.FindAsync(NegocioId);
        if (negocio is null) return NotFound();

        var disponibles = Modulos.DeNegocio(negocio.Plan);
        var elegidos = (dto.ModulosBarbero ?? Array.Empty<string>())
            .Where(m => disponibles.Contains(m)) // nunca se guarda un módulo que el Plan no tiene
            .Distinct()
            .ToArray();

        negocio.ModulosBarbero = string.Join(",", elegidos);
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Negocio", negocio.Id, negocio.Nombre, "Roles del equipo actualizados", elegidos.Length > 0 ? string.Join(", ", elegidos) : "Sin módulos para Barbero");
        await _db.SaveChangesAsync();

        return Ok(new RolesDto(elegidos, disponibles));
    }

    // Horario semanal de atención del negocio -- disponible para
    // cualquier Plan (a diferencia de Apariencia/Roles de acá arriba, ver
    // la conversación con Misael: manejar los horarios de tu propio
    // negocio/equipo no es un feature premium, es operar el día a día),
    // por eso NO pasa por NegocioConModulo. Siempre devuelve las 7 filas
    // -- las siembra Utils/Horarios.cs al crear el Negocio (o el backfill
    // de la migración para los que ya existían), así que si un día no
    // aparece es un bug de seeding, no un caso a contemplar acá.
    [HttpGet("horario")]
    public async Task<ActionResult<List<HorarioNegocioDiaDto>>> GetHorario()
    {
        var dias = await _db.HorariosNegocio
            .Where(h => h.NegocioId == NegocioId)
            .OrderBy(h => h.DiaSemana)
            .ToListAsync();

        return Ok(dias.Select(ToDiaDto));
    }

    [HttpPut("horario")]
    public async Task<ActionResult<List<HorarioNegocioDiaDto>>> ActualizarHorario(ActualizarHorarioNegocioDto dto)
    {
        if (dto.Dias is null || dto.Dias.Count != 7 || dto.Dias.Select(d => d.DiaSemana).Distinct().Count() != 7)
            return BadRequest("Debes enviar los 7 días de la semana, uno de cada uno.");

        var dias = await _db.HorariosNegocio
            .Where(h => h.NegocioId == NegocioId)
            .ToDictionaryAsync(h => h.DiaSemana);

        foreach (var d in dto.Dias)
        {
            if (d.DiaSemana < 0 || d.DiaSemana > 6)
                return BadRequest("Día de la semana inválido.");
            if (!dias.TryGetValue(d.DiaSemana, out var fila))
                return NotFound($"No se encontró el horario del negocio para el día {d.DiaSemana} -- contacta soporte.");

            if (d.Abierto)
            {
                if (!Horarios.TryParseHora(d.HoraInicio, out var inicio) || !Horarios.TryParseHora(d.HoraFin, out var fin) || fin <= inicio)
                    return BadRequest("La hora de inicio debe ser antes que la hora de fin.");
                fila.HoraInicio = inicio;
                fila.HoraFin = fin;
            }

            fila.Abierto = d.Abierto;
        }

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Negocio", NegocioId, "Horario del negocio", "Horario de atención actualizado");
        await _db.SaveChangesAsync();

        var actualizado = await _db.HorariosNegocio.Where(h => h.NegocioId == NegocioId).OrderBy(h => h.DiaSemana).ToListAsync();
        return Ok(actualizado.Select(ToDiaDto));
    }

    private static HorarioNegocioDiaDto ToDiaDto(HorarioNegocio h) =>
        new(h.DiaSemana, h.Abierto, Horarios.FormatHora(h.HoraInicio), Horarios.FormatHora(h.HoraFin));

    // Dirección del local -- mismo criterio que Horario arriba: info
    // operativa disponible en cualquier Plan, no pasa por
    // NegocioConModulo aunque se edite desde la misma pantalla
    // (Apariencia.jsx) que Logo/Color, que sí son Pro.
    [HttpGet("ubicacion")]
    public async Task<ActionResult<UbicacionDto>> GetUbicacion()
    {
        var negocio = await _db.Negocios.FindAsync(NegocioId);
        if (negocio is null) return NotFound();
        return Ok(new UbicacionDto(negocio.Direccion, negocio.Latitud, negocio.Longitud));
    }

    [HttpPut("ubicacion")]
    public async Task<ActionResult<UbicacionDto>> ActualizarUbicacion(ActualizarUbicacionDto dto)
    {
        // Las dos vienen juntas o ninguna -- un solo click en el mapa las
        // fija a la vez (ver MapaUbicacion.jsx), así que si llega una sin
        // la otra es un bug del front, no un caso a tolerar en silencio.
        if ((dto.Latitud is null) != (dto.Longitud is null))
            return BadRequest("Latitud y longitud deben venir juntas.");
        if (dto.Latitud is < -90 or > 90)
            return BadRequest("Latitud fuera de rango.");
        if (dto.Longitud is < -180 or > 180)
            return BadRequest("Longitud fuera de rango.");

        var negocio = await _db.Negocios.FindAsync(NegocioId);
        if (negocio is null) return NotFound();

        negocio.Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? null : dto.Direccion.Trim();
        negocio.Latitud = dto.Latitud;
        negocio.Longitud = dto.Longitud;
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Negocio", negocio.Id, negocio.Nombre, "Ubicación actualizada");
        await _db.SaveChangesAsync();

        return Ok(new UbicacionDto(negocio.Direccion, negocio.Latitud, negocio.Longitud));
    }
}
