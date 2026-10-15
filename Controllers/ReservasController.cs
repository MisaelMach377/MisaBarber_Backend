using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Services;
using misabarber.Utils;

namespace misabarber.Controllers;

// Reservas de productos. Quién puede qué:
//  - Cliente: reservar, ver y cancelar SUS reservas (siempre desde el token).
//  - Admin: ver todas, reservar a nombre de un cliente, entregar, cancelar.
// Un Barbero no toca esto (el stock es del dueño): ni lista ni resuelve.
[ApiController]
[Route("api/reservas")]
[RequiereAuth]
public class ReservasController : ControllerBase
{
    // Reglas de negocio fijas por ahora (si algún día un negocio quiere
    // distintas, pasan a columnas de Negocio como se hizo con ModulosBarbero).
    private const int DiasHabilesVigencia = 3;
    private const int MaxActivasPorCliente = 3;
    private const int MaxUnidadesPorReserva = 5;
    private const int NotaMaxLength = 300;

    private static readonly string[] EstadosValidos = { "Pendiente", "Entregada", "Cancelada", "Vencida" };

    private readonly MisaBarberContext _db;
    private readonly PushNotificationService _push;

    public ReservasController(MisaBarberContext db, PushNotificationService push)
    {
        _db = db;
        _push = push;
    }

    // Vigencia en DÍAS HÁBILES (lunes a viernes), no en horas corridas: una
    // reserva del viernes en la tarde no puede vencer el domingo, cuando
    // nadie puede venir a recogerla. Se cuenta desde el día siguiente y vence
    // al final (23:59:59) del tercer día hábil, hora de Perú -- así el
    // cliente tiene el día completo para pasar, no hasta "la misma hora de
    // hace 3 días". FechaVence se guarda en UTC como el resto de las fechas
    // de esta tabla (Perú es UTC-5 fijo, ver Utils/Reloj.cs). Los feriados
    // nacionales (Utils/Feriados.cs) tampoco cuentan como día hábil.
    private static DateTime CalcularVencimientoUtc(DateTime ahoraPeru)
    {
        var dia = ahoraPeru.Date;
        var contados = 0;
        while (contados < DiasHabilesVigencia)
        {
            dia = dia.AddDays(1);
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Feriados.EsFeriado(dia))
                contados++;
        }
        return dia.AddDays(1).AddSeconds(-1).AddHours(5);
    }

    private UsuarioClaims Actor => HttpContext.UsuarioActual()!;
    private Guid NegocioId => Actor.NegocioId;
    private bool EsAdmin => Actor.Rol is "Admin" or "SuperAdmin";

    private static ReservaDto ToDto(ReservaProducto r) => new(
        r.Id, r.ProductoId, r.Producto?.Nombre ?? "", r.Producto?.Marca, r.Producto?.FotoUrl,
        r.ClienteId, r.Cliente?.Nombre ?? "", r.Cliente?.Telefono,
        r.Cantidad, r.PrecioUnitario, r.PrecioUnitario * r.Cantidad,
        r.Estado, r.FechaCreacion, r.FechaVence, r.FechaResolucion, r.ResueltoPorNombre, r.Nota, r.CreadaPorAdmin);

    private IQueryable<ReservaProducto> Consulta() =>
        _db.ReservasProductos.AsNoTracking()
            .Include(r => r.Producto)
            .Include(r => r.Cliente)
            .Where(r => r.NegocioId == NegocioId);

    // Reservar. El truco de la concurrencia está en el UPDATE condicional:
    // "StockReservado += n WHERE Stock - StockReservado >= n". Si dos clientes
    // quieren la última unidad a la vez, la base serializa los UPDATE y solo
    // uno cumple la condición; el otro ve 0 filas y recibe "no hay unidades".
    // Leer el stock, comparar en C# y recién guardar dejaría pasar a los dos.
    [HttpPost]
    public async Task<ActionResult<ReservaDto>> Crear(ReservaCreateDto dto)
    {
        if (Actor.Rol is not ("Cliente" or "Admin" or "SuperAdmin"))
            return StatusCode(403, "No tienes permiso para hacer esto.");

        if (dto.Cantidad < 1 || dto.Cantidad > MaxUnidadesPorReserva)
            return BadRequest($"Puedes reservar entre 1 y {MaxUnidadesPorReserva} unidades por reserva.");
        if (dto.Nota is { Length: > NotaMaxLength })
            return BadRequest($"La nota no puede pasar de {NotaMaxLength} caracteres.");

        // El cliente sale del token; solo un Admin puede indicar otro.
        Guid clienteId;
        if (EsAdmin)
        {
            if (dto.ClienteId is null) return BadRequest("Indica a nombre de qué cliente es la reserva.");
            clienteId = dto.ClienteId.Value;
        }
        else
        {
            if (Actor.ClienteId is null) return StatusCode(403, "Tu cuenta no está asociada a un cliente.");
            clienteId = Actor.ClienteId.Value;
        }

        var cliente = await _db.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clienteId && c.NegocioId == NegocioId);
        if (cliente is null) return BadRequest("El cliente no existe.");

        var producto = await _db.Productos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.NegocioId == NegocioId && p.Estado == "Activo");
        if (producto is null) return BadRequest("El producto no está disponible.");

        var activas = await _db.ReservasProductos
            .CountAsync(r => r.ClienteId == clienteId && r.NegocioId == NegocioId && r.Estado == "Pendiente");
        if (activas >= MaxActivasPorCliente)
            return BadRequest($"Ya tienes {MaxActivasPorCliente} reservas activas. Espera a recogerlas o cancela alguna.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var cantidad = dto.Cantidad;
        var filas = await _db.Productos
            .Where(p => p.Id == producto.Id && p.NegocioId == NegocioId && p.Estado == "Activo"
                        && p.Stock - p.StockReservado >= cantidad)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockReservado, p => p.StockReservado + cantidad));
        if (filas == 0)
            return BadRequest("No quedan suficientes unidades disponibles de este producto.");

        var ahora = DateTime.UtcNow;
        var reserva = new ReservaProducto
        {
            NegocioId = NegocioId,
            ProductoId = producto.Id,
            ClienteId = clienteId,
            Cantidad = cantidad,
            PrecioUnitario = producto.Precio,
            Estado = "Pendiente",
            FechaCreacion = ahora,
            FechaVence = CalcularVencimientoUtc(Reloj.AhoraPeru()),
            Nota = string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim(),
            CreadaPorAdmin = EsAdmin,
        };
        _db.ReservasProductos.Add(reserva);
        Auditoria.Registrar(_db, Actor, "Reserva", reserva.Id, producto.Nombre,
            "Creada", $"{cantidad} × {producto.Nombre} para {cliente.Nombre}");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Fuera de la transacción: un push que falla nunca debe deshacer la reserva.
        await _push.NotificarNuevaReserva(reserva, cliente.Nombre, producto.Nombre, Actor.Id);

        var creada = await Consulta().FirstAsync(r => r.Id == reserva.Id);
        return Ok(ToDto(creada));
    }

    // Reservas del cliente logueado (activas primero, luego las más recientes).
    [HttpGet("mias")]
    public async Task<ActionResult<List<ReservaDto>>> Mias()
    {
        if (Actor.ClienteId is null) return Ok(new List<ReservaDto>());
        var clienteId = Actor.ClienteId.Value;

        var lista = await Consulta()
            .Where(r => r.ClienteId == clienteId)
            .OrderBy(r => r.Estado == "Pendiente" ? 0 : 1)
            .ThenByDescending(r => r.FechaCreacion)
            .Take(30)
            .ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    // Panel del Admin. Sin filtro = todas; ?estado=Pendiente para la bandeja.
    [HttpGet]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<List<ReservaDto>>> GetAll([FromQuery] string? estado, [FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 300);
        var q = Consulta();
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!EstadosValidos.Contains(estado)) return BadRequest("Estado no válido.");
            q = q.Where(r => r.Estado == estado);
        }

        var lista = await q
            .OrderBy(r => r.Estado == "Pendiente" ? 0 : 1)
            .ThenByDescending(r => r.FechaCreacion)
            .Take(take)
            .ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    // Cuántas reservas esperan a ser recogidas (para el badge del menú).
    [HttpGet("pendientes/cantidad")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<int>> CantidadPendientes() =>
        Ok(await _db.ReservasProductos.CountAsync(r => r.NegocioId == NegocioId && r.Estado == "Pendiente"));

    [HttpPut("{id}/entregar")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<ReservaDto>> Entregar(Guid id)
    {
        var existe = await _db.ReservasProductos.AnyAsync(r => r.Id == id && r.NegocioId == NegocioId);
        if (!existe) return NotFound();

        var ok = await ReservasStock.Entregar(_db, id, Actor.Id, Actor.Nombre);
        if (!ok) return Conflict("Esta reserva ya no está pendiente (la cancelaron o venció).");

        var r = await Consulta().FirstAsync(x => x.Id == id);
        Auditoria.Registrar(_db, Actor, "Reserva", r.Id, r.Producto?.Nombre ?? "", "Entregada", $"{r.Cantidad} × {r.Producto?.Nombre} a {r.Cliente?.Nombre}");
        await _db.SaveChangesAsync();
        await _push.NotificarReservaResuelta(r, r.Producto?.Nombre ?? "", "Entregada", Actor.Id);
        return Ok(ToDto(r));
    }

    // Cancelar: el cliente la suya, el Admin cualquiera del negocio.
    [HttpPut("{id}/cancelar")]
    public async Task<ActionResult<ReservaDto>> Cancelar(Guid id)
    {
        var reserva = await _db.ReservasProductos.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.NegocioId == NegocioId);
        if (reserva is null) return NotFound();

        if (!EsAdmin && (Actor.Rol != "Cliente" || reserva.ClienteId != Actor.ClienteId))
            return NotFound(); // 404 y no 403: no se confirma que la reserva de otro existe.

        var ok = await ReservasStock.Liberar(_db, id, "Cancelada", Actor.Nombre);
        if (!ok) return Conflict("Esta reserva ya no está pendiente.");

        var r = await Consulta().FirstAsync(x => x.Id == id);
        Auditoria.Registrar(_db, Actor, "Reserva", r.Id, r.Producto?.Nombre ?? "", "Cancelada", $"{r.Cantidad} × {r.Producto?.Nombre} de {r.Cliente?.Nombre}");
        await _db.SaveChangesAsync();
        if (EsAdmin)
            await _push.NotificarReservaResuelta(r, r.Producto?.Nombre ?? "", "Cancelada", Actor.Id);
        return Ok(ToDto(r));
    }
}
