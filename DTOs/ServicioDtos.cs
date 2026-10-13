namespace misabarber.DTOs;

public record ServicioDto(Guid Id, string Nombre, string? Descripcion, decimal Precio, int DuracionMinutos, string Estado, DateTime FechaCreacion, string? FotoUrl, bool EsEspecial);

public record ServicioCreateDto(string Nombre, string? Descripcion, decimal Precio, int DuracionMinutos, string? FotoUrl = null, bool EsEspecial = false);
