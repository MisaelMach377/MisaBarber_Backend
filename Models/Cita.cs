namespace misabarber.Models;

public class Cita
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece esta cita (multi-tenant, ver
    // Models/Negocio.cs). Se guarda acá TAMBIÉN (y no solo se infiere por
    // Cliente/Barbero) para poder filtrar y auditar directo sin tener que
    // hacer join, y como segunda red de seguridad: aunque alguien lograra
    // mandar un ClienteId/BarberoId de otro Negocio, HayConflictoDeHorario
    // y el resto de las queries de CitasController igual filtran por el
    // NegocioId del usuario logueado.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public Guid BarberoId { get; set; }
    public Barbero? Barbero { get; set; }

    // Antes era un solo ServicioId -- ahora una cita puede llevar varios
    // servicios a la vez (ej. "Corte" + "Barba"), así que se guarda como
    // lista vía la tabla intermedia CitaServicio (ver ese archivo). El
    // precio y la duración totales YA NO se guardan acá: se calculan
    // sumando los de cada servicio de la lista (ver CitasController.ToDto),
    // mismo criterio de "no congelar un total" que ya se usaba antes con
    // un solo servicio.
    public List<CitaServicio> CitaServicios { get; set; } = new();

    // Hora de inicio. La hora de fin no se guarda — se calcula en el
    // momento (FechaHora + la suma de las duraciones de los servicios)
    // porque si el precio o la duración de un servicio cambian después, no
    // queremos que citas viejas ya agendadas cambien de horario solas.
    public DateTime FechaHora { get; set; }

    // Pendiente | Confirmada | Completada | Cancelada
    public string Estado { get; set; } = "Pendiente";

    public string? Notas { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Si ya se le mandó el push de "tu cita está por empezar" al staff
    // (ver Services/RecordatorioCitasService.cs). Sin esto, cada vuelta
    // del background service (cada 5 min, ver ese archivo) volvería a
    // mandar el mismo recordatorio mientras la cita siga cayendo dentro
    // de la ventana de "antelación" -- este flag es lo que la hace
    // dispararse una sola vez por cita.
    public bool RecordatorioEnviado { get; set; } = false;
}
