namespace misabarber.Models;

// Una venta de producto ya concretada: salió del inventario y se cobró (en
// mano). Nace de dos lugares -- venta directa en mostrador (Origen "Mostrador")
// o una reserva entregada (Origen "Reserva") -- y por eso los reportes de
// productos leen de ESTA tabla y no del historial de stock: MovimientoStock
// sabe cuántas unidades se movieron, pero no a qué precio se vendieron.
//
// Append-only. Nombres del producto/cliente/usuario denormalizados a propósito:
// si después se borra el producto o el cliente, la venta (y los ingresos del
// reporte) no desaparecen -- por eso las FK son SetNull y no Cascade.
public class VentaProducto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public Guid? ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;

    // Opcional: una venta de mostrador puede ser a alguien que no está registrado.
    public Guid? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }

    public int Cantidad { get; set; }

    // Precio al que se vendió (por defecto el del catálogo, editable por si
    // se hizo un descuento) y el total ya calculado, guardado para que el
    // reporte sume sin recalcular.
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }

    public string Origen { get; set; } = "Mostrador"; // Mostrador | Reserva
    public Guid? ReservaId { get; set; }

    public Guid UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
