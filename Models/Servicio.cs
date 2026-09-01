namespace misabarber.Models;

public class Servicio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece este servicio (multi-tenant, ver
    // Models/Negocio.cs) -- cada barbería arma su propia carta de
    // servicios y precios, independiente de las demás.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public string Nombre { get; set; } = string.Empty; // "Corte clásico", "Barba", etc.

    // Qué incluye el servicio -- el Cliente la ve al tocar el ícono de
    // información al lado de cada servicio en MiCuenta.jsx (paso 1 de la
    // reserva), no es obligatoria: muchas barberías van a dejar el nombre
    // solo ("Corte clásico") sin necesitar explicar más.
    public string? Descripcion { get; set; }

    public decimal Precio { get; set; }

    // Duración estimada del servicio — CitasController la usa para calcular
    // el rango [FechaHora, FechaHora + DuracionMinutos) y evitar que se
    // agenden dos citas al mismo barbero que se pisen en el tiempo.
    public int DuracionMinutos { get; set; } = 30;

    public string Estado { get; set; } = "Activo"; // Activo | Inactivo

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
