namespace misabarber.Models;

// Productos físicos que vende la barbería (cera, pomada, shampoo, etc.) --
// separado de Servicio a propósito: un Servicio es algo que se AGENDA
// (lleva DuracionMinutos, entra en una Cita), un Producto es algo que se
// VENDE en el mostrador (lleva Stock, no tiene horario ni barbero
// asociado). Por ahora es solo catálogo + inventario manual -- todavía no
// hay un flujo de "venta" que descuente Stock solo (eso sería el próximo
// paso si Misael lo pide: una tabla Venta/VentaProducto igual de separada
// de Cita, con su propio historial).
public class Producto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece (multi-tenant, ver Models/Negocio.cs) --
    // mismo criterio que Servicio/Barbero: cada negocio arma su propio
    // catálogo, independiente de los demás.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public string Nombre { get; set; } = string.Empty; // "Cera mate", "Pomada clásica", etc.
    public string? Marca { get; set; } // "American Crew", "Suavecito", etc. -- opcional.
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }

    // Cantidad disponible ahora mismo. Manual por ahora (el Admin la
    // ajusta a mano desde Productos.jsx cada vez que compra/vende) -- ver
    // el comentario de arriba sobre por qué no se descuenta sola todavía.
    public int Stock { get; set; }

    // Debajo (o igual) de este número el panel avisa "Stock bajo". Por producto
    // (una cera que rota mucho necesita avisar antes que una navaja). 5 = el
    // umbral fijo que tenía Productos.jsx, así los productos existentes no
    // cambian de comportamiento al migrar.
    public int StockMinimo { get; set; } = 5;

    // Unidades de Stock que están APARTADAS por reservas Pendientes (ver
    // Models/ReservaProducto.cs). Es un contador denormalizado a propósito:
    // recalcularlo con un SUM de reservas en cada lectura del catálogo sería
    // caro y, peor, no se podría proteger con un UPDATE atómico. Disponible
    // para reservar = Stock - StockReservado; el invariante es
    // 0 <= StockReservado <= Stock (lo cuidan las condiciones de cada UPDATE).
    public int StockReservado { get; set; }

    public string? FotoUrl { get; set; }

    public string Estado { get; set; } = "Activo"; // Activo | Inactivo

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
