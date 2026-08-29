using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Gestión de cuentas de acceso — separado de BarberosController a
// propósito: un Barbero (la ficha operativa que se ve en la agenda, con
// foto/teléfono/estado) no siempre tiene una cuenta de login todavía, y
// crear/borrar una cuenta acá no debería tocar su ficha de barbero. Solo
// Admin entra (ver RequiereAuth a nivel de clase).
[ApiController]
[Route("api/usuarios")]
[RequiereAuth(Rol = "Admin")]
public class UsuariosController : ControllerBase
{
    private static readonly string[] RolesValidos = { "Admin", "Barbero" };
    private static readonly string[] EstadosValidos = { "Activo", "Inactivo" };

    private readonly MisaBarberContext _db;

    public UsuariosController(MisaBarberContext db)
    {
        _db = db;
    }

    private static UsuarioDto ToDto(Usuario u) => new(
        u.Id, u.Nombre, u.Email, u.FotoUrl, u.Rol, u.BarberoId, u.Barbero?.Nombre, u.ClienteId, u.Estado, u.FechaCreacion
    );

    private IQueryable<Usuario> ConBarbero() => _db.Usuarios.Include(u => u.Barbero);

    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> GetAll()
    {
        var lista = await ConBarbero().OrderBy(u => u.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UsuarioDto>> GetById(Guid id)
    {
        var usuario = await ConBarbero().FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null) return NotFound();
        return Ok(ToDto(usuario));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create(UsuarioCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("El correo es obligatorio.");
        if (!RolesValidos.Contains(dto.Rol))
            return BadRequest("Rol no válido.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("La contraseña debe tener al menos 6 caracteres.");

        var email = dto.Email.Trim().ToLower();
        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email);
        if (yaExiste)
            return BadRequest("Ya existe un usuario con ese correo.");

        if (dto.Rol == "Barbero")
        {
            if (dto.BarberoId is null)
                return BadRequest("Elige a qué barbero corresponde esta cuenta.");
            var barberoExiste = await _db.Barberos.AnyAsync(b => b.Id == dto.BarberoId);
            if (!barberoExiste)
                return BadRequest("El barbero no existe.");
        }

        var usuario = new Usuario
        {
            Nombre = dto.Nombre,
            Email = dto.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Rol = dto.Rol,
            BarberoId = dto.Rol == "Barbero" ? dto.BarberoId : null,
            FotoUrl = dto.FotoUrl,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var creado = await ConBarbero().FirstAsync(u => u.Id == usuario.Id);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, ToDto(creado));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UsuarioDto>> Update(Guid id, UsuarioUpdateDto dto)
    {
        var usuario = await ConBarbero().FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("El correo es obligatorio.");
        if (!RolesValidos.Contains(dto.Rol))
            return BadRequest("Rol no válido.");

        var email = dto.Email.Trim().ToLower();
        var otroConEseCorreo = await _db.Usuarios.AnyAsync(u => u.Id != id && u.Email.ToLower() == email);
        if (otroConEseCorreo)
            return BadRequest("Ya existe otro usuario con ese correo.");

        if (dto.Rol == "Barbero")
        {
            if (dto.BarberoId is null)
                return BadRequest("Elige a qué barbero corresponde esta cuenta.");
            var barberoExiste = await _db.Barberos.AnyAsync(b => b.Id == dto.BarberoId);
            if (!barberoExiste)
                return BadRequest("El barbero no existe.");
        }

        usuario.Nombre = dto.Nombre;
        usuario.Email = dto.Email.Trim();
        usuario.Rol = dto.Rol;
        usuario.BarberoId = dto.Rol == "Barbero" ? dto.BarberoId : null;
        if (dto.FotoUrl is not null)
            usuario.FotoUrl = dto.FotoUrl;

        await _db.SaveChangesAsync();

        var actualizado = await ConBarbero().FirstAsync(u => u.Id == id);
        return Ok(ToDto(actualizado));
    }

    [HttpPut("{id}/estado")]
    public async Task<ActionResult<UsuarioDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var usuario = await ConBarbero().FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null) return NotFound();

        // No dejar que un Admin se desactive a sí mismo y se quede afuera
        // sin nadie que pueda volver a activarlo.
        if (usuario.Id == HttpContext.UsuarioActual()!.Id && dto.Estado == "Inactivo")
            return BadRequest("No puedes desactivar tu propia cuenta.");

        usuario.Estado = dto.Estado;
        await _db.SaveChangesAsync();
        return Ok(ToDto(usuario));
    }

    // El Admin resetea la contraseña de otro usuario sin saber la actual
    // (distinto de AuthController.CambiarContrasena, que es cada quien
    // cambiando la propia y sí tiene que confirmarla).
    [HttpPut("{id}/resetear-contrasena")]
    public async Task<IActionResult> ResetearContrasena(Guid id, ResetearContrasenaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ContrasenaNueva) || dto.ContrasenaNueva.Length < 6)
            return BadRequest("La nueva contraseña debe tener al menos 6 caracteres.");

        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        usuario.PasswordHash = PasswordHasher.Hash(dto.ContrasenaNueva);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        if (usuario.Id == HttpContext.UsuarioActual()!.Id)
            return BadRequest("No puedes eliminar tu propia cuenta.");

        _db.Usuarios.Remove(usuario);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
