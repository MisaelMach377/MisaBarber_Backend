using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

[ApiController]
[Route("api/notificaciones")]
public class NotificacionesController : ControllerBase
{
    private readonly MisaBarberContext _db;
    private readonly IConfiguration _config;

    public NotificacionesController(MisaBarberContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // Sin [RequiereAuth] a propósito: el front la necesita para armar la
    // suscripción (pushManager.subscribe) ANTES de mandarla, y la clave
    // pública en sí no es secreta -- es justamente la que ve el navegador
    // del usuario, mismo criterio que un login público.
    [HttpGet("vapid-public-key")]
    public ActionResult<VapidPublicKeyDto> GetVapidPublicKey()
    {
        var clave = _config["Vapid:PublicKey"];
        if (string.IsNullOrWhiteSpace(clave))
            return NotFound("Las notificaciones push no están configuradas todavía.");

        return Ok(new VapidPublicKeyDto(clave));
    }

    [HttpPost("suscribir")]
    [RequiereAuth]
    public async Task<IActionResult> Suscribir(SuscripcionPushCreateDto dto)
    {
        var usuario = HttpContext.UsuarioActual()!;

        // Si este mismo Endpoint (navegador/dispositivo) ya estaba
        // suscrito -- de este usuario o de otro que haya entrado antes en
        // el mismo navegador -- se actualiza en vez de duplicar. El
        // Endpoint identifica al navegador, no a la cuenta, así que puede
        // haber quedado de una sesión anterior con otro usuario.
        var existente = await _db.SuscripcionesPush.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existente is not null)
        {
            existente.UsuarioId = usuario.Id;
            existente.P256dh = dto.Keys.P256dh;
            existente.Auth = dto.Keys.Auth;
        }
        else
        {
            _db.SuscripcionesPush.Add(new SuscripcionPush
            {
                UsuarioId = usuario.Id,
                Endpoint = dto.Endpoint,
                P256dh = dto.Keys.P256dh,
                Auth = dto.Keys.Auth,
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("suscribir")]
    [RequiereAuth]
    public async Task<IActionResult> Desuscribir([FromQuery] string endpoint)
    {
        var usuario = HttpContext.UsuarioActual()!;
        var existente = await _db.SuscripcionesPush
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UsuarioId == usuario.Id);

        if (existente is not null)
        {
            _db.SuscripcionesPush.Remove(existente);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
