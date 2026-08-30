using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
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
        return Ok(new AparienciaDto(negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario));
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
        await _db.SaveChangesAsync();

        return Ok(new AparienciaDto(negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario));
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
        await _db.SaveChangesAsync();

        return Ok(new RolesDto(elegidos, disponibles));
    }
}
