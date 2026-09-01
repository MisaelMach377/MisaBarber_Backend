using System.Globalization;
using misabarber.Models;

namespace misabarber.Utils;

// Siembra las 7 filas (una por día de la semana, misma convención que
// System.DayOfWeek: 0=Domingo..6=Sábado) de horario para un Negocio o
// Barbero recién creado (ver NegociosController.Create y
// BarberosController.Create) -- así GetDisponibilidad (CitasController)
// siempre encuentra una fila para cada día y nunca tiene que adivinar un
// default a medio camino del cálculo. El Negocio nace abierto todos los
// días 9am-7pm (el horario fijo que ya se usaba antes de que esto
// existiera, para no sorprender a nadie con un cambio de comportamiento);
// el Barbero nace trabajando todos los días sin horario propio (null =
// usa el del negocio ese día, ver Models/HorarioBarbero.cs).
public static class Horarios
{
    public static List<HorarioNegocio> SembrarNegocio(Guid negocioId) =>
        Enumerable.Range(0, 7).Select(dia => new HorarioNegocio
        {
            NegocioId = negocioId,
            DiaSemana = dia,
            Abierto = true,
            HoraInicio = new TimeSpan(9, 0, 0),
            HoraFin = new TimeSpan(19, 0, 0),
        }).ToList();

    public static List<HorarioBarbero> SembrarBarbero(Guid barberoId) =>
        Enumerable.Range(0, 7).Select(dia => new HorarioBarbero
        {
            BarberoId = barberoId,
            DiaSemana = dia,
            Trabaja = true,
            HoraInicio = null,
            HoraFin = null,
        }).ToList();

    // "HH:mm" en vez de TimeSpan crudo en los DTOs (ver DTOs/HorarioDtos.cs)
    // -- coincide 1 a 1 con lo que manda/lee <input type="time"> del lado
    // del front, así ninguno de los dos lados tiene que traducir el
    // formato interno de TimeSpan de .NET (que acepta "d.hh:mm:ss" y otras
    // variantes que acá no queremos permitir).
    public static bool TryParseHora(string? valor, out TimeSpan hora) =>
        TimeSpan.TryParseExact(valor, @"hh\:mm", CultureInfo.InvariantCulture, out hora);

    public static string FormatHora(TimeSpan hora) => hora.ToString(@"hh\:mm");
}
