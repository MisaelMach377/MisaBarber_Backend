namespace misabarber.Models;

// Registro de auditoría de citas. Es una tabla de solo-inserción, separada
// de Cita a propósito: guarda una "foto" (nombres, no solo los Ids) de cómo
// estaba la cita en el momento del evento, para que el historial siga
// siendo legible aunque el cliente/barbero/servicio cambie de nombre después,
// o incluso si la cita misma se termina borrando (Pendiente/Cancelada sí se
// puede borrar — ver CitasController.Delete). CitaId no tiene navegación ni
// FK real: es solo una referencia "si todavía existe", nunca bloquea nada.
public class CitaAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CitaId { get; set; }

    // A qué barbería pertenece este registro (multi-tenant, ver
    // Models/Negocio.cs). Se guarda directo (y no se infiere por CitaId)
    // porque CitaId es una referencia "blanda" que puede apuntar a una
    // cita ya borrada -- sin este campo no habría forma confiable de
    // filtrar el historial de auditoría por Negocio.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    // "Creada" | "Editada" | "Estado: Pendiente -> Confirmada" | "Eliminada"
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }

    public string ClienteNombre { get; set; } = string.Empty;
    public string BarberoNombre { get; set; } = string.Empty;
    public string ServicioNombre { get; set; } = string.Empty;

    // Fecha/hora de la CITA (para poder filtrar el historial por cuándo era
    // el turno), distinta de FechaHoraEvento (cuándo pasó el cambio en sí).
    public DateTime FechaHoraCita { get; set; }
    public DateTime FechaHoraEvento { get; set; } = DateTime.UtcNow;
}
