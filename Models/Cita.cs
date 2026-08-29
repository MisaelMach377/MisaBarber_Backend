namespace misabarber.Models;

public class Cita
{
    public Guid Id { get; set; } = Guid.NewGuid();

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
}
