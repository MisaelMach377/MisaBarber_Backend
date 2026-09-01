namespace misabarber.Models;

// Horario semanal de atención del NEGOCIO: una fila por día de la semana
// (DiaSemana usa la misma convención que System.DayOfWeek -- 0=Domingo,
// 1=Lunes, ..., 6=Sábado -- para no tener que traducir nada al comparar
// contra fecha.DayOfWeek en CitasController). Siempre existen las 7 filas
// por Negocio -- se siembran solas al crear el Negocio (ver Utils/
// Horarios.cs + NegociosController.Create) y se backfillean para los
// negocios que ya existían en la migración que agrega esta tabla -- así
// GetDisponibilidad nunca tiene que adivinar un default a medio cálculo:
// si no hay fila, es un bug de seeding, no un caso normal a contemplar.
public class HorarioNegocio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    public int DiaSemana { get; set; }

    // Si el negocio no atiende ese día (ej. Domingo cerrado), no hay
    // horas para reservar con NINGÚN barbero ese día, sin importar su
    // horario individual (ver HorarioBarbero).
    public bool Abierto { get; set; } = true;

    public TimeSpan HoraInicio { get; set; } = new TimeSpan(9, 0, 0);
    public TimeSpan HoraFin { get; set; } = new TimeSpan(19, 0, 0);
}
