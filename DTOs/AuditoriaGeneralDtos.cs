namespace misabarber.DTOs;

public record AuditoriaGeneralDto(
    Guid Id,
    string Entidad,
    Guid? EntidadId,
    string EntidadNombre,
    string Accion,
    string? Detalle,
    string AutorNombre,
    DateTime FechaHoraEvento
);
