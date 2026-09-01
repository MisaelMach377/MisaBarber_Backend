namespace misabarber.Models;

// Horario semanal de UN barbero -- una fila por día de la semana por
// Barbero (misma convención de DiaSemana que HorarioNegocio). Se siembran
// las 7 al crear el barbero (Trabaja = true, sin horario propio) para que
// por defecto trabaje todos los días con el mismo horario del negocio
// hasta que el Admin lo edite explícitamente desde Barberos.jsx (pestaña
// Horarios) -- así un barbero recién creado no queda invisible para
// agendar por falta de configuración.
public class HorarioBarbero
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BarberoId { get; set; }
    public Barbero? Barbero { get; set; }

    public int DiaSemana { get; set; }

    // Si el barbero no trabaja ESE día (ej. solo entra martes y jueves),
    // no se ofrece como opción al armar una cita ese día, así el negocio
    // esté abierto.
    public bool Trabaja { get; set; } = true;

    // Null = "ese día usa el mismo horario que el negocio" (caso normal,
    // ver Utils/Horarios.cs). Si el Admin pone valores acá es un horario
    // PROPIO más corto (ej. medio tiempo) -- CitasController.
    // GetDisponibilidad lo intersecta con el del negocio, así nunca puede
    // quedar trabajando fuera del horario en que el negocio está abierto
    // aunque el Admin le ponga un horario más amplio por error.
    public TimeSpan? HoraInicio { get; set; }
    public TimeSpan? HoraFin { get; set; }
}
