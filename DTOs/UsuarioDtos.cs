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
    string[] ModulosVisibles,
    // Horario semanal del negocio (7 filas, ver DTOs/HorarioDtos.cs) --
    // viaja acá para que la pantalla del cliente (MiCuenta.jsx) pueda
    // mostrar "Atendemos de Lunes a Domingo, 9:00-19:00" sin otro
    // request. Barbero/Admin también lo reciben aunque no lo usen todavía
    // -- es información pública del negocio, no hace falta filtrarla por rol.
    List<HorarioNegocioDiaDto> NegocioHorario,
    // Dirección del local (puede ser null si el Admin todavía no la
    // cargó desde Apariencia.jsx) -- viaja acá por la misma razón que
    // NegocioHorario un poco más arriba: para que MiCuenta.jsx pinte el
    // mapa embebido de "Encuéntranos aquí" sin un request aparte.
    string? NegocioDireccion,
    // Coordenadas exactas (click en el mapa, ver Models/Negocio.cs) --
    // cuando están presentes, MiCuenta.jsx las prefiere sobre
    // NegocioDireccion para pintar el pin, porque son precisas y no
    // dependen de que Google adivine bien una búsqueda de texto.
    double? NegocioLatitud,
    double? NegocioLongitud
);

public record UsuarioCreateDto(string Nombre, string Email, string Password, string Rol, Guid? BarberoId, string? FotoUrl);

public record UsuarioUpdateDto(string Nombre, string Email, string Rol, Guid? BarberoId, string? FotoUrl);

public record ResetearContrasenaDto(string ContrasenaNueva);
