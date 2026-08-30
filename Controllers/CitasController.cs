using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;
using misabarber.Services;

namespace misabarber.Controllers;

[ApiController]
[Route("api/citas")]
[RequiereAuth]
public class CitasController : ControllerBase
{
    private static readonly string[] EstadosValidos = { "Pendiente", "Confirmada", "Completada", "Cancelada" };

    // Horario de atención usado para calcular disponibilidad (GetDisponibilidad).
    // Fijo por ahora (no hay pantalla de configuración de horario todavía);
    // vive acá, en un solo lugar, para que el día que se haga configurable
    // sea un cambio de una sola línea.
    private const int HoraApertura = 9;
    private const int HoraCierre = 19;
    private const int PasoDisponibilidadMinutos = 15;

    private readonly MisaBarberContext _db;
    private readonly PushNotificationService _notificaciones;

    public CitasController(MisaBarberContext db, PushNotificationService notificaciones)
    {
        _db = db;
        _notificaciones = notificaciones;
    }

    // A qué barbería pertenece el usuario logueado (multi-tenant, ver
    // Models/Negocio.cs) -- TODA query de este controller filtra por acá,
    // nunca por un negocioId que pudiera mandar el front.
    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    // Historial/Reportes (Reportes.jsx reusa este mismo endpoint, ver su
    // comentario en GetHistorial) es un módulo de Plan Pro (ver
    // Utils/Modulos.cs) -- pero SOLO para el panel de staff. Un Cliente
    // consultando SUS PROPIAS citas pasadas en MiCuenta.jsx no tiene nada
    // que ver con el Plan de la barbería, así que nunca se lo bloquea acá
    // (ver el chequeo de Rol antes de llamar a esto en cada endpoint).
    private async Task<bool> PlanPermiteHistorial()
    {
        var plan = await _db.Negocios.Where(n => n.Id == NegocioId).Select(n => n.Plan).FirstOrDefaultAsync();
        return Modulos.DeNegocio(plan).Contains("Historial");
    }

    // Una cita ahora puede llevar varios servicios (ej. "Corte" + "Barba"),
    // así que el total de precio/duración se calcula sumando los de la
    // lista en vez de leerlo de un solo campo -- ver Models/Cita.cs y
    // Models/CitaServicio.cs.
    private static CitaDto ToDto(Cita c)
    {
        var servicios = c.CitaServicios
            .Select(cs => new CitaServicioDto(
                cs.ServicioId,
                cs.Servicio?.Nombre ?? "",
                cs.Servicio?.DuracionMinutos ?? 0,
                cs.Servicio?.Precio ?? 0))
            .ToList();

        return new CitaDto(
            c.Id,
            c.ClienteId,
            c.Cliente?.Nombre ?? "",
            c.Cliente?.FotoUrl,
            c.BarberoId,
            c.Barbero?.Nombre ?? "",
            c.Barbero?.FotoUrl,
            servicios,
            string.Join(", ", servicios.Select(s => s.ServicioNombre)),
            servicios.Sum(s => s.ServicioDuracionMinutos),
            servicios.Sum(s => s.ServicioPrecio),
            c.FechaHora,
            c.Estado,
            c.Notas,
            c.FechaCreacion
        );
    }

    private static CitaAuditoriaDto ToAuditoriaDto(CitaAuditoria a) => new(
        a.Id,
        a.CitaId,
        a.Accion,
        a.Detalle,
        a.ClienteNombre,
        a.BarberoNombre,
        a.ServicioNombre,
        a.FechaHoraCita,
        a.FechaHoraEvento
    );

    private IQueryable<Cita> ConDatos() =>
        _db.Citas
            .Where(c => c.NegocioId == NegocioId)
            .Include(c => c.Cliente)
            .Include(c => c.Barbero)
            .Include(c => c.CitaServicios).ThenInclude(cs => cs.Servicio);

    // Deja una fila en CitasAuditoria con una "foto" de la cita en ese
    // momento (nombres, no solo Ids — ver Models/CitaAuditoria.cs). Cuando
    // hay más de un servicio, ServicioNombre guarda los nombres unidos con
    // ", " (ej. "Corte, Barba") -- sigue siendo un solo campo de texto para
    // no tener que migrar también la tabla de auditoría, que es de solo
    // lectura histórica. No hace SaveChanges: se guarda junto con el
    // cambio principal en un solo SaveChangesAsync, para que ambos queden
    // en la misma transacción.
    private void RegistrarAuditoria(Cita cita, string accion, string? detalle = null)
    {
        _db.CitasAuditoria.Add(new CitaAuditoria
        {
            NegocioId = NegocioId,
            CitaId = cita.Id,
            Accion = accion,
            Detalle = detalle,
            ClienteNombre = cita.Cliente?.Nombre ?? "",
            BarberoNombre = cita.Barbero?.Nombre ?? "",
            ServicioNombre = string.Join(", ", cita.CitaServicios.Select(cs => cs.Servicio?.Nombre ?? "")),
            FechaHoraCita = cita.FechaHora,
        });
    }

    // Duración total de una cita = suma de la duración de cada servicio
    // que lleva, con un piso de 30 min si por algún motivo viniera en 0
    // (mismo piso que ya usaba el código viejo con un solo servicio, para
    // que nunca se trate como una cita de duración cero al chequear choques
    // de horario).
    private static int DuracionTotal(Cita c)
    {
        var suma = c.CitaServicios.Sum(cs => cs.Servicio?.DuracionMinutos ?? 0);
        return suma > 0 ? suma : 30;
    }

    // GET api/citas?fecha=2026-08-28 -> citas de ESE día, ordenadas por hora.
    // Sin fecha, devuelve las de hoy (el listado completo sin filtro no
    // tiene mucho sentido para un negocio que agenda día a día — para eso
    // está GET api/citas/historial).
    [HttpGet]
    public async Task<ActionResult<List<CitaDto>>> GetAll([FromQuery] DateTime? fecha)
    {
        var dia = (fecha ?? DateTime.Today).Date;
        var siguienteDia = dia.AddDays(1);

        var query = ConDatos().Where(c => c.FechaHora >= dia && c.FechaHora < siguienteDia);

        // Un Barbero solo ve sus propias citas — filtrado del lado del
        // servidor con el BarberoId que viene en SU token, nunca confiando
        // en un query param que el propio front pudiera mandar mal armado.
        // Admin ve todas (de SU negocio -- ver ConDatos()).
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol == "Barbero")
            query = query.Where(c => c.BarberoId == usuario.BarberoId);
        else if (usuario.Rol == "Cliente")
            query = query.Where(c => c.ClienteId == usuario.ClienteId);

        var lista = await query.OrderBy(c => c.FechaHora).ToListAsync();

        return Ok(lista.Select(ToDto));
    }

    // GET api/citas/historial?desde=&hasta=&clienteId=&barberoId=&estado=
    // Historial de reservas sin límite a "hoy" — para revisar qué pasó con
    // un cliente/barbero en el tiempo, o auditar un rango de fechas. Todos
    // los filtros son opcionales y se combinan con AND. Tope de 500 filas:
    // esto es para revisión humana en pantalla, no un export masivo.
    [HttpGet("historial")]
    public async Task<ActionResult<List<CitaDto>>> GetHistorial(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] Guid? clienteId,
        [FromQuery] Guid? barberoId,
        [FromQuery] string? estado)
    {
        var usuario = HttpContext.UsuarioActual()!;

        // El Plan de la barbería solo restringe al STAFF (Admin/Barbero
        // viendo el módulo Historial/Reportes del panel) -- un Cliente
        // consultando sus propias citas en MiCuenta.jsx nunca se bloquea
        // acá, ver el comentario de PlanPermiteHistorial más arriba.
        if (usuario.Rol != "Cliente" && !await PlanPermiteHistorial())
            return Forbid();

        var query = ConDatos();

        // Mismo criterio que en GetAll: un Barbero solo puede ver su
        // propio historial, sin importar qué barberoId venga en la query.
        var barberoIdEfectivo = usuario.Rol == "Barbero" ? usuario.BarberoId : barberoId;
        var clienteIdEfectivo = usuario.Rol == "Cliente" ? usuario.ClienteId : clienteId;

        if (desde.HasValue)
            query = query.Where(c => c.FechaHora >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(c => c.FechaHora < hasta.Value.Date.AddDays(1));
        if (clienteIdEfectivo.HasValue)
            query = query.Where(c => c.ClienteId == clienteIdEfectivo.Value);
        if (barberoIdEfectivo.HasValue)
            query = query.Where(c => c.BarberoId == barberoIdEfectivo.Value);
        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(c => c.Estado == estado);

        var lista = await query
            .OrderByDescending(c => c.FechaHora)
            .Take(500)
            .ToListAsync();

        return Ok(lista.Select(ToDto));
    }

    // GET api/citas/auditoria?desde=&hasta=&citaId=
    // Log de eventos (creada / editada / cambio de estado / eliminada),
    // más reciente primero. Independiente de si la cita todavía existe.
    [HttpGet("auditoria")]
    public async Task<ActionResult<List<CitaAuditoriaDto>>> GetAuditoria(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] Guid? citaId)
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();
        if (!await PlanPermiteHistorial()) return Forbid();

        var query = _db.CitasAuditoria.Where(a => a.NegocioId == NegocioId);

        if (desde.HasValue)
            query = query.Where(a => a.FechaHoraEvento >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(a => a.FechaHoraEvento < hasta.Value.Date.AddDays(1));
        if (citaId.HasValue)
            query = query.Where(a => a.CitaId == citaId.Value);

        var lista = await query
            .OrderByDescending(a => a.FechaHoraEvento)
            .Take(500)
            .ToListAsync();

        return Ok(lista.Select(ToAuditoriaDto));
    }

    // GET api/citas/disponibilidad?barberoId=&servicioIds=&servicioIds=&fecha=2026-08-28&excluirCitaId=
    // Devuelve los horarios de inicio ("HH:mm") libres ese día para ese
    // barbero + la lista de servicios elegida (la duración a reservar es
    // la SUMA de todos), ya descontando los que chocan con citas
    // existentes — así el front le muestra al cliente una lista de horas
    // reales para elegir, en vez de un campo de fecha/hora en blanco donde
    // recién se entera del choque al guardar. excluirCitaId es para cuando
    // se está EDITANDO una cita: que no choque contra sí misma.
    [HttpGet("disponibilidad")]
    public async Task<ActionResult<List<string>>> GetDisponibilidad(
        [FromQuery] Guid barberoId,
        [FromQuery] List<Guid> servicioIds,
        [FromQuery] DateTime fecha,
        [FromQuery] Guid? excluirCitaId)
    {
        if (servicioIds is null || servicioIds.Count == 0)
            return BadRequest("Elige al menos un servicio.");

        var idsUnicos = servicioIds.Distinct().ToList();
        var servicios = await _db.Servicios.Where(s => idsUnicos.Contains(s.Id) && s.NegocioId == NegocioId).ToListAsync();
        if (servicios.Count != idsUnicos.Count)
            return BadRequest("Uno o más servicios no existen.");

        var barberoExiste = await _db.Barberos.AnyAsync(b => b.Id == barberoId && b.NegocioId == NegocioId);
        if (!barberoExiste) return BadRequest("El barbero no existe.");

        var dia = fecha.Date;
        var siguienteDia = dia.AddDays(1);

        // Trae las citas activas de ese barbero ese día UNA sola vez, y
        // evalúa los huecos en memoria — más barato que una query por cada
        // slot candidato (que sería 1 query cada 15 min entre las 9 y las 19).
        var ocupados = await ConDatos()
            .Where(c => c.BarberoId == barberoId
                && c.Estado != "Cancelada"
                && c.FechaHora >= dia && c.FechaHora < siguienteDia
                && (excluirCitaId == null || c.Id != excluirCitaId))
            .ToListAsync();

        var duracion = Math.Max(servicios.Sum(s => s.DuracionMinutos), 5);
        var apertura = dia.AddHours(HoraApertura);
        var cierre = dia.AddHours(HoraCierre);
        var ahora = DateTime.Now;

        var disponibles = new List<string>();
        for (var inicio = apertura; inicio.AddMinutes(duracion) <= cierre; inicio = inicio.AddMinutes(PasoDisponibilidadMinutos))
        {
            // No ofrecer horas que ya pasaron si se está consultando el día de hoy.
            if (dia == DateTime.Today && inicio <= ahora)
                continue;

            var fin = inicio.AddMinutes(duracion);
            var choca = ocupados.Any(o => inicio < o.FechaHora.AddMinutes(DuracionTotal(o)) && o.FechaHora < fin);
            if (!choca)
                disponibles.Add(inicio.ToString("HH:mm"));
        }

        return Ok(disponibles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CitaDto>> GetById(Guid id)
    {
        var c = await ConDatos().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol == "Cliente" && c.ClienteId != usuario.ClienteId) return Forbid();
        if (usuario.Rol == "Barbero" && c.BarberoId != usuario.BarberoId) return Forbid();

        return Ok(ToDto(c));
    }

    // Calcula si [inicio, fin) se pisa con alguna cita activa (no cancelada)
    // que YA tiene ese barbero ese mismo día. excluirId se usa en Update
    // para no chocar contra sí misma.
    private async Task<bool> HayConflictoDeHorario(Guid barberoId, DateTime inicio, DateTime fin, Guid? excluirId)
    {
        var dia = inicio.Date;
        var siguienteDia = dia.AddDays(1);

        var citasDelDia = await ConDatos()
            .Where(c => c.BarberoId == barberoId
                && c.Estado != "Cancelada"
                && c.FechaHora >= dia && c.FechaHora < siguienteDia
                && (excluirId == null || c.Id != excluirId))
            .ToListAsync();

        return citasDelDia.Any(c =>
        {
            var citaFin = c.FechaHora.AddMinutes(DuracionTotal(c));
            // Dos rangos [a,b) y [c,d) se pisan si a < d y c < b.
            return inicio < citaFin && c.FechaHora < fin;
        });
    }

    // Valida la lista de servicios que llega en el DTO (no vacía, todos
    // existen DENTRO del negocio del usuario) y devuelve las entidades ya
    // cargadas -- Create y Update repiten exactamente esta validación, así
    // que vive en un solo lugar.
    private async Task<(List<Servicio>? servicios, ActionResult? error)> ValidarServicios(List<Guid>? servicioIds)
    {
        if (servicioIds is null || servicioIds.Count == 0)
            return (null, BadRequest("Elige al menos un servicio."));

        var idsUnicos = servicioIds.Distinct().ToList();
        var servicios = await _db.Servicios.Where(s => idsUnicos.Contains(s.Id) && s.NegocioId == NegocioId).ToListAsync();
        if (servicios.Count != idsUnicos.Count)
            return (null, BadRequest("Uno o más servicios no existen."));

        return (servicios, null);
    }

    [HttpPost]
    public async Task<ActionResult<CitaDto>> Create(CitaCreateDto dto)
    {
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol == "Cliente" && usuario.ClienteId is null) return Unauthorized();
        var clienteId = usuario.Rol == "Cliente" ? usuario.ClienteId!.Value : dto.ClienteId;

        var cliente = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == clienteId && x.NegocioId == NegocioId);
        if (cliente is null) return BadRequest("El cliente no existe.");

        var barbero = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == dto.BarberoId && x.NegocioId == NegocioId);
        if (barbero is null) return BadRequest("El barbero no existe.");

        var (servicios, errorServicios) = await ValidarServicios(dto.ServicioIds);
        if (errorServicios is not null) return errorServicios;

        var inicio = dto.FechaHora;
        var fin = inicio.AddMinutes(servicios!.Sum(s => s.DuracionMinutos));

        if (await HayConflictoDeHorario(dto.BarberoId, inicio, fin, excluirId: null))
            return BadRequest($"{barbero.Nombre} ya tiene otra cita agendada en ese horario. Elige otra hora o otro barbero.");

        var cita = new Cita
        {
            NegocioId = NegocioId,
            ClienteId = clienteId,
            BarberoId = dto.BarberoId,
            FechaHora = dto.FechaHora,
            Notas = dto.Notas,
            CitaServicios = servicios.Select(s => new CitaServicio { ServicioId = s.Id, Servicio = s }).ToList(),
        };

        _db.Citas.Add(cita);

        cita.Cliente = cliente;
        cita.Barbero = barbero;
        RegistrarAuditoria(cita, "Creada", $"Agendada para {inicio:dd/MM/yyyy HH:mm}");

        await _db.SaveChangesAsync();

        await _notificaciones.NotificarNuevaCita(cita, usuario.Id);

        return CreatedAtAction(nameof(GetById), new { id = cita.Id }, ToDto(cita));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CitaDto>> Update(Guid id, CitaCreateDto dto)
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();

        var cita = await ConDatos().FirstOrDefaultAsync(x => x.Id == id);
        if (cita is null) return NotFound();

        var cliente = await _db.Clientes.FirstOrDefaultAsync(x => x.Id == dto.ClienteId && x.NegocioId == NegocioId);
        if (cliente is null) return BadRequest("El cliente no existe.");

        var barbero = await _db.Barberos.FirstOrDefaultAsync(x => x.Id == dto.BarberoId && x.NegocioId == NegocioId);
        if (barbero is null) return BadRequest("El barbero no existe.");

        var (servicios, errorServicios) = await ValidarServicios(dto.ServicioIds);
        if (errorServicios is not null) return errorServicios;

        var inicio = dto.FechaHora;
        var fin = inicio.AddMinutes(servicios!.Sum(s => s.DuracionMinutos));

        if (await HayConflictoDeHorario(dto.BarberoId, inicio, fin, excluirId: id))
            return BadRequest($"{barbero.Nombre} ya tiene otra cita agendada en ese horario. Elige otra hora o otro barbero.");

        var detalle = $"{cita.FechaHora:dd/MM/yyyy HH:mm} con {cita.Barbero?.Nombre} -> {inicio:dd/MM/yyyy HH:mm} con {barbero.Nombre}";

        cita.ClienteId = dto.ClienteId;
        cita.BarberoId = dto.BarberoId;
        cita.FechaHora = dto.FechaHora;
        cita.Notas = dto.Notas;

        // Se reemplaza la lista completa de servicios en vez de calcular un
        // diff (agregar/quitar solo lo que cambió) -- son como mucho un
        // puñado de servicios por cita, no vale la pena la complejidad
        // extra. Como la relación es requerida (CitaId no es nullable en
        // CitaServicio), EF borra solas las filas que salen de la
        // colección al hacer SaveChanges, en vez de dejarlas huérfanas.
        cita.CitaServicios.Clear();
        foreach (var s in servicios!)
            cita.CitaServicios.Add(new CitaServicio { ServicioId = s.Id, Servicio = s });

        cita.Cliente = cliente;
        cita.Barbero = barbero;
        RegistrarAuditoria(cita, "Editada", detalle);

        await _db.SaveChangesAsync();

        return Ok(ToDto(cita));
    }

    [HttpPut("{id}/estado")]
    public async Task<ActionResult<CitaDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var cita = await ConDatos().FirstOrDefaultAsync(x => x.Id == id);
        if (cita is null) return NotFound();

        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol == "Cliente")
        {
            if (cita.ClienteId != usuario.ClienteId) return Forbid();
            if (dto.Estado != "Cancelada") return Forbid();
        }

        var estadoAnterior = cita.Estado;
        cita.Estado = dto.Estado;
        RegistrarAuditoria(cita, $"Estado: {estadoAnterior} -> {dto.Estado}");

        await _db.SaveChangesAsync();

        await _notificaciones.NotificarCambioEstado(cita, dto.Estado, usuario.Id, usuario.Rol);

        return Ok(ToDto(cita));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();

        var cita = await ConDatos().FirstOrDefaultAsync(x => x.Id == id);
        if (cita is null) return NotFound();

        // Solo se puede borrar de verdad si todavía no pasó nada con ella —
        // una cita Confirmada o Completada es historial real, se cancela en
        // vez de borrarse (mismo criterio de "Inactivo en vez de borrar" que
        // el resto del sistema). La auditoría igual queda: aunque se borre
        // la fila de Cita (y en cascada sus filas de CitaServicio -- ver
        // MisaBarberContext.OnModelCreating), el evento "Eliminada" con la
        // foto de sus datos se conserva en CitasAuditoria (por eso ahí no
        // hay FK real).
        if (cita.Estado is "Confirmada" or "Completada")
            return BadRequest("No puedes eliminar una cita confirmada o completada. Puedes cancelarla en su lugar.");

        RegistrarAuditoria(cita, "Eliminada", $"Cita que estaba agendada para {cita.FechaHora:dd/MM/yyyy HH:mm}");
        _db.Citas.Remove(cita);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
