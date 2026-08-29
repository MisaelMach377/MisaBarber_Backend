using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.Models;
using WebPush;

namespace misabarber.Services;

// Encapsula el envío de notificaciones push del navegador (Web Push +
// VAPID, librería WebPush -- ver misabarber.csproj) para avisar de
// cambios en una cita sin que el usuario tenga que estar mirando la
// pestaña. Un solo lugar que sabe: a quién avisar según lo que pasó, y
// qué hacer cuando una suscripción ya no sirve.
public class PushNotificationService
{
    private readonly MisaBarberContext _db;
    private readonly WebPushClient _cliente = new();
    private readonly VapidDetails? _vapid;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(MisaBarberContext db, IConfiguration config, ILogger<PushNotificationService> logger)
    {
        _db = db;
        _logger = logger;

        var publicKey = config["Vapid:PublicKey"];
        var privateKey = config["Vapid:PrivateKey"];
        var subject = config["Vapid:Subject"];

        // Si todavía no están configuradas las claves VAPID (ver
        // appsettings.Development.json), el servicio queda "apagado": no
        // manda nada y no rompe el resto de la app -- una notificación
        // que no sale nunca debe tumbar el guardado de una cita.
        _vapid = string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey) || string.IsNullOrWhiteSpace(subject)
            ? null
            : new VapidDetails(subject, publicKey, privateKey);
    }

    // Cita nueva: avisa al staff (Admin + el barbero asignado, si tiene
    // cuenta de acceso -- no todos la tienen, ver Models/Usuario.cs) --
    // salvo a quien la creó, para no auto-notificar a un Admin/Barbero que
    // agenda desde el propio panel.
    public async Task NotificarNuevaCita(Cita cita, Guid actorUsuarioId)
    {
        var destinatarios = await _db.Usuarios
            .Where(u => u.Estado == "Activo" && u.Id != actorUsuarioId)
            .Where(u => u.Rol == "Admin" || (u.Rol == "Barbero" && u.BarberoId == cita.BarberoId))
            .Select(u => u.Id)
            .ToListAsync();

        var cuando = cita.FechaHora.ToString("dd/MM HH:mm");
        await EnviarATodos(destinatarios, "Nueva cita", $"{cita.Cliente?.Nombre} agendó para el {cuando}");
    }

    // Cambio de estado: avisa al "otro lado" de quien hizo el cambio -- si
    // fue el Cliente (solo puede cancelar la suya propia, ver
    // CitasController.CambiarEstado) le avisa al staff; si fue Admin/
    // Barbero, le avisa al Cliente (si tiene cuenta de acceso).
    public async Task NotificarCambioEstado(Cita cita, string estadoNuevo, Guid actorUsuarioId, string actorRol)
    {
        var cuando = cita.FechaHora.ToString("dd/MM HH:mm");

        if (actorRol == "Cliente")
        {
            var destinatarios = await _db.Usuarios
                .Where(u => u.Estado == "Activo" && u.Id != actorUsuarioId)
                .Where(u => u.Rol == "Admin" || (u.Rol == "Barbero" && u.BarberoId == cita.BarberoId))
                .Select(u => u.Id)
                .ToListAsync();

            await EnviarATodos(destinatarios, "Cita cancelada", $"{cita.Cliente?.Nombre} canceló su cita del {cuando}");
            return;
        }

        var clienteUsuarioId = await _db.Usuarios
            .Where(u => u.Estado == "Activo" && u.Rol == "Cliente" && u.ClienteId == cita.ClienteId && u.Id != actorUsuarioId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();

        if (clienteUsuarioId is null) return;

        var (titulo, cuerpo) = estadoNuevo switch
        {
            "Confirmada" => ("Cita confirmada", $"Tu cita del {cuando} quedó confirmada."),
            "Completada" => ("¡Gracias por tu visita!", "Esperamos verte pronto de nuevo."),
            "Cancelada" => ("Cita cancelada", $"Tu cita del {cuando} fue cancelada."),
            _ => ("Cita actualizada", $"Tu cita del {cuando} cambió de estado."),
        };

        await EnviarATodos(new[] { clienteUsuarioId.Value }, titulo, cuerpo);
    }

    private async Task EnviarATodos(IReadOnlyCollection<Guid> usuarioIds, string titulo, string cuerpo)
    {
        if (_vapid is null || usuarioIds.Count == 0) return;

        var suscripciones = await _db.SuscripcionesPush
            .Where(s => usuarioIds.Contains(s.UsuarioId))
            .ToListAsync();

        if (suscripciones.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { titulo, cuerpo });
        var vencidas = new List<SuscripcionPush>();

        foreach (var s in suscripciones)
        {
            try
            {
                var suscripcion = new PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await _cliente.SendNotificationAsync(suscripcion, payload, _vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // El navegador invalidó esta suscripción (se desinstaló,
                // se borraron los datos del sitio, etc.) -- no vale la
                // pena reintentar, se limpia para no seguir fallando cada
                // vez que pase algo.
                vencidas.Add(s);
            }
            catch (Exception ex)
            {
                // Un push que falla nunca debe tumbar la acción principal
                // (crear/actualizar la cita ya se guardó antes de llegar
                // acá) -- solo se registra para poder revisarlo.
                _logger.LogWarning(ex, "No se pudo enviar la notificación push a {SuscripcionId}", s.Id);
            }
        }

        if (vencidas.Count > 0)
        {
            _db.SuscripcionesPush.RemoveRange(vencidas);
            await _db.SaveChangesAsync();
        }
    }
}
