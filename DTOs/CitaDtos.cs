namespace misabarber.DTOs;

// Un servicio dentro de la lista de una cita -- ya no hay un ServicioId
// "suelto" en CitaDto: ahora una cita puede llevar varios a la vez (ej.
// "Corte" + "Barba"), ver Models/CitaServicio.cs.
public record CitaServicioDto(
    Guid ServicioId,
    string ServicioNombre,
    int ServicioDuracionMinutos,
    decimal ServicioPrecio
);

public record CitaDto(
    Guid Id,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteFotoUrl,
    Guid BarberoId,
    string BarberoNombre,
    string? BarberoFotoUrl,
    List<CitaServicioDto> Servicios,
    // Nombres ya unidos con ", " (ej. "Corte, Barba"), listos para una
    // columna de tabla -- así Citas/Historial/Reportes no tienen que volver
    // a armar el texto cada uno por su cuenta. "Servicios" de arriba sigue
    // disponible para quien necesite el detalle (precio/duración por
    // servicio, o los Ids para precargar el formulario de edición).
    string ServiciosNombre,
    int DuracionTotalMinutos,
    decimal PrecioTotal,
    DateTime FechaHora,
    string Estado,
    string? Notas,
    DateTime FechaCreacion
);

public record CitaCreateDto(Guid ClienteId, Guid BarberoId, List<Guid> ServicioIds, DateTime FechaHora, string? Notas);
