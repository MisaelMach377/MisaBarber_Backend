using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Administración de negocios (barberías) alquiladas -- exclusivo del
// SuperAdmin (yo, el dueño de la plataforma, ver el Rol "SuperAdmin" en
// Models/Usuario.cs). [RequiereAuth(Rol = "SuperAdmin")] exige ese rol
// EXACTO -- a diferencia de [RequiereAuth(Rol = "Admin")] en el resto del
// sistema, acá un Admin normal (el de un negocio alquilado) no entra ni
// aunque RequiereAuthAttribute trate a SuperAdmin como Admin en todos
// lados: la relación es de un solo sentido, SuperAdmin cumple "Admin" pero
// "Admin" no cumple "SuperAdmin".
[ApiController]
[Route("api/negocios")]
[RequiereAuth(Rol = "SuperAdmin")]
public class NegociosController : ControllerBase
{
    private static readonly Regex SlugValido = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    private readonly MisaBarberContext _db;

    public NegociosController(MisaBarberContext db)
    {
        _db = db;
    }

    private static NegocioDto ToDto(Negocio n) => new(n.Id, n.Nombre, n.Slug, n.EsPrincipal, n.Estado, n.FechaCreacion, n.LogoUrl, n.ColorPrimario, n.Plan);

    [HttpGet]
    public async Task<ActionResult<List<NegocioDto>>> GetAll()
    {
        var lista = await _db.Negocios.OrderBy(n => n.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    // Crea la barbería nueva Y su primer Usuario Admin en un solo paso --
    // sin esto, quien alquile el sistema quedaría con un Negocio pero sin
    // ninguna cuenta para entrar a administrarlo. Ese Admin SIEMPRE se crea
    // con Rol = "Admin", nunca "SuperAdmin" -- el poder de administrar
    // negocios es exclusivo de la cuenta original, no se hereda al crear uno.
    [HttpPost]
    public async Task<ActionResult<NegocioDto>> Create(NegocioCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreNegocio))
            return BadRequest("El nombre de la barbería es obligatorio.");

        var slug = (dto.Slug ?? "").Trim().ToLower();
        if (!SlugValido.IsMatch(slug))
            return BadRequest("El slug solo puede tener letras minúsculas, números y guiones (ej. \"la-mejor-barberia\").");

        var slugEnUso = await _db.Negocios.AnyAsync(n => n.Slug == slug);
        if (slugEnUso)
            return Conflict("Ya existe una barbería con ese slug.");

        if (string.IsNullOrWhiteSpace(dto.NombreAdmin))
            return BadRequest("El nombre del administrador es obligatorio.");

        var email = (dto.EmailAdmin ?? "").Trim().ToLower();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return BadRequest("Ingresa un correo válido para el administrador.");

        if (string.IsNullOrWhiteSpace(dto.PasswordAdmin) || dto.PasswordAdmin.Length < 6)
            return BadRequest("La contraseña debe tener al menos 6 caracteres.");

        var negocio = new Negocio
        {
            Nombre = dto.NombreNegocio.Trim(),
            Slug = slug,
            EsPrincipal = false,
            Plan = "Free",
        };
        _db.Negocios.Add(negocio);
        _db.HorariosNegocio.AddRange(Horarios.SembrarNegocio(negocio.Id));

        var admin = new Usuario
        {
            NegocioId = negocio.Id,
            Nombre = dto.NombreAdmin.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(dto.PasswordAdmin),
            Rol = "Admin",
        };
        _db.Usuarios.Add(admin);

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), ToDto(negocio));
    }

    // Suspende (o reactiva) el acceso de un negocio -- ver
    // AuthController.ResolverNegocio, que bloquea login/registro si
    // Estado != "Activo". No borra nada.
    [HttpPut("{id}/estado")]
    public async Task<ActionResult<NegocioDto>> CambiarEstado(Guid id, CambiarEstadoNegocioDto dto)
    {
        if (dto.Estado != "Activo" && dto.Estado != "Inactivo")
            return BadRequest("Estado no válido.");

        var negocio = await _db.Negocios.FindAsync(id);
        if (negocio is null) return NotFound();

        if (negocio.EsPrincipal && dto.Estado == "Inactivo")
            return BadRequest("No puedes suspender el negocio principal.");

        negocio.Estado = dto.Estado;
        await _db.SaveChangesAsync();
        return Ok(ToDto(negocio));
    }

    // El SuperAdmin editando la apariencia de CUALQUIER negocio desde la
    // lista (a diferencia de MiNegocioController, que es cada Admin
    // editando solo el suyo) -- útil para dejarle el logo/color ya
    // cargado a una barbería cuando la da de alta, sin depender de que su
    // Admin entre a configurarlo.
    [HttpPut("{id}/apariencia")]
    public async Task<ActionResult<NegocioDto>> ActualizarApariencia(Guid id, ActualizarAparienciaDto dto)
    {
        if (!Regex.IsMatch(dto.ColorPrimario ?? "", "^#[0-9a-fA-F]{6}$"))
            return BadRequest("El color debe ser un código hexadecimal válido, ej: #2563eb.");

        var negocio = await _db.Negocios.FindAsync(id);
        if (negocio is null) return NotFound();

        negocio.LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl;
        negocio.ColorPrimario = dto.ColorPrimario;
        await _db.SaveChangesAsync();
        return Ok(ToDto(negocio));
    }

    // Free | Pro -- lo sube/baja el SuperAdmin a mano desde la lista (sin
    // cobro automático todavía, ver Utils/Modulos.cs). Bajar de Pro a Free
    // no borra la configuración de Roles.jsx de ese negocio (ModulosBarbero
    // queda como estaba): si vuelve a subir a Pro, recupera exactamente lo
    // que tenía habilitado antes -- ver AuthController.ModulosPara, que
    // intersecta con el Plan en cada login/me en vez de tocar esa lista acá.
    [HttpPut("{id}/plan")]
    public async Task<ActionResult<NegocioDto>> ActualizarPlan(Guid id, ActualizarPlanNegocioDto dto)
    {
        if (!Modulos.PlanesValidos.Contains(dto.Plan))
            return BadRequest("Plan no válido.");

        var negocio = await _db.Negocios.FindAsync(id);
        if (negocio is null) return NotFound();

        negocio.Plan = dto.Plan;
        await _db.SaveChangesAsync();
        return Ok(ToDto(negocio));
    }
}
