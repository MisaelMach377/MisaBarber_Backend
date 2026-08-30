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

    private UsuarioDto ToDto(Usuario u, Negocio negocio) => new(
        u.Id, u.Nombre, u.Email, u.FotoUrl, u.Rol, u.BarberoId, u.Barbero?.Nombre, u.ClienteId, u.Estado, u.FechaCreacion,
        negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario, negocio.Plan, ModulosPara(u, negocio)
    );

    // Admin/SuperAdmin ven todo lo que el Plan del negocio permite -- acá
    // no hay restricción de rol, esa es la respuesta a "qué compró la
    // barbería", no a "qué le muestro a mi empleado". Un Barbero además se
    // filtra por Negocio.ModulosBarbero (lo que su Admin habilitó en
    // Roles.jsx) -- la intersección con lo del Plan es a propósito: si el
    // Admin bajó de Pro a Free, un módulo que le había habilitado a su
    // Barbero deja de aparecer solo, sin tener que tocar ModulosBarbero.
    // Un Cliente no usa este panel -- lista vacía.
    private static string[] ModulosPara(Usuario u, Negocio negocio)
    {
        var permitidosPorPlan = Modulos.DeNegocio(negocio.Plan);
        if (u.Rol == "Admin" || u.Rol == "SuperAdmin") return permitidosPorPlan;
        if (u.Rol == "Barbero")
        {
            var propios = (negocio.ModulosBarbero ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return permitidosPorPlan.Intersect(propios).ToArray();
        }
        return Array.Empty<string>();
    }

    private static UsuarioClaims ToClaims(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol, u.NegocioId, u.BarberoId, u.ClienteId);

    // Encuentra a qué Negocio pertenece este login/registro. Sin slug (o
    // vacío) = el negocio principal, la barbería original -- así los links
    // que ya están publicados (misabarber.netlify.app/login, sin nada más)
    // siguen funcionando igual que antes de este cambio. Con slug = la
    // barbería alquilada correspondiente (ver Models/Negocio.cs). Un
    // negocio con Estado = "Inactivo" (alquiler suspendido) se trata igual
    // que si no existiera -- no se distingue el motivo en la respuesta a
    // propósito, para no filtrarle a un desconocido si el slug existe o no.
    private async Task<Negocio?> ResolverNegocio(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return await _db.Negocios.FirstOrDefaultAsync(n => n.EsPrincipal && n.Estado == "Activo");

        var slugNormalizado = slug.Trim().ToLower();
        return await _db.Negocios.FirstOrDefaultAsync(n => n.Slug == slugNormalizado && n.Estado == "Activo");
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var negocio = await ResolverNegocio(dto.Slug);
        if (negocio is null) return Unauthorized("Correo o contraseña incorrectos.");

        var email = (dto.Email ?? "").Trim().ToLower();
        var usuario = await _db.Usuarios.Include(u => u.Barbero)
            .FirstOrDefaultAsync(u => u.NegocioId == negocio.Id && u.Email.ToLower() == email);

        if (usuario is null || !PasswordHasher.Verify(dto.Password ?? "", usuario.PasswordHash))
            return Unauthorized("Correo o contraseña incorrectos.");

        if (usuario.Estado != "Activo")
            return Unauthorized("Este usuario está desactivado. Habla con el administrador.");

        var token = JwtHelper.Generar(ToClaims(usuario), Secreto);

        return Ok(new LoginResponseDto(token, ToDto(usuario, negocio)));
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
        var negocio = await ResolverNegocio(dto.Slug);
        if (negocio is null) return BadRequest("Esta barbería no existe o ya no está disponible.");

        var nombre = (dto.Nombre ?? "").Trim();
        var email = (dto.Email ?? "").Trim().ToLower();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest("Ingresa tu nombre.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return BadRequest("Ingresa un correo válido.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest("La contraseña debe tener al menos 6 caracteres.");

        var yaExiste = await _db.Usuarios.AnyAsync(u => u.NegocioId == negocio.Id && u.Email.ToLower() == email);
        if (yaExiste)
            return Conflict("Ya existe una cuenta con ese correo en esta barbería.");

        var cliente = new Cliente
        {
            NegocioId = negocio.Id,
            Nombre = nombre,
            Email = email,
            Telefono = telefono,
        };
        _db.Clientes.Add(cliente);

        var usuario = new Usuario
        {
            NegocioId = negocio.Id,
            Nombre = nombre,
            Email = email,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Rol = "Cliente",
            ClienteId = cliente.Id,
        };
        _db.Usuarios.Add(usuario);

        await _db.SaveChangesAsync();

        var token = JwtHelper.Generar(ToClaims(usuario), Secreto);

        return Ok(new LoginResponseDto(token, ToDto(usuario, negocio)));
    }

    // El front la llama al montar la app para rehidratar la sesión desde
    // el token guardado en el navegador (ver AuthContext.jsx) — así el
    // usuario no tiene que loguearse de nuevo cada vez que recarga.
    [HttpGet("me")]
    [RequiereAuth]
    public async Task<ActionResult<UsuarioDto>> Me()
    {
        var actual = HttpContext.UsuarioActual()!;
        var usuario = await _db.Usuarios.Include(u => u.Barbero).Include(u => u.Negocio)
            .FirstOrDefaultAsync(u => u.Id == actual.Id);
        if (usuario is null || usuario.Estado != "Activo") return Unauthorized();
        return Ok(ToDto(usuario, usuario.Negocio!));
    }

    // Pública (sin sesión) -- la llama la pantalla de login/registro ANTES
    // de autenticarse (ver Login.jsx) para pintar el logo y color propios
    // de la barbería según el slug de la URL. Reusa ResolverNegocio, que
    // ya trata "no existe" e "inactivo" igual -- así esta pantalla tampoco
    // filtra si un slug existe pero está suspendido, ni error ni éxito
    // distinto: simplemente no hay marca propia que mostrar, se usa el
    // look por defecto de MisaBarber.
    [HttpGet("negocio")]
    public async Task<ActionResult<NegocioPublicoDto>> ObtenerNegocioPublico([FromQuery] string? slug)
    {
        var negocio = await ResolverNegocio(slug);
        if (negocio is null) return NotFound();
        return Ok(new NegocioPublicoDto(negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario));
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
