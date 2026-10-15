namespace misabarber.DTOs;

// PrecioUnitario null = usar el precio actual del catálogo.
public record VentaCreateDto(Guid ProductoId, int Cantidad, Guid? ClienteId, decimal? PrecioUnitario);

public record VentaDto(
    Guid Id,
    Guid? ProductoId,
    string ProductoNombre,
    string? ClienteNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Total,
    string Origen,
    string UsuarioNombre,
    DateTime Fecha
);

public record ReporteProductoFilaDto(Guid? ProductoId, string Nombre, int Unidades, decimal Ingresos, DateTime UltimaVenta);

public record ReporteSinVentasDto(Guid ProductoId, string Nombre, string? Marca, int Stock, decimal ValorInmovilizado);

public record ReporteProductosDto(
    decimal IngresosTotal,
    int UnidadesTotal,
    int NumeroVentas,
    List<ReporteProductoFilaDto> Productos,
    List<ReporteSinVentasDto> SinVentas
);

public record ReposicionItemDto(
    Guid ProductoId,
    string Nombre,
    string? Marca,
    int Stock,
    int StockReservado,
    int StockMinimo,
    int Vendidas30Dias,
    int Sugerido
);
