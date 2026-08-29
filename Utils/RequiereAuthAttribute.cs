using Microsoft.AspNetCore.Mvc.Filters;

namespace misabarber.Utils;

// Filtro de autorización hecho a mano (en vez de [Authorize] +
// AddAuthentication().AddJwtBearer(), que vienen del paquete
// Microsoft.AspNetCore.Authentication.JwtBearer — no disponible en este
// entorno sin acceso a NuGet, ver JwtHelper.cs). Lee el usuario que el
// middleware de Program.cs ya dejó en HttpContext.Items (a partir del
// token validado con JwtHelper.Validar) y corta la request con 401/403 si
// no corresponde.
//
// Uso: [RequiereAuth] en la clase o la acción para exigir cualquier sesión
// válida, o [RequiereAuth(Rol = "Admin")] para restringir a un rol
// puntual (ver UsuariosController).
public class RequiereAuthAttribute : Attribute, IAuthorizationFilter
{
    public string? Rol { get; set; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var usuario = context.HttpContext.UsuarioActual();

        if (usuario is null)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        if (Rol is not null && usuario.Rol != Rol)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(
                "No tienes permiso para hacer esto.")
            { StatusCode = 403 };
        }
    }
}

public static class HttpContextExtensions
{
    public static UsuarioClaims? UsuarioActual(this Microsoft.AspNetCore.Http.HttpContext context) =>
        context.Items.TryGetValue("Usuario", out var v) ? v as UsuarioClaims : null;
}
