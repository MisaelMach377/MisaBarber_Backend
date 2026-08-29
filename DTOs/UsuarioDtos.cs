namespace misabarber.DTOs;

public record UsuarioDto(
    Guid Id,
    string Nombre,
    string Email,
    string? FotoUrl,
    string Rol,
    Guid? BarberoId,
    string? BarberoNombre,
    Guid? ClienteId,
    string Estado,
    DateTime FechaCreacion
);

public record UsuarioCreateDto(string Nombre, string Email, string Password, string Rol, Guid? BarberoId, string? FotoUrl);

public record UsuarioUpdateDto(string Nombre, string Email, string Rol, Guid? BarberoId, string? FotoUrl);

public record ResetearContrasenaDto(string ContrasenaNueva);
