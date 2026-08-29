using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.Models;
using misabarber.Utils;
using misabarber.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Railway ----
// Railway no fija un puerto: le pasa uno distinto a cada deploy en la
// variable PORT y espera que el contenedor escuche justo ahí (además, por
// defecto Kestrel bindea a localhost, que Railway no puede alcanzar desde
// afuera -- hace falta 0.0.0.0). Local (dotnet run / Visual Studio) nunca
// tiene PORT seteada -- ahí manda el profile de launchSettings.json como
// siempre, esto no le toca nada.
var puertoRailway = Environment.GetEnvironmentVariable("PORT");
var esRailway = !string.IsNullOrEmpty(puertoRailway);
if (esRailway)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{puertoRailway}");
}

// ---- Base de datos ----
builder.Services.AddDbContext<MisaBarberContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ---- Controllers + Swagger ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- Notificaciones push (Web Push + VAPID, ver Services/PushNotificationService.cs) ----
builder.Services.AddScoped<PushNotificationService>();

// ---- CORS (para el front en Vite) ----
// No hace falta AllowCredentials: la sesión viaja en el header
// Authorization (JWT guardado en el navegador, ver api/client.js del
// front), no en una cookie — así que no hay credenciales de por medio en
// el sentido que le importa a CORS.
var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ---- Pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Railway termina el HTTPS en su propio proxy y le manda al contenedor la
// request ya en HTTP plano -- redirigir acá adentro no suma nada (el
// usuario ya entró por HTTPS) y sin el middleware de ForwardedHeaders de
// por medio corre el riesgo clásico de loop de redirección. Local sigue
// redirigiendo como siempre.
if (!esRailway)
{
    app.UseHttpsRedirection();
}

// Sirve wwwroot/uploads/... (fotos de clientes/barberos que sube
// UploadController) como archivos estáticos públicos.
app.UseStaticFiles();

app.UseCors("Frontend");

// ---- Auth (JWT casero, ver Utils/JwtHelper.cs) ----
// Lee el header Authorization en cada request y, si el token es válido dado
// Jwt:Secret, deja los claims del usuario en HttpContext.Items para que
// RequiereAuthAttribute (y cualquier controller) los lean con
// HttpContext.UsuarioActual(). Un token ausente o inválido simplemente no
// deja nada ahí — son los [RequiereAuth] de cada controller los que
// deciden si eso es un problema (401), no este middleware.
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Secret en appsettings.");

app.Use(async (context, next) =>
{
    var header = context.Request.Headers["Authorization"].FirstOrDefault();
    if (header is not null && header.StartsWith("Bearer "))
    {
        var claims = JwtHelper.Validar(header["Bearer ".Length..], jwtSecret);
        if (claims is not null)
            context.Items["Usuario"] = claims;
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();

// ---- Migraciones + siembra del primer Admin ----
// Aplica migraciones pendientes al arrancar (así no hay que acordarse de
// correr "dotnet ef database update" a mano cada vez) y, si todavía no hay
// ningún usuario, crea un Admin inicial para poder entrar la primera vez.
// La contraseña es temporal: se espera cambiarla de inmediato desde "Mi
// perfil" una vez adentro.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MisaBarberContext>();
    await db.Database.MigrateAsync();

    if (!await db.Usuarios.AnyAsync())
    {
        db.Usuarios.Add(new Usuario
        {
            Nombre = "Administrador",
            Email = "admin@misabarber.com",
            PasswordHash = PasswordHasher.Hash("MisaBarber2026!"),
            Rol = "Admin",
        });
        await db.SaveChangesAsync();
    }
}

app.Run();
