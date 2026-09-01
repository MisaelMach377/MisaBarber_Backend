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

    public DbSet<Negocio> Negocios => Set<Negocio>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Barbero> Barberos => Set<Barbero>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<CitaServicio> CitaServicios => Set<CitaServicio>();
    public DbSet<CitaAuditoria> CitasAuditoria => Set<CitaAuditoria>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SuscripcionPush> SuscripcionesPush => Set<SuscripcionPush>();
    public DbSet<ChatMensaje> ChatMensajes => Set<ChatMensaje>();
    public DbSet<AuditoriaGeneral> AuditoriaGeneral => Set<AuditoriaGeneral>();
    public DbSet<HorarioNegocio> HorariosNegocio => Set<HorarioNegocio>();
    public DbSet<HorarioBarbero> HorariosBarbero => Set<HorarioBarbero>();
    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Slug único entre negocios -- así no puede haber dos barberías
        // apuntando a la misma URL pública. Postgres trata cada NULL como
        // distinto en un índice único, así que el negocio principal (Slug
        // = null, ver Models/Negocio.cs) no choca con nada.
        modelBuilder.Entity<Negocio>()
            .HasIndex(n => n.Slug)
            .IsUnique();

        // Default a nivel de columna (no solo en el modelo en memoria) --
        // así la migración que agrega ColorPrimario le puede poner este
        // valor a las filas que ya existen sin que yo tenga que editarla
        // a mano (mismo espíritu que el backfill manual que sí hizo falta
        // para NegocioId, pero acá EF ya sabe qué poner solo).
        modelBuilder.Entity<Negocio>()
            .Property(n => n.ColorPrimario)
            .HasDefaultValue("#2563eb");

        // Mismo motivo que ColorPrimario arriba: default a nivel de
        // columna para que la migración backfillee sola los negocios que
        // ya existen (todos en "Pro", con el combo básico de módulos para
        // su rol Barbero) sin que yo tenga que editarla a mano.
        modelBuilder.Entity<Negocio>()
            .Property(n => n.Plan)
            .HasDefaultValue("Pro");
        modelBuilder.Entity<Negocio>()
            .Property(n => n.ModulosBarbero)
            .HasDefaultValue("Citas,Clientes,Historial");

        // Precio con precisión fija — sin esto, Npgsql mapea decimal a
        // "numeric" sin precisión definida y tira un warning en cada
        // arranque (y en algunos motores puede truncar mal).
        modelBuilder.Entity<Servicio>()
            .Property(s => s.Precio)
            .HasPrecision(10, 2);

        // Restrict en NegocioId de cada tabla: no tiene sentido borrar un
        // Negocio que todavía tiene Clientes/Barberos/Servicios/Citas (se
        // suspende con Estado = "Inactivo" en su lugar, ver
        // Models/Negocio.cs) -- mismo criterio de "no borrar en cascada
        // datos que importan" que ya se usaba en las 3 FKs de Cita.
        modelBuilder.Entity<Cliente>()
            .HasOne(c => c.Negocio)
            .WithMany()
            .HasForeignKey(c => c.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Cliente>().HasIndex(c => c.NegocioId);

        modelBuilder.Entity<Barbero>()
            .HasOne(b => b.Negocio)
            .WithMany()
            .HasForeignKey(b => b.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Barbero>().HasIndex(b => b.NegocioId);

        modelBuilder.Entity<Servicio>()
            .HasOne(s => s.Negocio)
            .WithMany()
            .HasForeignKey(s => s.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Servicio>().HasIndex(s => s.NegocioId);

        modelBuilder.Entity<Cita>()
            .HasOne(c => c.Negocio)
            .WithMany()
            .HasForeignKey(c => c.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Cita>().HasIndex(c => c.NegocioId);

        modelBuilder.Entity<CitaAuditoria>()
            .HasOne(a => a.Negocio)
            .WithMany()
            .HasForeignKey(a => a.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CitaAuditoria>().HasIndex(a => a.NegocioId);

        modelBuilder.Entity<Usuario>()
            .HasOne(u => u.Negocio)
            .WithMany()
            .HasForeignKey(u => u.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Usuario>().HasIndex(u => u.NegocioId);

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

        // Usuario.Email es el usuario de login: único POR Negocio (no
        // global) -- dos barberías distintas alquilando el sistema pueden
        // cada una tener un usuario con el mismo correo sin chocar entre
        // sí. A nivel de BD como segunda red de seguridad además de la
        // validación en AuthController/UsuariosController (evita una
        // condición de carrera si dos requests de alta llegan casi al
        // mismo tiempo).
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => new { u.NegocioId, u.Email })
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

        // ChatMensaje: mismo criterio que CitaAuditoria -- FK Restrict a
        // Negocio (no se puede borrar un negocio con conversaciones), pero
        // sin FK a Usuario para ClienteId/AutorId (los nombres ya viajan
        // denormalizados en la fila, ver Models/ChatMensaje.cs). El índice
        // compuesto es el que pisa GetPropio/GetConversacion (todos los
        // mensajes de una conversación puntual, ordenados por fecha).
        modelBuilder.Entity<ChatMensaje>()
            .HasOne(m => m.Negocio)
            .WithMany()
            .HasForeignKey(m => m.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChatMensaje>()
            .HasIndex(m => new { m.NegocioId, m.ClienteId, m.FechaEnvio });

        // AuditoriaGeneral: mismo criterio que CitaAuditoria -- FK
        // Restrict a Negocio, sin FK a Usuario para AutorId (el nombre ya
        // viaja denormalizado, ver Models/AuditoriaGeneral.cs), e índices
        // para que filtrar por Negocio/fecha/tipo de entidad no haga table
        // scan cuando el historial crezca.
        modelBuilder.Entity<AuditoriaGeneral>()
            .HasOne(a => a.Negocio)
            .WithMany()
            .HasForeignKey(a => a.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AuditoriaGeneral>()
            .HasIndex(a => new { a.NegocioId, a.FechaHoraEvento });
        modelBuilder.Entity<AuditoriaGeneral>()
            .HasIndex(a => a.Entidad);

        // HorarioNegocio/HorarioBarbero: Cascade (a diferencia de las FKs
        // de arriba) porque acá SÍ corresponde -- son filas de config pura
        // sin valor histórico propio, dueñas por completo de su Negocio/
        // Barbero (a diferencia de Cliente/Servicio/Cita, que si se
        // borraran en cascada se perdería historial real de negocio).
        // Único (NegocioId, DiaSemana) / (BarberoId, DiaSemana): nunca dos
        // filas para el mismo día -- ver Utils/Horarios.cs, que siembra
        // exactamente una por día.
        modelBuilder.Entity<HorarioNegocio>()
            .HasOne(h => h.Negocio)
            .WithMany()
            .HasForeignKey(h => h.NegocioId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HorarioNegocio>()
            .HasIndex(h => new { h.NegocioId, h.DiaSemana })
            .IsUnique();

        modelBuilder.Entity<HorarioBarbero>()
            .HasOne(h => h.Barbero)
            .WithMany()
            .HasForeignKey(h => h.BarberoId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HorarioBarbero>()
            .HasIndex(h => new { h.BarberoId, h.DiaSemana })
            .IsUnique();

        // Producto: precisión fija en Precio (mismo motivo que Servicio,
        // ver más arriba) y Restrict en NegocioId -- mismo criterio que
        // Cliente/Barbero/Servicio: no se puede borrar un negocio que
        // todavía tiene productos en su catálogo.
        modelBuilder.Entity<Producto>()
            .Property(p => p.Precio)
            .HasPrecision(10, 2);
        modelBuilder.Entity<Producto>()
            .HasOne(p => p.Negocio)
            .WithMany()
            .HasForeignKey(p => p.NegocioId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Producto>().HasIndex(p => p.NegocioId);
    }
}
