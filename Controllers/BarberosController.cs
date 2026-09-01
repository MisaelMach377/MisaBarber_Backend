using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/barberos")]
[RequiereAuth]
public class BarberosController : ControllerBase
{
    private static readonly string[] EstadosValidos = { "Activo", "Inactivo" };

    private readonly MisaBarberContext _db;

    public BarberosController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static BarberoDto ToDto(Barbero b) => new(b.Id, b.Nombre, b.Telefono, b.Email, b.FotoUrl, b.Estado, b.FechaCreacion);

    [HttpGet]
    public async Task<ActionResult<List<BarberoDto>>> GetAll()
    {
        var lista = await _db.Barberos.Where(b => b.NegocioId == NegocioId).OrderBy(b => b.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BarberoDto>> GetById(Guid id)
    {
        var b = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (b is null) return NotFound();
        return Ok(ToDto(b));
    }

    [HttpPost]
    public async Task<ActionResult<BarberoDto>> Create(BarberoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");

        if (!Validaciones.TelefonoValido(dto.Telefono))
            return BadRequest("El teléfono debe tener máximo 9 dígitos numéricos.");

        var barbero = new Barbero
        {
            NegocioId = NegocioId,
            Nombre = dto.Nombre,
            Telefono = dto.Telefono,
            Email = dto.Email,
            FotoUrl = dto.FotoUrl,
        };

        _db.Barberos.Add(barbero);
        _db.HorariosBarbero.AddRange(Horarios.SembrarBarbero(barbero.Id));
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Barbero", barbero.Id, barbero.Nombre, "Creado");
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = barbero.Id }, ToDto(barbero));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<BarberoDto>> Update(Guid id, BarberoCreateDto dto)
    {
        var b = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (b is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");

        if (!Validaciones.TelefonoValido(dto.Telefono))
            return BadRequest("El teléfono debe tener máximo 9 dígitos numéricos.");

        b.Nombre = dto.Nombre;
        b.Telefono = dto.Telefono;
        b.Email = dto.Email;
        if (dto.FotoUrl is not null)
            b.FotoUrl = dto.FotoUrl;

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Barbero", b.Id, b.Nombre, "Editado");
        await _db.SaveChangesAsync();
        return Ok(ToDto(b));
    }

    [HttpPut("{id}/estado")]
    public async Task<ActionResult<BarberoDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var b = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (b is null) return NotFound();

        var estadoAnterior = b.Estado;
        b.Estado = dto.Estado;
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Barbero", b.Id, b.Nombre, $"Estado: {estadoAnterior} -> {dto.Estado}");
        await _db.SaveChangesAsync();
        return Ok(ToDto(b));
    }

    // Horario semanal de UN barbero -- lo edita solo el Admin (ver
    // Barberos.jsx, pestaña Horarios), por eso estos dos endpoints SÍ
    // llevan [RequiereAuth(Rol = "Admin")] puntual, a diferencia del resto
    // del controller que hereda el [RequiereAuth] de la clase (cualquier
    // rol autenticado del negocio, ver Utils/RequiereAuthAttribute.cs).
    [HttpGet("{id}/horario")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<List<HorarioBarberoDiaDto>>> GetHorario(Guid id)
    {
        var existe = await _db.Barberos.AnyAsync(b => b.Id == id && b.NegocioId == NegocioId);
        if (!existe) return NotFound();

        var dias = await _db.HorariosBarbero
            .Where(h => h.BarberoId == id)
            .OrderBy(h => h.DiaSemana)
            .ToListAsync();

        return Ok(dias.Select(ToDiaDto));
    }

    [HttpPut("{id}/horario")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<List<HorarioBarberoDiaDto>>> ActualizarHorario(Guid id, ActualizarHorarioBarberoDto dto)
    {
        var barbero = await _db.Barberos.FirstOrDefaultAsync(b => b.Id == id && b.NegocioId == NegocioId);
        if (barbero is null) return NotFound();

        if (dto.Dias is null || dto.Dias.Count != 7 || dto.Dias.Select(d => d.DiaSemana).Distinct().Count() != 7)
            return BadRequest("Debes enviar los 7 días de la semana, uno de cada uno.");

        var dias = await _db.HorariosBarbero.Where(h => h.BarberoId == id).ToDictionaryAsync(h => h.DiaSemana);

        foreach (var d in dto.Dias)
        {
            if (d.DiaSemana < 0 || d.DiaSemana > 6)
                return BadRequest("Día de la semana inválido.");
            if (!dias.TryGetValue(d.DiaSemana, out var fila))
                return NotFound($"No se encontró el horario para el día {d.DiaSemana} -- contacta soporte.");

            if (d.Trabaja && (d.HoraInicio is not null || d.HoraFin is not null))
            {
                // Ambas o ninguna: no tiene sentido un horario propio a medias.
                if (!Horarios.TryParseHora(d.HoraInicio, out var inicio) || !Horarios.TryParseHora(d.HoraFin, out var fin) || fin <= inicio)
                    return BadRequest("Si le pones un horario propio a este barbero, la hora de inicio debe ser antes que la de fin.");
                fila.HoraInicio = inicio;
                fila.HoraFin = fin;
            }
            else
            {
                // Sin horario propio (o no trabaja ese día) -- vuelve a "usa el del negocio".
                fila.HoraInicio = null;
                fila.HoraFin = null;
            }

            fila.Trabaja = d.Trabaja;
        }

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Barbero", barbero.Id, barbero.Nombre, "Horario actualizado");
        await _db.SaveChangesAsync();

        var actualizado = await _db.HorariosBarbero.Where(h => h.BarberoId == id).OrderBy(h => h.DiaSemana).ToListAsync();
        return Ok(actualizado.Select(ToDiaDto));
    }

    private static HorarioBarberoDiaDto ToDiaDto(HorarioBarbero h) =>
        new(h.DiaSemana, h.Trabaja, h.HoraInicio is TimeSpan hi ? Horarios.FormatHora(hi) : null, h.HoraFin is TimeSpan hf ? Horarios.FormatHora(hf) : null);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var b = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (b is null) return NotFound();

        var tieneHistorial = await _db.Citas.AnyAsync(x => x.BarberoId == id);
        if (tieneHistorial)
            return BadRequest("No puedes eliminar a este barbero porque ya tiene citas registradas. Puedes marcarlo como Inactivo en su lugar.");

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Barbero", b.Id, b.Nombre, "Eliminado");
        _db.Barberos.Remove(b);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
