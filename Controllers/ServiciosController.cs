using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/servicios")]
[RequiereAuth]
public class ServiciosController : ControllerBase
{
    private static readonly string[] EstadosValidos = { "Activo", "Inactivo" };

    private readonly MisaBarberContext _db;

    public ServiciosController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static ServicioDto ToDto(Servicio s) => new(s.Id, s.Nombre, s.Descripcion, s.Precio, s.DuracionMinutos, s.Estado, s.FechaCreacion);

    [HttpGet]
    public async Task<ActionResult<List<ServicioDto>>> GetAll()
    {
        var lista = await _db.Servicios.Where(s => s.NegocioId == NegocioId).OrderBy(s => s.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServicioDto>> GetById(Guid id)
    {
        var s = await _db.Servicios.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (s is null) return NotFound();
        return Ok(ToDto(s));
    }

    [HttpPost]
    public async Task<ActionResult<ServicioDto>> Create(ServicioCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");
        if (dto.Precio < 0)
            return BadRequest("El precio no puede ser negativo.");
        if (dto.DuracionMinutos <= 0)
            return BadRequest("La duración tiene que ser mayor a 0.");

        var servicio = new Servicio
        {
            NegocioId = NegocioId,
            Nombre = dto.Nombre,
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            Precio = dto.Precio,
            DuracionMinutos = dto.DuracionMinutos,
        };

        _db.Servicios.Add(servicio);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = servicio.Id }, ToDto(servicio));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ServicioDto>> Update(Guid id, ServicioCreateDto dto)
    {
        var s = await _db.Servicios.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (s is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");
        if (dto.Precio < 0)
            return BadRequest("El precio no puede ser negativo.");
        if (dto.DuracionMinutos <= 0)
            return BadRequest("La duración tiene que ser mayor a 0.");

        s.Nombre = dto.Nombre;
        s.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        s.Precio = dto.Precio;
        s.DuracionMinutos = dto.DuracionMinutos;

        await _db.SaveChangesAsync();
        return Ok(ToDto(s));
    }

    [HttpPut("{id}/estado")]
    public async Task<ActionResult<ServicioDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var s = await _db.Servicios.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (s is null) return NotFound();

        s.Estado = dto.Estado;
        await _db.SaveChangesAsync();
        return Ok(ToDto(s));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var s = await _db.Servicios.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (s is null) return NotFound();

        var tieneHistorial = await _db.CitaServicios.AnyAsync(cs => cs.ServicioId == id);
        if (tieneHistorial)
            return BadRequest("No puedes eliminar este servicio porque ya tiene citas registradas. Puedes marcarlo como Inactivo en su lugar.");

        _db.Servicios.Remove(s);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
