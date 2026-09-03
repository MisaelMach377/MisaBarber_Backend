namespace misabarber.Models;

// Reseña que un Cliente deja sobre una Cita ya Completada (ver
// Models/Cita.cs). Cuelga de la Cita -- no de "el barbero" directo -- para
// que solo se pueda calificar algo que de verdad pasó (una cita real,
// terminada), no un formulario suelto. BarberoId igual se guarda acá
// TAMBIÉN, denormalizado (mismo criterio que NegocioId en Cita): así
// BarberosController puede sacar "promedio de este barbero" con una sola
// query filtrando por BarberoId, sin tener que pasar por Cita en cada
// consulta.
public class Resena
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece (multi-tenant, ver Models/Negocio.cs) --
    // mismo criterio que el resto de entidades: se guarda directo acá y no
    // solo se infiere por Cita, para filtrar/auditar sin join.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    // Una Cita solo puede tener UNA reseña -- se refuerza con un índice
    // único sobre CitaId (ver MisaBarberContext.OnModelCreating), no solo
    // confiando en que CitasController.CrearResena revise antes de insertar.
    public Guid CitaId { get; set; }
    public Cita? Cita { get; set; }

    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public Guid BarberoId { get; set; }
    public Barbero? Barbero { get; set; }

    // 1 a 5 -- se valida el rango en el controller, no acá (mismo criterio
    // que el resto del modelo: las entidades son datos, la validación de
    // negocio vive en el controller).
    public int Puntuacion { get; set; }

    public string? Comentario { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
