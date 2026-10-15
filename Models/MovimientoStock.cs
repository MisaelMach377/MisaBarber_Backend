namespace misabarber.Models;

// Historial de cambios de Stock de un Producto: una fila por cada entrada,
// salida o ajuste. Responde "¿por qué está en 7 si ayer eran 12?" -- antes el
// Stock era un número suelto que el Admin pisaba a mano y no quedaba rastro.
// Producto.Stock sigue siendo la fuente de verdad del stock actual; esta tabla
// es el rastro (append-only: no se edita ni se borra una fila).
//
// Más adelante, las reservas y las ventas van a generar sus propios movimientos
// (Salida por entrega de una reserva), por eso Tipo es texto abierto y no solo
// un bool entrada/salida.
public class MovimientoStock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenant (ver Models/Negocio.cs): se toma del token, nunca del body.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public Guid ProductoId { get; set; }
    public Producto? Producto { get; set; }

    // "Entrada" (reposición/compra) | "Salida" (venta, uso, merma) |
    // "Ajuste" (corrección tras contar el inventario).
    public string Tipo { get; set; } = string.Empty;

    // Cambio con signo: +5 entró, -2 salió. Para un Ajuste es la diferencia
    // contra el stock que había (nuevo - anterior).
    public int Cantidad { get; set; }

    // Stock que quedó después de este movimiento -- guardado para leer el
    // historial sin tener que recalcularlo sumando todo desde el inicio.
    public int StockResultante { get; set; }

    public string? Motivo { get; set; } // "Venta", "Reposición", "Merma", "Conteo"...

    // Quién lo hizo -- denormalizado (mismo criterio que AuditoriaGeneral): el
    // nombre queda legible aunque esa cuenta se borre después.
    public Guid UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
