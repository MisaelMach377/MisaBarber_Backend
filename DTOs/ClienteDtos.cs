namespace misabarber.DTOs;

public record ClienteDto(Guid Id, string Nombre, string? Telefono, string? Email, string? FotoUrl, string Estado, DateTime FechaCreacion);

public record ClienteCreateDto(string Nombre, string? Telefono, string? Email, string? FotoUrl);

public record CambiarEstadoDto(string Estado);
