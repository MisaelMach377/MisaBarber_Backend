namespace misabarber.DTOs;

public record ResenaDto(
    Guid Id,
    Guid CitaId,
    Guid ClienteId,
    string ClienteNombre,
    string? ClienteFotoUrl,
    Guid BarberoId,
    int Puntuacion,
    string? Comentario,
    DateTime FechaCreacion
);

public record ResenaCreateDto(int Puntuacion, string? Comentario);

// Resumen para la ficha de un Barbero: promedio + cuántas reseñas tiene,
// más la lista misma -- así BarberosController.GetResenas devuelve todo
// junto y el front no tiene que calcular el promedio a mano ni pedir dos
// endpoints distintos.
public record ResumenResenasDto(double Promedio, int Total, List<ResenaDto> Resenas);
