namespace misabarber.Models;

// Un link de "Olvidé mi contraseña" ya emitido. Del token real (el que viaja
// por correo) NO se guarda nada: solo su hash SHA-256 (TokenHash). Así, si
// alguien llega a leer la tabla, no puede armar links válidos -- mismo
// criterio que PasswordHash en Usuario. Un solo uso (UsadoEn) y vence rápido
// (Expira), para que un correo viejo olvidado en una bandeja no sirva de nada.
public class RecuperacionContrasena
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime Creado { get; set; } = DateTime.UtcNow;
    public DateTime Expira { get; set; }
    public DateTime? UsadoEn { get; set; }
}
