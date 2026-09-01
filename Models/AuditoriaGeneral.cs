namespace misabarber.Models;

// Registro de auditoría de acciones administrativas fuera de Citas
// (Barberos, Clientes, Usuarios, Mi Negocio) -- ver CitaAuditoria.cs para
// el equivalente específico de Citas, que se mantiene aparte porque su
// forma (ClienteNombre/BarberoNombre/ServicioNombre) es propia de ese
// dominio y ya tenía su propia pantalla (Historial.jsx) antes de que
// existiera esta. Acá es más genérica a propósito -- una fila por evento,
// con quién lo hizo y una descripción en texto de qué entidad tocó -- para
// poder cubrir varias entidades sin necesitar una tabla por cada una.
public class AuditoriaGeneral
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // A qué barbería pertenece este evento (multi-tenant, ver
    // Models/Negocio.cs) -- se toma del NegocioId del autor (el token),
    // nunca de algo que mande el body del request.
    public Guid NegocioId { get; set; }
    public Negocio? Negocio { get; set; }

    // "Barbero" | "Cliente" | "Usuario" | "Negocio" -- qué tipo de ficha
    // tocó este evento.
    public string Entidad { get; set; } = string.Empty;

    // Id de la ficha afectada -- sin FK real (misma razón que CitaId en
    // CitaAuditoria: el registro histórico sigue siendo legible aunque esa
    // ficha se termine borrando después).
    public Guid? EntidadId { get; set; }
    public string EntidadNombre { get; set; } = string.Empty;

    // "Creado" | "Editado" | "Eliminado" | "Estado: Activo -> Inactivo" | etc.
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }

    // Quién lo hizo -- denormalizado (mismo criterio que ClienteNombre en
    // CitaAuditoria/ChatMensaje): el nombre del autor queda legible en el
    // historial aunque esa cuenta de Usuario se borre después.
    public Guid AutorId { get; set; }
    public string AutorNombre { get; set; } = string.Empty;

    public DateTime FechaHoraEvento { get; set; } = DateTime.UtcNow;
}
