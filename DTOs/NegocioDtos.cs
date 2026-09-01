namespace misabarber.DTOs;

public record NegocioDto(
    Guid Id, string Nombre, string? Slug, bool EsPrincipal, string Estado, DateTime FechaCreacion,
    string? LogoUrl, string ColorPrimario, string Plan
);

public record ActualizarPlanNegocioDto(string Plan); // Free | Pro, ver Utils/Modulos.cs

// Config de "qué ve un Barbero de este negocio" -- ModulosDisponibles es
// lo que el Plan actual permite en total (ver Utils/Modulos.DeNegocio),
// para que Roles.jsx sepa qué mostrar bloqueado/grisado en vez de
// dejar tildar algo que el Plan ni siquiera tiene.
public record RolesDto(string[] ModulosBarbero, string[] ModulosDisponibles);
public record ActualizarRolesDto(string[] ModulosBarbero);

// Lo que ve la pantalla de login/registro ANTES de autenticarse (ver
// AuthController.ObtenerNegocioPublico) -- a propósito lleva solo lo
// necesario para pintar la marca, nada de Estado/Id/FechaCreacion que no
// le sirven a un visitante anónimo.
public record NegocioPublicoDto(string Nombre, string? LogoUrl, string ColorPrimario);

// Apariencia editable de la barbería -- la usan MiNegocioController (el
// propio Admin/SuperAdmin editando SU negocio) y NegociosController (el
// SuperAdmin editando cualquiera desde la lista).
// Slug/EsPrincipal viajan de SOLO LECTURA acá -- el Admin no los edita
// desde Apariencia (eso sigue siendo del SuperAdmin en NegociosController),
// pero Apariencia.jsx los necesita para armar el link/QR de login y
// registro de SU barbería (ver el comentario de Negocio.Slug).
public record AparienciaDto(string NombreNegocio, string? LogoUrl, string ColorPrimario, string? Slug, bool EsPrincipal);
public record ActualizarAparienciaDto(string? LogoUrl, string ColorPrimario);

// Alta de una barbería nueva (alquiler) + su primer usuario Admin, en un
// solo paso -- lo usa NegociosController, exclusivo del SuperAdmin (yo).
// Slug es obligatorio acá (a diferencia de LoginDto/RegistroClienteDto):
// todo negocio nuevo que se cree por acá NO es el principal, así que
// siempre necesita una URL propia.
public record NegocioCreateDto(
    string NombreNegocio,
    string Slug,
    string NombreAdmin,
    string EmailAdmin,
    string PasswordAdmin
);

public record CambiarEstadoNegocioDto(string Estado); // Activo | Inactivo
