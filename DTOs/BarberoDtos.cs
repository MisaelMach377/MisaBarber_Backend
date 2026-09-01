namespace misabarber.DTOs;

public record BarberoDto(Guid Id, string Nombre, string? Telefono, string? Email, string? FotoUrl, string Estado, DateTime FechaCreacion);

public record BarberoCreateDto(string Nombre, string? Telefono, string? Email, string? FotoUrl);
