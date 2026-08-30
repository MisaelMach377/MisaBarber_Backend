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

    // A qué barbería pertenece esta cuenta (multi-tenant, ver
    // Models/Negocio.cs). El Email ya no es único a nivel global: es único
    // POR Negocio (dos barberías distintas pueden tener cada una un
    // usuario con el mismo correo, ver el índice compuesto en
    // MisaBarberContext.OnModelCreating).
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // Usado como usuario de login (único DENTRO del Negocio, se valida en
    // AuthController / UsuariosController). Se compara siempre en minúsculas.
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }

    // SuperAdmin | Admin | Barbero | Cliente. "Admin" ve y administra todo
    // DENTRO de su propio Negocio; "Barbero" solo ve sus propias citas
    // (CitasController filtra por BarberoId del lado del servidor, no
    // confía en lo que mande el front); "Cliente" es el que se
    // auto-registra desde /registro para reservar (ver
    // AuthController.Registro) y solo ve sus propias citas, igual que un
    // Barbero con las suyas. "SuperAdmin" es un Admin con un poder extra:
    // además de administrar su propio Negocio (siempre el principal --
    // ver NegociosController.Create, que nunca crea cuentas con este rol
    // para un negocio alquilado), puede crear/suspender OTROS negocios.
    // RequiereAuthAttribute trata "SuperAdmin" como si cumpliera
    // [RequiereAuth(Rol = "Admin")] en todos lados (ver ese archivo) --
    // no hace falta duplicar cada chequeo de "Admin" para incluirlo.
    // Solo un SuperAdmin puede asignarle este rol a otra cuenta (ver
    // UsuariosController.Create/Update), para que un Admin normal no se
    // lo pueda dar a sí mismo eligiéndolo del select.
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
