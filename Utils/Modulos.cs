namespace misabarber.Utils;

// Módulos "de negocio" configurables -- separados a propósito de Usuarios/
// Roles/Negocios, que son administración de cuentas y quedan siempre
// disponibles para un Admin sin importar el plan (ver AuthController.
// ModulosPara). Un módulo entra acá solo si tiene sentido como feature que
// se puede vender por plan (Reportes, Apariencia) o restringir por rol
// (Negocio.ModulosBarbero, ver MiNegocioController) -- el nombre de cada
// string tiene que coincidir letra por letra con el "to" de navItems en
// Layout.jsx (sin la barra), porque ahí es donde se compara.
public static class Modulos
{
    public static readonly string[] Todos =
        { "Citas", "Clientes", "Barberos", "Servicios", "Productos", "Historial", "Reportes", "Apariencia", "Chat" };

    // Free = lo básico para operar el día a día. Pro suma reportes/
    // historial (análisis) y apariencia (marca propia) -- ver la
    // conversación con Misael: son features, no el manejo de su propio
    // equipo (eso -- Usuarios, Roles -- no se paywallea). Productos entra
    // en Free por el mismo motivo que Servicios: vender cera/pomada en el
    // mostrador es operar el día a día, no un feature de análisis/marca.
    public static readonly Dictionary<string, string[]> PorPlan = new()
    {
        ["Free"] = new[] { "Citas", "Clientes", "Barberos", "Servicios", "Productos" },
        ["Pro"] = Todos,
    };

    public static readonly string[] PlanesValidos = { "Free", "Pro" };

    public static string[] DeNegocio(string? plan) =>
        PorPlan.TryGetValue(plan ?? "", out var modulos) ? modulos : PorPlan["Pro"];
}
