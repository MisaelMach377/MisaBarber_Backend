namespace misabarber.DTOs;

// Un día de la semana en el horario del NEGOCIO (DiaSemana: 0=Domingo..
// 6=Sábado, igual que System.DayOfWeek). HoraInicio/HoraFin viajan como
// "HH:mm" (mismo formato que devuelve <input type="time"> en el front),
// no como TimeSpan crudo, para no arrastrar el formato interno de .NET al
// JSON.
public record HorarioNegocioDiaDto(int DiaSemana, bool Abierto, string HoraInicio, string HoraFin);

public record ActualizarHorarioNegocioDto(List<HorarioNegocioDiaDto> Dias);

// Un día de la semana en el horario de UN BARBERO. HoraInicio/HoraFin en
// null = "ese día usa el mismo horario que el negocio" (ver
// Models/HorarioBarbero.cs) -- solo llevan valor cuando el Admin le puso
// un horario propio más corto a ese barbero puntual.
public record HorarioBarberoDiaDto(int DiaSemana, bool Trabaja, string? HoraInicio, string? HoraFin);

public record ActualizarHorarioBarberoDto(List<HorarioBarberoDiaDto> Dias);

// Respuesta de GetDisponibilidad -- antes era solo List<string> con las
// horas libres; ahora viaja también el Motivo por el que puede estar
// vacía (NegocioCerrado | BarberoNoTrabaja | null), así el front le
// muestra al cliente/admin un mensaje específico ("Este día el negocio no
// atiende") en vez del genérico "no quedan horarios libres", que sugiere
// que sí se puede reservar otro horario ese mismo día cuando en realidad
// no hay forma.
public record DisponibilidadDto(List<string> Horas, string? Motivo);
