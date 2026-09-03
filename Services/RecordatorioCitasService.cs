using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.Utils;

namespace misabarber.Services;

// Recorre periódicamente las citas por venir y le manda un push al staff
// ("tu próxima cita está por empezar") un rato antes de que arranquen --
// esto es lo que de verdad resuelve el pedido de "un calendario de
// recordatorio para que no se les olvide": un barbero cortando pelo no
// está mirando el Panel a cada rato, pero SÍ ve la notificación que le
// llega al celular/navegador (reusa la infraestructura de Web Push que ya
// existía para avisos de citas nuevas/canceladas, ver PushNotificationService).
//
// Es un BackgroundService (se registra como AddHostedService en Program.cs,
// corre en un loop propio durante toda la vida de la app) y no un
// controller porque nadie lo dispara desde afuera -- se despierta solo.
public class RecordatorioCitasService : BackgroundService
{
    // Cuánto antes de la hora de la cita se manda el aviso. 30 min le da
    // margen real al barbero/Admin para prepararse sin ser tan temprano
    // que ya se les olvidó de nuevo para cuando de verdad toca.
    private static readonly TimeSpan Antelacion = TimeSpan.FromMinutes(30);

    // Cada cuánto se revisa si hay recordatorios pendientes. 5 min alcanza
    // de sobra: como máximo un recordatorio sale 5 min más tarde de los
    // 30 min "ideales", sin generar carga real sobre la tabla de Citas
    // (es una sola query liviana, filtrada por fecha/estado/flag).
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    // BackgroundService es un singleton que vive toda la app -- no puede
    // recibir MisaBarberContext/PushNotificationService directo en el
    // constructor (son Scoped, ver Program.cs) porque ninguno está
    // pensado para vivir tanto tiempo ni ser usado desde varios ticks
    // seguidos sin cerrarse. IServiceScopeFactory es lo que permite abrir
    // un scope nuevo (y por lo tanto un DbContext nuevo) en cada vuelta,
    // igual que hace Program.cs al sembrar el Admin inicial al arrancar.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordatorioCitasService> _logger;

    public RecordatorioCitasService(IServiceScopeFactory scopeFactory, ILogger<RecordatorioCitasService> logger)
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
                await EnviarPendientes(stoppingToken);
            }
            catch (Exception ex)
            {
                // Un tick que falla (ej. la BD momentáneamente inalcanzable
                // en un redeploy) no debe tumbar el servicio para siempre
                // -- se registra el error y se reintenta en la próxima
                // vuelta, 5 min después.
                _logger.LogError(ex, "Error revisando recordatorios de citas pendientes");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // La app se está apagando (stoppingToken canceló el
                // Delay) -- no es un error, simplemente hay que salir del
                // while sin loguear nada.
            }
        }
    }

    private async Task EnviarPendientes(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MisaBarberContext>();
        var notificaciones = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

        // Reloj.AhoraPeru() (no DateTime.Now/UtcNow directo) por el mismo
        // motivo que CitasController.GetDisponibilidad -- FechaHora de
        // una Cita es hora de pared de Perú sin zona horaria (ver
        // Data/DateTimeConverters.cs), así que "ahora" tiene que calcularse
        // igual, sin importar que el contenedor de Railway corra en UTC.
        var ahora = Reloj.AhoraPeru();
        var limite = ahora + Antelacion;

        // Solo citas que TODAVÍA no arrancaron (FechaHora > ahora): si el
        // servicio estuvo caído más de 30 min y se "saltó" la ventana de
        // una cita, no tiene sentido mandar un recordatorio de algo que ya
        // debería estar pasando o ya pasó -- eso confundiría más de lo
        // que ayuda.
        var pendientes = await db.Citas
            .Where(c => (c.Estado == "Pendiente" || c.Estado == "Confirmada")
                && !c.RecordatorioEnviado
                && c.FechaHora > ahora
                && c.FechaHora <= limite)
            .Include(c => c.Cliente)
            .Include(c => c.Barbero)
            .ToListAsync(ct);

        if (pendientes.Count == 0) return;

        foreach (var cita in pendientes)
        {
            await notificaciones.NotificarRecordatorio(cita);
            cita.RecordatorioEnviado = true;
        }

        // Un solo SaveChanges para todo el lote -- si algo fallara a mitad
        // de los pushes (ver el try/catch por-suscripción dentro de
        // EnviarATodos, que nunca deja que un push roto tumbe el resto),
        // igual se guardan los flags de las que sí se alcanzaron a avisar.
        await db.SaveChangesAsync(ct);
    }
}
