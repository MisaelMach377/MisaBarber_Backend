namespace misabarber.DTOs;

public record BarberoDto(
    Guid Id,
    string Nombre,
    string? Telefono,
    string? Email,
    string? FotoUrl,
    string Estado,
    DateTime FechaCreacion,
    // null (no 0) cuando el barbero todavía no tiene ninguna reseña -- así
    // Barberos.jsx puede distinguir "sin calificar todavía" de "calificado
    // con promedio bajo", en vez de mostrar una estrellita vacía engañosa
    // para alguien que simplemente nunca recibió una reseña.
    double? PromedioResenas,
    int TotalResenas
);

public record BarberoCreateDto(string Nombre, string? Telefono, string? Email, string? FotoUrl);
