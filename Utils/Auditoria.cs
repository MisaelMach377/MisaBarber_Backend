using misabarber.Data;
using misabarber.Models;

namespace misabarber.Utils;

// Registra un evento en AuditoriaGeneral (ver Models/AuditoriaGeneral.cs)
// -- centralizado acá para no repetir el mismo bloque en cada controller
// que necesita auditar (Barberos, Clientes, Usuarios, Mi Negocio). Mismo
// criterio que CitasController.RegistrarAuditoria: no hace SaveChanges
// acá -- queda pendiente en el DbContext para guardarse junto con el
// cambio principal en un solo SaveChangesAsync, en la misma transacción.
public static class Auditoria
{
    public static void Registrar(
        MisaBarberContext db,
        UsuarioClaims autor,
        string entidad,
        Guid? entidadId,
        string entidadNombre,
        string accion,
        string? detalle = null)
    {
        db.AuditoriaGeneral.Add(new AuditoriaGeneral
        {
            NegocioId = autor.NegocioId,
            Entidad = entidad,
            EntidadId = entidadId,
            EntidadNombre = entidadNombre,
            Accion = accion,
            Detalle = detalle,
            AutorId = autor.Id,
            AutorNombre = autor.Nombre,
        });
    }
}
