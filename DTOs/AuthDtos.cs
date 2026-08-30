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
public record RegistroClienteDto(string Nombre, string Email, string? Telefono, string Password, string? Slug = null);
