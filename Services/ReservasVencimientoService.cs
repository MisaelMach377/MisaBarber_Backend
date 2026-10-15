using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.Utils;

namespace misabarber.Services;

// Si un cliente reserva y nunca pasa, esas unidades quedarían apartadas para
// siempre y el producto figuraría "agotado" sin estarlo. Este servicio revisa
// cada tanto las reservas Pendientes cuya FechaVence ya pasó y las marca
// Vencida, devolviendo las unidades al stock disponible.
//
// Mismo patrón que RecordatorioCitasService: BackgroundService singleton que
// abre un scope nuevo por vuelta (el DbContext es Scoped) y que no se cae por
// un tick fallido.
public class ReservasVencimientoService : BackgroundService
{
    // 10 min alcanza: una reserva vencida libera su stock como mucho 10 min
    // tarde, y la consulta es liviana (índice por Negocio/Estado/FechaVence).
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservasVencimientoService> _logger;

    public ReservasVencimientoService(IServiceScopeFactory scopeFactory, ILogger<ReservasVencimientoService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await VencerPendientes();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error venciendo reservas de productos");
            }

            try
            {
                await AvisarPorVencer();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error avisando reservas por vencer");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // La app se está apagando: salir del while sin loguear.
            }
        }
    }

    // El día que vence una reserva (a partir de las 9:00 hora de Perú -- no de
    // madrugada), le llega un push al cliente: "pasa hoy a recoger". El flag
    // AvisoVencimientoEnviado se marca con un UPDATE condicional ANTES de
    // enviar: si hay dos instancias del backend corriendo, solo una gana la
    // fila y el cliente no recibe el aviso duplicado.
    private async Task AvisarPorVencer()
    {
        var ahoraPeru = Reloj.AhoraPeru();
        if (ahoraPeru.Hour < 9) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MisaBarberContext>();
        var push = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

        // "Hoy" en hora de Perú = desde las 05:00 UTC de hoy hasta las 05:00 UTC
        // de mañana. FechaVence (23:59:59 Perú) cae siempre dentro de ese tramo.
        var desdeUtc = ahoraPeru.Date.AddHours(5);
        var hastaUtc = desdeUtc.AddDays(1);

        var porVencer = await db.ReservasProductos.AsNoTracking()
            .Include(r => r.Producto)
            .Where(r => r.Estado == "Pendiente" && !r.AvisoVencimientoEnviado
                        && r.FechaVence > desdeUtc && r.FechaVence <= hastaUtc)
            .Take(100)
            .ToListAsync();

        foreach (var r in porVencer)
        {
            var filas = await db.ReservasProductos
                .Where(x => x.Id == r.Id && !x.AvisoVencimientoEnviado)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvisoVencimientoEnviado, true));
            if (filas == 0) continue;

            try
            {
                await push.NotificarReservaPorVencer(r, r.Producto?.Nombre ?? "tu producto");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo avisar la reserva por vencer {ReservaId}", r.Id);
            }
        }
    }

    private async Task VencerPendientes()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MisaBarberContext>();

        var ahora = DateTime.UtcNow;
        var ids = await db.ReservasProductos.AsNoTracking()
            .Where(r => r.Estado == "Pendiente" && r.FechaVence <= ahora)
            .Select(r => r.Id)
            .Take(200)
            .ToListAsync();

        // Una por una (cada una en su propia transacción dentro de Liberar): si
        // una falla, las demás igual se procesan, y el UPDATE condicional hace
        // que sea inofensivo si el cliente la canceló justo en este instante.
        foreach (var id in ids)
        {
            try
            {
                await ReservasStock.Liberar(db, id, "Vencida", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo vencer la reserva {ReservaId}", id);
            }
        }
    }
}
