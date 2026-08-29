namespace misabarber.Models;

// Cuenta de acceso al sistema (login). Separada de Barbero/Cliente a
// propósito: no todo Barbero tiene cuenta todavía (BarberoId es el puente
// OPCIONAL entre "quién entra" y "de qué barbero son las citas que debe
// ver" — ver el filtro por rol en CitasController), y lo mismo con
// ClienteId para el rol Cliente (el puente entre la cuenta de login y la
// ficha de contacto que ya usaban las citas).
public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nombre { get; set; } = string.Empty;

    // Usado como usuario de login (único, se valida en AuthController /
    // UsuariosController). Se compara siempre en minúsculas.
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }

    // Admin | Barbero | Cliente. "Admin" ve y administra todo; "Barbero"
    // solo ve sus propias citas (CitasController filtra por BarberoId del
    // lado del servidor, no confía en lo que mande el front); "Cliente" es
    // el que se auto-registra desde /registro para reservar (ver
    // AuthController.Registro) y más adelante solo va a ver sus propias
    // citas, igual que un Barbero con las suyas.
    public string Rol { get; set; } = "Barbero";

    // Solo tiene sentido cuando Rol == "Barbero". Null para los demás.
    public Guid? BarberoId { get; set; }
    public Barbero? Barbero { get; set; }

    // Solo tiene sentido cuando Rol == "Cliente". Null para los demás.
    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Estado { get; set; } = "Activo"; // Activo | Inactivo

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
