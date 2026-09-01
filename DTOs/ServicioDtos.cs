namespace misabarber.DTOs;

public record ServicioDto(Guid Id, string Nombre, string? Descripcion, decimal Precio, int DuracionMinutos, string Estado, DateTime FechaCreacion);

public record ServicioCreateDto(string Nombre, string? Descripcion, decimal Precio, int DuracionMinutos);
