namespace misabarber.DTOs;

public record LoginDto(string Email, string Password);

public record LoginResponseDto(string Token, UsuarioDto Usuario);

public record CambiarContrasenaDto(string ContrasenaActual, string ContrasenaNueva);

// Alta pública de un Cliente (auto-registro desde /login, panel "Sign
// Up"). Telefono es opcional a propósito -- no todos lo van a llenar en
// el primer paso, y no bloquea la creación de la cuenta.
public record RegistroClienteDto(string Nombre, string Email, string? Telefono, string Password);
