namespace misabarber.DTOs;

public record CitaAuditoriaDto(
    Guid Id,
    Guid? CitaId,
    string Accion,
    string? Detalle,
    string ClienteNombre,
    string BarberoNombre,
    string ServicioNombre,
    DateTime FechaHoraCita,
    DateTime FechaHoraEvento
);
