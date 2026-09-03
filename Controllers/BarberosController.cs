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

    // promedio/total tienen default (null, 0) a propósito -- Create/Update/
    // CambiarEstado llaman a esto justo después de crear o tocar un
    // barbero, momento en el que sus reseñas (si las tuviera de antes) no
    // cambiaron para nada; solo GetAll/GetById necesitan el dato real,
    // y lo calculan con una query de agregación aparte (ver más abajo)
    // en vez de traer TODAS las Resenas a memoria por cada barbero.
    private static BarberoDto ToDto(Barbero b, double? promedio = null, int total = 0) =>
        new(b.Id, b.Nombre, b.Telefono, b.Email, b.FotoUrl, b.Estado, b.FechaCreacion, promedio, total);

    // Una sola query agrupada para N barberos -- evita el N+1 de consultar
    // "las reseñas de este barbero" adentro de un loop. Devuelve un
    // diccionario BarberoId -> (promedio, total) que ya viene limpio: un
    // barbero sin ninguna reseña simplemente no aparece acá (por eso
    // ToDto recibe null como default, no 0 -- ver su comentario).
    private async Task<Dictionary<Guid, (double Promedio, int Total)>> ResumenResenasPorBarbero(IEnumerable<Guid> barberoIds)
    {
        var ids = barberoIds.ToList();
        var filas = await _db.Resenas
            .Where(r => r.NegocioId == NegocioId && ids.Contains(r.BarberoId))
            .GroupBy(r => r.BarberoId)
            .Select(g => new { BarberoId = g.Key, Promedio = g.Average(x => x.Puntuacion), Total = g.Count() })
            .ToListAsync();

        return filas.ToDictionary(f => f.BarberoId, f => (f.Promedio, f.Total));
    }

    private static ResenaDto ToResenaDto(Resena r) => new(
        r.Id,
        r.CitaId,
        r.ClienteId,
        r.Cliente?.Nombre ?? "",
        r.Cliente?.FotoUrl,
        r.BarberoId,
        r.Puntuacion,
        r.Comentario,
        r.FechaCreacion
    );

    [HttpGet]
    public async Task<ActionResult<List<BarberoDto>>> GetAll()
    {
        var lista = await _db.Barberos.Where(b => b.NegocioId == NegocioId).OrderBy(b => b.Nombre).ToListAsync();
        var resumenes = await ResumenResenasPorBarbero(lista.Select(b => b.Id));
        return Ok(lista.Select(b => resumenes.TryGetValue(b.Id, out var r) ? ToDto(b, r.Promedio, r.Total) : ToDto(b)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BarberoDto>> GetById(Guid id)
    {
        var b = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (b is null) return NotFound();
        var resumenes = await ResumenResenasPorBarbero(new[] { b.Id });
        return Ok(resumenes.TryGetValue(b.Id, out var r) ? ToDto(b, r.Promedio, r.Total) : ToDto(b));
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

    // GET api/barberos/{id}/resenas -- promedio + lista de reseñas de este
    // barbero, más recientes primero. Cualquier cuenta logueada del
    // Negocio la puede ver (Admin/Barbero/Cliente, ver [RequiereAuth] a
    // nivel de clase) -- todavía no hay una vista PÚBLICA sin login que
    // muestre esto (ej. para que un cliente potencial vea el rating antes
    // de reservar); si se quiere eso más adelante es un endpoint aparte,
    // sin [RequiereAuth], que no exponga nada más que Puntuacion/
    // Comentario/fecha.
    [HttpGet("{id}/resenas")]
    public async Task<ActionResult<ResumenResenasDto>> GetResenas(Guid id)
    {
        var existe = await _db.Barberos.AnyAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (!existe) return NotFound();

        var resenas = await _db.Resenas
            .Where(r => r.BarberoId == id && r.NegocioId == NegocioId)
            .Include(r => r.Cliente)
            .OrderByDescending(r => r.FechaCreacion)
            .ToListAsync();

        var promedio = resenas.Count > 0 ? resenas.Average(r => r.Puntuacion) : 0;
        return Ok(new ResumenResenasDto(promedio, resenas.Count, resenas.Select(ToResenaDto).ToList()));
    }

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
