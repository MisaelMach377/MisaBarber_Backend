namespace misabarber.Models;

// Un negocio = una barbería que usa el sistema (multi-tenant: todos
// comparten la misma base de datos y el mismo backend, pero cada fila de
// Cliente/Barbero/Servicio/Cita/CitaAuditoria/Usuario pertenece a UN solo
// Negocio, y cada request solo puede ver/tocar los datos del Negocio del
// usuario logueado -- el filtro por NegocioId se hace del lado del
// servidor en cada controller, nunca se confía en lo que mande el front).
// Así una persona puede alquilar el sistema para su propia barbería sin
// tocar ni ver los datos de las demás que ya lo usan.
public class Negocio
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nombre { get; set; } = string.Empty;

    // Identifica al negocio en las rutas públicas de login/registro/reserva
    // (ej. misabarber.netlify.app/la-mejor-barberia/registro). El negocio
    // "principal" (la barbería original, la mía) tiene EsPrincipal = true y
    // Slug = null -- sigue entrando por /login y /registro tal cual, sin
    // slug, para no romper los links que ya están publicados. Los negocios
    // que se alquilen de acá en adelante sí llevan Slug (ver
    // AuthController.ResolverNegocio).
    public string? Slug { get; set; }
    public bool EsPrincipal { get; set; } = false;

    // Activo | Inactivo -- para poder suspender el acceso de un negocio
    // que deja de pagar el alquiler (bloquea su login, ver
    // AuthController.ResolverNegocio), sin borrar sus datos.
    public string Estado { get; set; } = "Activo";

    // Apariencia propia de cada barbería (logo + color de acento), para
    // que la pantalla de login/registro (AuthController.ObtenerNegocioPublico,
    // pública -- todavía no hay sesión) y todo el panel una vez adentro
    // (UsuarioDto.NegocioLogoUrl/NegocioColorPrimario, ver AuthController.Me)
    // se vean con su propia marca en vez de la de MisaBarber. LogoUrl es
    // relativa (la sube UploadController a /uploads/negocios/) igual que
    // Usuario.FotoUrl. La configura el propio Admin de la barbería desde
    // MiNegocioController, o el SuperAdmin desde NegociosController.
    public string? LogoUrl { get; set; }
    public string ColorPrimario { get; set; } = "#2563eb";

    // Free | Pro -- qué módulos de negocio tiene disponibles esta
    // barbería (ver Utils/Modulos.cs), lo asigna el SuperAdmin a mano
    // desde Negocios.jsx (sin cobro automático todavía). Los negocios que
    // ya existían al agregar esto quedan en "Pro" -- no tiene sentido que
    // a alguien que ya estaba usando Reportes/Apariencia se los saque una
    // migración.
    public string Plan { get; set; } = "Pro";

    // Qué módulos (de los que además permita el Plan) puede ver un
    // Barbero de ESTE negocio -- lo configura el propio Admin desde
    // Roles.jsx, separado del Plan porque son dos preguntas distintas:
    // el Plan es "qué compró la barbería", esto es "qué le muestro a mi
    // empleado". Lista separada por comas (ver AuthController.ModulosPara)
    // -- Admin/SuperAdmin nunca se filtran por acá, siempre ven todo lo
    // que el Plan permite.
    public string ModulosBarbero { get; set; } = "Citas,Clientes,Historial";

    // Dirección física del local -- se usa para pintar el mapa embebido
    // en la pantalla del cliente (MiCuenta.jsx, sección "Encuéntranos
    // aquí") vía el embed de Google Maps por texto libre
    // (https://www.google.com/maps?q=<direccion>&output=embed), que NO
    // necesita API key -- evita meter una key de Google en el proyecto
    // solo para mostrar un mapa de solo lectura. Nullable a propósito: es
    // información operativa básica (como el Horario), no un feature de
    // Plan Pro, así que se edita en MiNegocioController sin pasar por
    // NegocioConModulo -- pero mientras el Admin no la cargue, no hay
    // dirección exacta que buscar y la sección del cliente simplemente
    // no se muestra (ver UsuarioDto.NegocioDireccion).
    public string? Direccion { get; set; }

    // Coordenadas exactas del local, elegidas por el Admin haciendo click
    // en el mapa interactivo de Apariencia.jsx (Leaflet + OpenStreetMap,
    // sin API key -- ver components/Mapa/MapaUbicacion.jsx) en vez de
    // tener que escribir lat/long a mano. Cuando están cargadas, el mapa
    // del cliente (MiCuenta.jsx) las usa directo para poner el pin exacto;
    // si todavía no las cargó pero sí escribió Direccion como texto, cae
    // al embed de Google Maps por búsqueda de texto (ver el comentario de
    // Direccion arriba) -- así ningún negocio se queda sin mapa mientras
    // el Admin no haga el click.
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
