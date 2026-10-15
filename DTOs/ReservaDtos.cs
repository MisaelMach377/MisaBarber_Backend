namespace misabarber.DTOs;

// ClienteId solo lo usa el Admin (reserva a nombre de un cliente); si lo manda
// un Cliente, se ignora -- el cliente SIEMPRE sale del token.
public record ReservaCreateDto(Guid ProductoId, int Cantidad, string? Nota, Guid? ClienteId);

public record ReservaDto(
    Guid Id,
    Guid ProductoId,
    string ProductoNombre,
    string? ProductoMarca,
    string? ProductoFotoUrl,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteTelefono,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Total,
    string Estado,
    DateTime FechaCreacion,
    DateTime FechaVence,
    DateTime? FechaResolucion,
    string? ResueltoPorNombre,
    string? Nota,
    bool CreadaPorAdmin
);
