using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Services;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MisaBarberContext _db;
    private readonly IConfiguration _config;
    private readonly EmailService _email;
    private readonly ILogger<AuthController> _log;
    private readonly IWebHostEnvironment _env;

    public AuthController(MisaBarberContext db, IConfiguration config, EmailService email, ILogger<AuthController> log, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _email = email;
        _log = log;
        _env = env;
    }

    private string Secreto => _config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Falta configurar Jwt:Secret en appsettings.");

    // Async (no un simple map en memoria) porque el horario del negocio
    // viaja acá también -- mismo criterio que LogoUrl/ColorPrimario un
    // poco más arriba: que la pantalla "Buenas tardes" del cliente
    // (MiCuenta.jsx) pinte el horario apenas carga /auth/me, sin pedirle
    // nada aparte al backend.
    private async Task<UsuarioDto> ToDto(Usuario u, Negocio negocio)
    {
        var horario = await _db.HorariosNegocio
            .Where(h => h.NegocioId == negocio.Id)
            .OrderBy(h => h.DiaSemana)
            .Select(h => new HorarioNegocioDiaDto(h.DiaSemana, h.Abierto, Horarios.FormatHora(h.HoraInicio), Horarios.FormatHora(h.HoraFin)))
            .ToListAsync();

        return new(
            u.Id, u.Nombre, u.Email, u.FotoUrl, u.Rol, u.BarberoId, u.Barbero?.Nombre, u.ClienteId, u.Estado, u.FechaCreacion,
            negocio.Nombre, negocio.LogoUrl, negocio.ColorPrimario, negocio.Plan, ModulosPara(u, negocio), horario, negocio.Direccion,
            negocio.Latitud, negocio.Longitud,
            negocio.EsPrincipal ? null : negocio.Slug,
            u.EmailContacto,
            Negocio.ParseEstilos(negocio.EstilosDestacados)
        );
    }

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
        // Un Cliente no usa el panel interno, así que en general no le
        // interesa esta lista -- salvo "Chat", que también existe del
        // lado del Cliente (ver ChatWidget.jsx): así sabe, sin otro
        // request, si su barbería tiene chat en vivo habilitado (Plan Pro).
        if (u.Rol == "Cliente")
            return permitidosPorPlan.Contains("Chat") ? new[] { "Chat" } : Array.Empty<string>();
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
    [EnableRateLimiting("login")]
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

        return Ok(new LoginResponseDto(token, await ToDto(usuario, negocio)));
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
    [EnableRateLimiting("registro")]
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

        return Ok(new LoginResponseDto(token, await ToDto(usuario, negocio)));
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
        return Ok(await ToDto(usuario, usuario.Negocio!));
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

    // Web App Manifest de UNA barbería, como archivo REAL servido por URL
    // (el front lo cuelga en <link rel="manifest" href=".../auth/manifest?slug=x">).
    // Antes el manifest se armaba en el navegador como Blob URL después de
    // cargar JS: el navegador no puede volver a pedirlo (ni para instalar ni para
    // revisar actualizaciones de la app ya instalada), así que el nombre/ícono
    // dependían de qué logo alcanzaba a medirse primero. Acá el contenido es
    // determinista: mismo slug => mismo manifest, siempre. Público (sin sesión), por
    // el mismo motivo que /auth/negocio.
    [HttpGet("manifest")]
    public async Task<IActionResult> ObtenerManifest([FromQuery] string? slug)
    {
        var negocio = await ResolverNegocio(slug);
        var frontend = (_config["Cors:FrontendOrigin"] ?? "http://localhost:5173").TrimEnd('/');
        var backend = $"{Request.Scheme}://{Request.Host}";

        var nombre = negocio?.Nombre ?? "MisaBarber";
        var color = negocio?.ColorPrimario ?? "#0f172a";
        var slugFinal = negocio is { EsPrincipal: false } ? negocio.Slug : null;

        // Logo del negocio: URL absoluta de Cloudinary, o ruta relativa del
        // backend. Para Cloudinary se piden dos PNG cuadrados (192 y 512, lo que
        // Chrome exige para instalar) con la transformación en la URL -- así el
        // tamaño declarado es el real, sin tener que medir la imagen.
        var iconos = new List<object>();
        var logo = negocio?.LogoUrl;
        if (string.IsNullOrWhiteSpace(logo))
        {
            iconos.Add(new { src = $"{frontend}/favicon-misabarber.png", sizes = "256x256", type = "image/png", purpose = "any" });
        }
        else
        {
            var abs = logo.StartsWith("http") ? logo : $"{backend}{logo}";
            if (abs.Contains("/image/upload/"))
            {
                foreach (var t in new[] { 192, 512 })
                    iconos.Add(new
                    {
                        src = abs.Replace("/image/upload/", $"/image/upload/w_{t},h_{t},c_pad,b_rgb:0f172a,f_png/"),
                        sizes = $"{t}x{t}",
                        type = "image/png",
                        purpose = "any",
                    });
            }
            else
            {
                iconos.Add(new { src = abs, sizes = "512x512", type = "image/png", purpose = "any" });
            }
        }

        var manifest = new
        {
            name = nombre,
            short_name = nombre.Length > 12 ? nombre[..11] + "…" : nombre,
            description = $"Reserva tu cita en {nombre}.",
            start_url = slugFinal is null ? $"{frontend}/" : $"{frontend}/{slugFinal}/login",
            scope = $"{frontend}/",
            id = $"{frontend}/?negocio={slugFinal ?? "principal"}",
            display = "standalone",
            background_color = "#0f172a",
            theme_color = color,
            icons = iconos,
        };

        Response.Headers.CacheControl = "public, max-age=300";
        return new ContentResult
        {
            Content = System.Text.Json.JsonSerializer.Serialize(manifest),
            ContentType = "application/manifest+json",
            StatusCode = 200,
        };
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

    // Cambia el correo de recuperación de la propia cuenta. Exige la
    // contraseña actual (ver ActualizarContactoDto). Vacío = volver a usar
    // el correo de acceso.
    [HttpPut("contacto")]
    [RequiereAuth]
    public async Task<ActionResult<object>> ActualizarContacto(ActualizarContactoDto dto)
    {
        var actual = HttpContext.UsuarioActual()!;
        var usuario = await _db.Usuarios.FindAsync(actual.Id);
        if (usuario is null) return Unauthorized();

        if (!PasswordHasher.Verify(dto.ContrasenaActual ?? "", usuario.PasswordHash))
            return BadRequest("La contraseña actual no es correcta.");

        if (!UsuariosController.TryContacto(dto.EmailContacto, out var contacto))
            return BadRequest("El correo de contacto no es válido.");

        usuario.EmailContacto = contacto;
        await _db.SaveChangesAsync();
        return Ok(new { emailContacto = contacto });
    }

    // ---- Olvidé mi contraseña ----
    // Paso 1: la persona escribe su correo. La respuesta es SIEMPRE la misma
    // exista o no la cuenta (y tarde lo mismo: el correo se manda en segundo
    // plano), para que nadie use este endpoint para averiguar qué correos
    // están registrados. Anti-spam: además del límite por IP, un usuario no
    // recibe más de 3 links por hora ni uno nuevo antes de 60 s.
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private const string RespuestaGenerica = "Si ese correo está registrado, te enviamos un link para restablecer tu contraseña. Revisa tu bandeja (y spam).";

    [HttpPost("olvide-contrasena")]
    [EnableRateLimiting("recuperar")]
    public async Task<IActionResult> OlvideContrasena(OlvideContrasenaDto dto)
    {
        var email = (dto.Email ?? "").Trim().ToLower();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return BadRequest("Ingresa un correo válido.");

        var negocio = await ResolverNegocio(dto.Slug);
        // Los logs de abajo son SOLO del servidor (la respuesta al cliente es
        // siempre la misma, a propósito): sirven para diagnosticar por qué
        // no salió un correo sin filtrarle a nadie si la cuenta existe.
        if (negocio is null)
        {
            _log.LogInformation("Recuperación: no existe un negocio activo para el slug '{Slug}'.", dto.Slug);
            return Ok(new { mensaje = RespuestaGenerica });
        }

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.NegocioId == negocio.Id && u.Email.ToLower() == email && u.Estado == "Activo");
        if (usuario is null)
        {
            _log.LogInformation("Recuperación: no hay un usuario ACTIVO con el correo de acceso '{Email}' en el negocio '{Negocio}'.", email, negocio.Nombre);
            return Ok(new { mensaje = RespuestaGenerica });
        }

        var ahora = DateTime.UtcNow;
        var recientes = await _db.RecuperacionesContrasena
            .Where(r => r.UsuarioId == usuario.Id && r.Creado > ahora.AddHours(-1))
            .Select(r => r.Creado)
            .ToListAsync();
        if (recientes.Count >= 3 || recientes.Any(c => c > ahora.AddSeconds(-60)))
        {
            _log.LogInformation("Recuperación: omitida por límite (máx. 3 por hora y 1 cada 60 s) para '{Email}'.", email);
            return Ok(new { mensaje = RespuestaGenerica });
        }

        // 32 bytes aleatorios criptográficos (256 bits) en base64url: imposible de adivinar.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _db.RecuperacionesContrasena.Add(new RecuperacionContrasena
        {
            UsuarioId = usuario.Id,
            TokenHash = Hash(token),
            Creado = ahora,
            Expira = ahora.AddMinutes(30),
        });
        await _db.SaveChangesAsync();

        var origen = (_config["Cors:FrontendOrigin"] ?? "http://localhost:5173").TrimEnd('/');
        var ruta = negocio.EsPrincipal || string.IsNullOrEmpty(negocio.Slug) ? "" : $"/{negocio.Slug}";
        var link = $"{origen}{ruta}/restablecer?token={token}";
        var nombreNegocio = System.Net.WebUtility.HtmlEncode(negocio.Nombre);
        var nombreUsuario = System.Net.WebUtility.HtmlEncode(usuario.Nombre);
        // Color de la barbería para el botón (solo si es un #hex válido: va dentro de un style).
        var color = System.Text.RegularExpressions.Regex.IsMatch(negocio.ColorPrimario ?? "", "^#[0-9a-fA-F]{6}$")
            ? negocio.ColorPrimario! : "#111111";
        var html = $@"<!doctype html><html><body style=""margin:0;padding:0;background:#f4f4f5"">
<div style=""display:none;max-height:0;overflow:hidden"">Tu link para crear una nueva contraseña vence en 30 minutos.</div>
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f4f5;padding:32px 12px"">
 <tr><td align=""center"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width:480px;background:#ffffff;border-radius:16px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;color:#18181b"">
   <tr><td style=""background:{color};padding:22px 28px;color:#ffffff;font-size:18px;font-weight:700"">{nombreNegocio}</td></tr>
   <tr><td style=""padding:30px 28px 8px"">
     <h1 style=""margin:0 0 12px;font-size:22px;line-height:1.3"">Restablece tu contraseña</h1>
     <p style=""margin:0 0 22px;font-size:15px;line-height:1.6;color:#3f3f46"">Hola {nombreUsuario}, recibimos una solicitud para cambiar la contraseña de tu cuenta. Toca el botón para elegir una nueva.</p>
     <p style=""margin:0 0 24px""><a href=""{link}"" style=""display:inline-block;background:{color};color:#ffffff;padding:14px 26px;border-radius:10px;text-decoration:none;font-weight:700;font-size:15px"">Crear nueva contraseña</a></p>
     <p style=""margin:0 0 6px;font-size:13px;line-height:1.6;color:#71717a"">El link vence en <strong>30 minutos</strong> y solo funciona una vez.</p>
     <p style=""margin:0 0 22px;font-size:13px;line-height:1.6;color:#71717a"">Si no fuiste tú, ignora este correo: tu contraseña no cambia.</p>
   </td></tr>
   <tr><td style=""padding:16px 28px 26px;border-top:1px solid #e4e4e7;font-size:12px;line-height:1.6;color:#a1a1aa"">
     ¿El botón no funciona? Copia este link en tu navegador:<br><span style=""word-break:break-all;color:#71717a"">{link}</span>
   </td></tr>
  </table>
 </td></tr>
</table></body></html>";

        // Segundo plano: así la respuesta no tarda distinto según exista o no la cuenta.
        // Va al correo de contacto si lo configuraron; si no, al de acceso.
        var para = string.IsNullOrWhiteSpace(usuario.EmailContacto) ? usuario.Email : usuario.EmailContacto;
        var asunto = $"Restablece tu contraseña - {negocio.Nombre}";
        _log.LogInformation("Recuperación: enviando correo a '{Para}' (cuenta '{Email}').", para, email);
        if (_env.IsDevelopment())
            _log.LogWarning("[DEV] Link de recuperación: {Link}", link);
        _ = Task.Run(async () =>
        {
            var ok = await _email.EnviarAsync(para, asunto, html);
            if (ok) _log.LogInformation("Recuperación: Brevo aceptó el correo para '{Para}'.", para);
        });

        return Ok(new { mensaje = RespuestaGenerica });
    }

    // Paso 2: canjear el token del correo por una contraseña nueva. Token
    // inexistente, vencido o ya usado dan el MISMO error. Al canjear se
    // invalidan todos los links pendientes del usuario, y se inicia sesión.
    [HttpPost("restablecer-contrasena")]
    [EnableRateLimiting("restablecer")]
    public async Task<ActionResult<LoginResponseDto>> RestablecerContrasena(RestablecerContrasenaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ContrasenaNueva) || dto.ContrasenaNueva.Length < 6)
            return BadRequest("La contraseña debe tener al menos 6 caracteres.");

        const string linkInvalido = "El link no es válido o ya venció. Pide uno nuevo.";
        if (string.IsNullOrWhiteSpace(dto.Token)) return BadRequest(linkInvalido);

        var hash = Hash(dto.Token.Trim());
        var ahora = DateTime.UtcNow;
        var rec = await _db.RecuperacionesContrasena
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.UsadoEn == null && r.Expira > ahora);
        if (rec is null) return BadRequest(linkInvalido);

        var usuario = await _db.Usuarios.Include(u => u.Barbero).FirstOrDefaultAsync(u => u.Id == rec.UsuarioId);
        if (usuario is null || usuario.Estado != "Activo") return BadRequest(linkInvalido);

        var negocio = await _db.Negocios.FirstOrDefaultAsync(n => n.Id == usuario.NegocioId && n.Estado == "Activo");
        if (negocio is null) return BadRequest(linkInvalido);

        usuario.PasswordHash = PasswordHasher.Hash(dto.ContrasenaNueva);

        var pendientes = await _db.RecuperacionesContrasena
            .Where(r => r.UsuarioId == usuario.Id && r.UsadoEn == null).ToListAsync();
        foreach (var p in pendientes) p.UsadoEn = ahora;

        await _db.SaveChangesAsync();

        var token = JwtHelper.Generar(ToClaims(usuario), Secreto);
        return Ok(new LoginResponseDto(token, await ToDto(usuario, negocio)));
    }
}
