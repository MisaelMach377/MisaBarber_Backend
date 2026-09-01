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

    public string? FotoUrl { get; set; }

    public string Estado { get; set; } = "Activo"; // Activo | Inactivo

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
