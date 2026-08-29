using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MisaBarberContext _db;
    private readonly IConfiguration _config;

    public AuthController(MisaBarberContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private string Secreto => _config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Falta configurar Jwt:Secret en appsettings.");

    private static UsuarioDto ToDto(Usuario u) => new(
        u.Id, u.Nombre, u.Email, u.FotoUrl, u.Rol, u.BarberoId, u.Barbero?.Nombre, u.ClienteId, u.Estado, u.FechaCreacion
    );

    private static UsuarioClaims ToClaims(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol, u.BarberoId, u.ClienteId);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLower();
        var usuario = await _db.Usuarios.Include(u => u.Barbero)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (usuario is null || !PasswordHasher.Verify(dto.Password ?? "", usuario.PasswordHash))
            return Unauthorized("Correo o contraseña incorrectos.");

        if (usuario.Estado != "Activo")
            return Unauthorized("Este usuario está desactivado. Habla con el administrador.");

        var token = JwtHelper.Generar(ToClaims(usuario), Secreto);

        return Ok(new LoginResponseDto(token, ToDto(usuario)));
    }

    // Alta pública de una cuenta Cliente ("Sign Up" en el panel deslizante
    // del login, ver Login.jsx): crea de una vez la ficha de Cliente (el
    // mismo registro de contacto que ya usaban las citas, ver
    // Models/Cliente.cs) y el Usuario de login enganchado a ella por
    // ClienteId -- mismo patrón que Usuario.BarberoId para el rol Barbero.
    // No pide [RequiereAuth] a propósito: todavía no hay sesión cuando se
    // llama. Devuelve el token de una (auto-login tras registrarse), como
    // cualquier signup moderno -- no tiene sentido mandar al cliente de
    // vuelta al formulario de login después de que ya escribió su clave.
    [HttpPost("registro")]
    public async Task<ActionResult<LoginResponseDto>> Registro(RegistroClienteDto dto)
    {
        var nombre = (dto.Nombre ?? "").Trim();
        var email = (dto.Email ?? "").Trim().ToLower();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest("Ingresa tu nombre.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return BadRequest("Ingresa un correo válido.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("La contraseña debe tener al menos 6 caracteres.");

        var yaExiste = await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email);
        if (yaExiste)
            return Conflict("Ya existe una cuenta con ese correo.");

        var cliente = new Cliente
        {
            Nombre = nombre,
            Email = email,
            Telefono = telefono,
        };
        _db.Clientes.Add(cliente);

        var usuario = new Usuario
        {
            Nombre = nombre,
            Email = email,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Rol = "Cliente",
            ClienteId = cliente.Id,
        };
        _db.Usuarios.Add(usuario);

        await _db.SaveChangesAsync();

        var token = JwtHelper.Generar(ToClaims(usuario), Secreto);

        return Ok(new LoginResponseDto(token, ToDto(usuario)));
    }

    // El front la llama al montar la app para rehidratar la sesión desde
    // el token guardado en el navegador (ver AuthContext.jsx) — así el
    // usuario no tiene que loguearse de nuevo cada vez que recarga.
    [HttpGet("me")]
    [RequiereAuth]
    public async Task<ActionResult<UsuarioDto>> Me()
    {
        var actual = HttpContext.UsuarioActual()!;
        var usuario = await _db.Usuarios.Include(u => u.Barbero).FirstOrDefaultAsync(u => u.Id == actual.Id);
        if (usuario is null || usuario.Estado != "Activo") return Unauthorized();
        return Ok(ToDto(usuario));
    }

    // Cambio de contraseña desde "Mi perfil" — pide la contraseña actual a
    // propósito (a diferencia de UsuariosController.ResetearContrasena,
    // que es el Admin reseteando la de otro usuario sin saber la vieja).
    [HttpPut("cambiar-contrasena")]
    [RequiereAuth]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaDto dto)
    {
        var actual = HttpContext.UsuarioActual()!;
        var usuario = await _db.Usuarios.FindAsync(actual.Id);
        if (usuario is null) return Unauthorized();

        if (!PasswordHasher.Verify(dto.ContrasenaActual ?? "", usuario.PasswordHash))
            return BadRequest("La contraseña actual no es correcta.");

        if (string.IsNullOrWhiteSpace(dto.ContrasenaNueva) || dto.ContrasenaNueva.Length < 6)
            return BadRequest("La nueva contraseña debe tener al menos 6 caracteres.");

        usuario.PasswordHash = PasswordHasher.Hash(dto.ContrasenaNueva);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
