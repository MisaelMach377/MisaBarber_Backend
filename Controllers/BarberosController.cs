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
