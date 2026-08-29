using Microsoft.EntityFrameworkCore;
using misabarber.Models;

namespace misabarber.Data;

public class MisaBarberContext : DbContext
{
    public MisaBarberContext(DbContextOptions<MisaBarberContext> options) : base(options) { }

    // Mapea TODO DateTime del modelo a "timestamp without time zone" +
    // normaliza su Kind a Unspecified (ver Data/DateTimeConverters.cs) —
    // así no hay que acordarse de hacerlo campo por campo, y da igual si el
    // valor viene de DateTime.UtcNow, DateTime.Today o del JSON del front.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UnspecifiedDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<UnspecifiedNullableDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Barbero> Barberos => Set<Barbero>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<CitaServicio> CitaServicios => Set<CitaServicio>();
    public DbSet<CitaAuditoria> CitasAuditoria => Set<CitaAuditoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Precio con precisión fija — sin esto, Npgsql mapea decimal a
        // "numeric" sin precisión definida y tira un warning en cada
        // arranque (y en algunos motores puede truncar mal).
        modelBuilder.Entity<Servicio>()
            .Property(s => s.Precio)
            .HasPrecision(10, 2);

        // Restrict (no Cascade) en las 3 FKs de Cita: si un Cliente/Barbero/
        // Servicio ya tiene citas, no se puede borrar sin querer y dejar
        // citas huérfanas — mismo criterio que ClienteFinal en MisaDesk
        // (ver ClientesController/BarberosController/ServiciosController.
        // Delete: revisan si hay citas antes de borrar y sugieren Inactivo).
        modelBuilder.Entity<Cita>()
            .HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cita>()
            .HasOne(c => c.Barbero)
            .WithMany()
            .HasForeignKey(c => c.BarberoId)
            .OnDelete(DeleteBehavior.Restrict);

        // CitaServicio: clave compuesta (CitaId, ServicioId) -- no necesita un
        // Id propio, la pareja ya es única por definición. Cascade en CitaId
        // porque una fila de CitaServicio no tiene sentido sin su Cita (si
        // se borra la Cita -- solo posible en Pendiente/Cancelada, ver
        // CitasController.Delete -- sus servicios asociados se van con
        // ella). Restrict en ServicioId, mismo criterio que antes: no se
        // puede borrar un Servicio que ya está usado en alguna cita.
        modelBuilder.Entity<CitaServicio>()
            .HasKey(cs => new { cs.CitaId, cs.ServicioId });

        modelBuilder.Entity<CitaServicio>()
            .HasOne(cs => cs.Cita)
            .WithMany(c => c.CitaServicios)
            .HasForeignKey(cs => cs.CitaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CitaServicio>()
            .HasOne(cs => cs.Servicio)
            .WithMany()
            .HasForeignKey(cs => cs.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Un barbero no puede tener dos citas activas exactamente a la misma
        // hora de inicio (el solape parcial ya lo valida CitasController a
        // mano; esto es una segunda red de seguridad a nivel de BD contra
        // dos requests simultáneos pegándole al mismo slot exacto).
        modelBuilder.Entity<Cita>()
            .HasIndex(c => new { c.BarberoId, c.FechaHora });

        // CitaAuditoria: sin navegación a Cita a propósito (ver Models/
        // CitaAuditoria.cs) — solo un índice para que filtrar por CitaId o
        // por fecha del evento no haga table scan cuando el historial crezca.
        modelBuilder.Entity<CitaAuditoria>()
            .HasIndex(a => a.CitaId);
        modelBuilder.Entity<CitaAuditoria>()
            .HasIndex(a => a.FechaHoraEvento);

        // Usuario.Email es el usuario de login: único a nivel de BD como
        // segunda red de seguridad además de la validación en
        // AuthController/UsuariosController (evita una condición de
        // carrera si dos requests de alta llegan casi al mismo tiempo).
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // SetNull (no Restrict) en Usuario->Barbero: si se elimina la
        // ficha de un Barbero, la cuenta de login no debería bloquear ese
        // borrado — simplemente se queda sin BarberoId asociado (el Admin
        // tendría que volver a vincularla a otro barbero o desactivarla).
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Barbero)
            .WithMany()
            .HasForeignKey(u => u.BarberoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mismo criterio que Usuario->Barbero, ahora para el rol Cliente:
        // si se borra la ficha de Cliente, la cuenta de login se queda
        // simplemente sin ClienteId (no bloquea el borrado).
        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Cliente)
            .WithMany()
            .HasForeignKey(u => u.ClienteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Una suscripción push queda huérfana sin sentido si se borra la
        // cuenta que la generó -- Cascade acá sí corresponde (a diferencia
        // de Usuario->Barbero/Cliente, que son SetNull): no hay "usuario
        // sin cuenta" al que reasignarla.
        modelBuilder.Entity<SuscripcionPush>()
            .HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un mismo navegador/dispositivo (Endpoint) no debería quedar
        // duplicado, aunque el flujo normal ya evita crear una fila nueva
        // para uno que ya existe (ver NotificacionesController.Suscribir)
        // -- esto es la segunda red de seguridad a nivel de BD, mismo
        // criterio que Usuario.Email.
        modelBuilder.Entity<SuscripcionPush>()
            .HasIndex(s => s.Endpoint)
            .IsUnique();
    }
}
