namespace misabarber.DTOs;

public record ProductoDto(
    Guid Id,
    string Nombre,
    string? Marca,
    string? Descripcion,
    decimal Precio,
    int Stock,
    int StockReservado,
    int StockMinimo,
    string? FotoUrl,
    string Estado,
    DateTime FechaCreacion
);

public record ProductoCreateDto(string Nombre, string? Marca, string? Descripcion, decimal Precio, int Stock, string? FotoUrl, int StockMinimo = 5);

// Tipo: "Entrada" | "Salida" | "Ajuste". En Entrada/Salida, Cantidad es cuántas
// unidades entran o salen (positiva). En Ajuste, Cantidad es el stock REAL contado
// (el nuevo total); el backend calcula la diferencia.
public record MovimientoStockCreateDto(string Tipo, int Cantidad, string? Motivo);

public record MovimientoStockDto(
    Guid Id,
    string Tipo,
    int Cantidad,
    int StockResultante,
    string? Motivo,
    string UsuarioNombre,
    DateTime Fecha
);
