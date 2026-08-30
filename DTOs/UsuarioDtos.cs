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
    DateTime FechaCreacion,
    string NegocioNombre,
    // Apariencia de SU negocio -- viajan acá (no en un endpoint aparte)
    // para que el panel entero se pinte con la marca correcta apenas
    // carga /auth/me, sin otro request. En UsuariosController.ToDto (la
    // lista de cuentas, no la sesión propia) van vacías, no hacen falta.
    string? NegocioLogoUrl,
    string NegocioColorPrimario,
    // Plan del negocio (Free|Pro) y la lista YA resuelta de módulos que
    // ESTE usuario puede ver (intersección de lo que permite el Plan y,
    // si es Barbero, lo que habilitó su Admin en Roles.jsx -- ver
    // AuthController.ModulosPara). Viaja resuelta para que Layout.jsx no
    // tenga que reimplementar esa lógica del lado del cliente.
    string NegocioPlan,
    string[] ModulosVisibles
);

public record UsuarioCreateDto(string Nombre, string Email, string Password, string Rol, Guid? BarberoId, string? FotoUrl);

public record UsuarioUpdateDto(string Nombre, string Email, string Rol, Guid? BarberoId, string? FotoUrl);

public record ResetearContrasenaDto(string ContrasenaNueva);
