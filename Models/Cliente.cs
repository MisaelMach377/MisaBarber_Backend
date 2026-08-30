namespace misabarber.Models;

// Cliente de la barbería (el que agenda la cita). Sin login/portal propio
// todavía — a diferencia del Cliente/ClienteFinal de MisaDesk, acá es solo
// un registro de contacto, no tiene cuenta.
public class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece este cliente (multi-tenant, ver
    // Models/Negocio.cs). Todo controller que toque Clientes filtra
    // siempre por el NegocioId del usuario logueado.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? FotoUrl { get; set; }

    // Activo | Inactivo. Igual que en MisaDesk: si el cliente ya tiene citas
    // en su historial, no se puede borrar (ver ClientesController.Delete) —
    // Inactivo es la alternativa para "ocultarlo" sin perder ese historial.
    public string Estado { get; set; } = "Activo";

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
