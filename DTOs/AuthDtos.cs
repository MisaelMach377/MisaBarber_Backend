namespace misabarber.DTOs;

// Slug es opcional: ausente o vacío = el negocio principal (la barbería
// original, la mía -- sigue entrando por /login sin nada más, ver
// AuthController.ResolverNegocio). Con slug = a qué barbería alquilada
// pertenece esta cuenta (ver Models/Negocio.cs).
public record LoginDto(string Email, string Password, string? Slug = null);

public record LoginResponseDto(string Token, UsuarioDto Usuario);

public record CambiarContrasenaDto(string ContrasenaActual, string ContrasenaNueva);

// Alta pública de un Cliente (auto-registro desde /login, panel "Sign
// Up"). Telefono es opcional a propósito -- no todos lo van a llenar en
// el primer paso, y no bloquea la creación de la cuenta. Slug: misma idea
// que en LoginDto -- a qué barbería se está registrando este cliente.
public record RegistroClienteDto(string Nombre, string Email, string? Telefono, string Password, string? Slug = null, string? EmailContacto = null);

// "Olvidé mi contraseña": paso 1 (pedir el link) y paso 2 (canjearlo).
// Slug: a qué barbería pertenece el correo (mismo criterio que LoginDto).
public record OlvideContrasenaDto(string Email, string? Slug = null);

// Cambiar el propio correo de recuperación: pide la contraseña actual para
// que una sesión robada no pueda redirigir la recuperación a otro correo.
public record ActualizarContactoDto(string? EmailContacto, string ContrasenaActual);

public record RestablecerContrasenaDto(string Token, string ContrasenaNueva);
