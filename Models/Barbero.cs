namespace misabarber.Models;

public class Barbero
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece este barbero (multi-tenant, ver
    // Models/Negocio.cs).
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? FotoUrl { get; set; }

    public string Estado { get; set; } = "Activo"; // Activo | Inactivo

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
