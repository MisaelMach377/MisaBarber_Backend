namespace misabarber.DTOs;

// AutorRol viaja tal cual ("Cliente" | "Admin" | "Barbero") para que cada
// lado (Chat.jsx del staff, ChatWidget.jsx del cliente) sepa de qué lado
// de la burbuja mostrar cada mensaje comparando contra SU propio rol, sin
// que el backend tenga que calcular un booleano "EsPropio" por request.
public record ChatMensajeDto(Guid Id, string AutorNombre, string AutorRol, string Texto, DateTime FechaEnvio);

public record EnviarMensajeDto(string Texto);

// Una fila del inbox del staff: un cliente con el que hay al menos un
// mensaje intercambiado, con la última línea y cuántos de ESE cliente
// todavía no se leyeron (ver ChatController.GetConversaciones).
public record ChatConversacionDto(Guid ClienteId, string ClienteNombre, string? UltimoMensaje, DateTime? UltimoMensajeFecha, int NoLeidos);
