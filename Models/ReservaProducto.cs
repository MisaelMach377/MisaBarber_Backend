namespace misabarber.Models;

// Reserva de un Producto hecha por un Cliente: "sepárame 2 unidades, paso a
// recogerlas". No cobra nada (se paga en la barbería al recoger) -- lo único
// que hace es APARTAR unidades: sube Producto.StockReservado, así el stock
// "disponible" (Stock - StockReservado) baja y nadie más puede reservar esas
// mismas unidades, pero el Stock físico no se toca hasta que el Admin marca
// la reserva como Entregada (ahí sí sale del inventario y queda un
// MovimientoStock "Salida" con el nombre del cliente).
//
// Ciclo de vida: Pendiente -> Entregada | Cancelada | Vencida. Una vez que
// sale de Pendiente no vuelve (es historial); por eso el cambio de estado se
// hace con un UPDATE condicional "WHERE Estado = 'Pendiente'" -- si el
// cliente cancela justo cuando el servicio de vencimiento la vence, solo uno
// gana y el stock reservado se libera una sola vez.
public class ReservaProducto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Multi-tenant (ver Models/Negocio.cs): se toma del token, nunca del body.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public Guid ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int Cantidad { get; set; }

    // Precio del producto AL MOMENTO de reservar: si el Admin lo sube después,
    // al cliente se le respeta lo que vio cuando reservó.
    public decimal PrecioUnitario { get; set; }

    public string Estado { get; set; } = "Pendiente"; // Pendiente | Entregada | Cancelada | Vencida

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Hasta cuándo se la guardamos. Pasada esta fecha, ReservasVencimientoService
    // la marca Vencida y devuelve las unidades al stock disponible.
    public DateTime FechaVence { get; set; }

    public DateTime? FechaResolucion { get; set; }

    // Quién la resolvió (Admin que entregó/canceló, o el propio cliente que la
    // canceló) -- denormalizado, mismo criterio que MovimientoStock. Null si la
    // venció el sistema.
    public string? ResueltoPorNombre { get; set; }

    // Mensaje opcional del cliente ("paso el sábado en la tarde").
    public string? Nota { get; set; }

    // true cuando ya se le mandó el push de "tu reserva vence hoy" -- para
    // que el servicio (que corre cada 10 min) no lo repita en cada vuelta.
    public bool AvisoVencimientoEnviado { get; set; }

    // true si la creó el Admin a nombre de un cliente (ej. por WhatsApp).
    public bool CreadaPorAdmin { get; set; }
}
