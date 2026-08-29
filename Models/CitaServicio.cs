namespace misabarber.Models;

// Tabla intermedia para la relación muchos-a-muchos entre Cita y Servicio
// (una cita ahora puede llevar varios servicios a la vez, ej. "Corte" +
// "Barba"). Es una entidad propia, no un many-to-many "skip navigation" de
// EF Core, a propósito: así el nombre de la tabla, sus columnas y sus FKs
// quedan explícitos acá y en la migración escrita a mano, en vez de
// depender de una convención automática de EF que sería más frágil de
// reproducir sin el CLI de `dotnet ef` (ver MisaBarberContext.OnModelCreating).
public class CitaServicio
{
    public Guid CitaId { get; set; }
    public Cita? Cita { get; set; }

    public Guid ServicioId { get; set; }
    public Servicio? Servicio { get; set; }
}
