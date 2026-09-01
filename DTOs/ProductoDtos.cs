namespace misabarber.DTOs;

public record ProductoDto(
    Guid Id,
    string Nombre,
    string? Marca,
    string? Descripcion,
    decimal Precio,
    int Stock,
    string? FotoUrl,
    string Estado,
    DateTime FechaCreacion
);

public record ProductoCreateDto(string Nombre, string? Marca, string? Descripcion, decimal Precio, int Stock, string? FotoUrl);
