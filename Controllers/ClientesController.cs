using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/clientes")]
[RequiereAuth]
public class ClientesController : ControllerBase
{
    private static readonly string[] EstadosValidos = { "Activo", "Inactivo" };

    private readonly MisaBarberContext _db;

    public ClientesController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static ClienteDto ToDto(Cliente c) => new(c.Id, c.Nombre, c.Telefono, c.Email, c.FotoUrl, c.Estado, c.FechaCreacion);

    [HttpGet]
    public async Task<ActionResult<List<ClienteDto>>> GetAll()
    {
        var lista = await _db.Clientes.Where(c => c.NegocioId == NegocioId).OrderBy(c => c.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClienteDto>> GetById(Guid id)
    {
        var c = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (c is null) return NotFound();
        return Ok(ToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create(ClienteCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");

        if (!Validaciones.TelefonoValido(dto.Telefono))
            return BadRequest("El teléfono debe tener máximo 9 dígitos numéricos.");

        var cliente = new Cliente
        {
            NegocioId = NegocioId,
            Nombre = dto.Nombre,
            Telefono = dto.Telefono,
            Email = dto.Email,
            FotoUrl = dto.FotoUrl,
        };

        _db.Clientes.Add(cliente);
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Cliente", cliente.Id, cliente.Nombre, "Creado");
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, ToDto(cliente));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ClienteDto>> Update(Guid id, ClienteCreateDto dto)
    {
        var c = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (c is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");

        if (!Validaciones.TelefonoValido(dto.Telefono))
            return BadRequest("El teléfono debe tener máximo 9 dígitos numéricos.");

        c.Nombre = dto.Nombre;
        c.Telefono = dto.Telefono;
        c.Email = dto.Email;
        if (dto.FotoUrl is not null)
            c.FotoUrl = dto.FotoUrl;

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Cliente", c.Id, c.Nombre, "Editado");
        await _db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    [HttpPut("{id}/estado")]
    public async Task<ActionResult<ClienteDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var c = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (c is null) return NotFound();

        var estadoAnterior = c.Estado;
        c.Estado = dto.Estado;
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Cliente", c.Id, c.Nombre, $"Estado: {estadoAnterior} -> {dto.Estado}");
        await _db.SaveChangesAsync();
        return Ok(ToDto(c));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var c = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (c is null) return NotFound();

        // Igual que ClienteFinal en MisaDesk: si ya tiene citas, no se borra
        // (dejaría citas huérfanas) — se sugiere Inactivo en su lugar.
        var tieneHistorial = await _db.Citas.AnyAsync(x => x.ClienteId == id);
        if (tieneHistorial)
            return BadRequest("No puedes eliminar a este cliente porque ya tiene citas registradas. Puedes marcarlo como Inactivo en su lugar.");

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Cliente", c.Id, c.Nombre, "Eliminado");
        _db.Clientes.Remove(c);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
