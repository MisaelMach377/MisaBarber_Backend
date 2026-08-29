namespace misabarber.Models;

// Suscripción a notificaciones push del navegador (Web Push) de UN
// dispositivo/navegador de un Usuario. Un mismo Usuario puede tener varias
// (ej. entra desde el celular y la compu), por eso no es un campo más de
// Usuario sino su propia tabla -- se identifican por Endpoint (lo arma el
// propio navegador al suscribirse, único por navegador/dispositivo).
//
// P256dh y Auth son las claves públicas que arma el navegador al
// suscribirse (PushSubscription.getKey) -- las necesita el backend para
// cifrar el mensaje que le manda al servicio de push del navegador (ver
// Services/PushNotificationService.cs), no son opcionales.
public class SuscripcionPush
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
