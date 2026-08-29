namespace misabarber.DTOs;

public record ServicioDto(Guid Id, string Nombre, decimal Precio, int DuracionMinutos, string Estado, DateTime FechaCreacion);

public record ServicioCreateDto(string Nombre, decimal Precio, int DuracionMinutos);
