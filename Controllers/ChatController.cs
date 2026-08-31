using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Chat en vivo entre un Cliente y el staff de su barbería. Sin
// WebSockets/SignalR a propósito: este entorno no tiene salida a NuGet
// (ver el comentario de JwtHelper.cs) y armar la autenticación de un Hub
// a mano sobre el JWT casero es un riesgo que no vale la pena -- en su
// lugar, tanto Chat.jsx (staff) como ChatWidget.jsx (cliente) consultan
// estos endpoints cada pocos segundos (polling), que reusan exactamente
// el mismo [RequiereAuth] + HttpContext.UsuarioActual() que el resto del
// sistema. Para el volumen de mensajes de una barbería esto se siente
// "en vivo" igual, con muchísimo menos riesgo.
[ApiController]
[Route("api/chat")]
[RequiereAuth]
public class ChatController : ControllerBase
{
    private const int TextoMaxLength = 2000;

    private readonly MisaBarberContext _db;

    public ChatController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    // Chat es un módulo de Plan Pro (ver Utils/Modulos.cs) -- mismo fence
    // del lado del servidor que ya tienen Apariencia/Historial: un
    // negocio en Free no puede leer ni mandar mensajes por acá aunque le
    // pegue directo a la API, aunque el front ya le esconda el link/la
    // burbuja (ver Layout.jsx / ChatWidget.jsx).
    private async Task<bool> PlanPermiteChat()
    {
        var plan = await _db.Negocios.Where(n => n.Id == NegocioId).Select(n => n.Plan).FirstOrDefaultAsync();
        return Modulos.DeNegocio(plan).Contains("Chat");
    }

    private static ChatMensajeDto ToDto(ChatMensaje m) => new(m.Id, m.AutorNombre, m.AutorRol, m.Texto, m.FechaEnvio);

    private static ActionResult<ChatMensajeDto>? ValidarTexto(string? texto, out string limpio)
    {
        limpio = (texto ?? "").Trim();
        if (string.IsNullOrWhiteSpace(limpio)) return new BadRequestObjectResult("Escribe un mensaje.");
        if (limpio.Length > TextoMaxLength) return new BadRequestObjectResult("El mensaje es demasiado largo.");
        return null;
    }

    // ---- Lado Cliente: una sola conversación, la propia ----

    // GET api/chat/mio -- el hilo completo del cliente logueado con SU
    // barbería. De paso marca como leído todo lo que le mandó el staff
    // (se está abriendo el chat ahora mismo).
    [HttpGet("mio")]
    public async Task<ActionResult<List<ChatMensajeDto>>> GetPropio()
    {
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol != "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Forbid();

        var mensajes = await _db.ChatMensajes
            .Where(m => m.NegocioId == NegocioId && m.ClienteId == usuario.Id)
            .OrderBy(m => m.FechaEnvio)
            .ToListAsync();

        var sinLeer = mensajes.Where(m => m.AutorRol != "Cliente" && !m.LeidoPorCliente).ToList();
        if (sinLeer.Count > 0)
        {
            foreach (var m in sinLeer) m.LeidoPorCliente = true;
            await _db.SaveChangesAsync();
        }

        return Ok(mensajes.Select(ToDto));
    }

    [HttpPost("mio")]
    public async Task<ActionResult<ChatMensajeDto>> EnviarPropio(EnviarMensajeDto dto)
    {
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol != "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Forbid();

        var error = ValidarTexto(dto.Texto, out var texto);
        if (error is not null) return error;

        var mensaje = new ChatMensaje
        {
            NegocioId = NegocioId,
            ClienteId = usuario.Id,
            ClienteNombre = usuario.Nombre,
            AutorId = usuario.Id,
            AutorNombre = usuario.Nombre,
            AutorRol = "Cliente",
            Texto = texto,
            LeidoPorCliente = true,
            LeidoPorStaff = false,
        };
        _db.ChatMensajes.Add(mensaje);
        await _db.SaveChangesAsync();

        return Ok(ToDto(mensaje));
    }

    // Badge liviano para la burbuja flotante cuando está cerrada -- no
    // trae los mensajes, solo el conteo, para poder consultarlo seguido
    // sin pesar. 0 (no 403) si el negocio no tiene Chat, así el widget
    // simplemente no muestra nada en vez de loguear un error en consola.
    [HttpGet("mio/no-leidos")]
    public async Task<ActionResult<int>> GetNoLeidosPropio()
    {
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol != "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Ok(0);

        var count = await _db.ChatMensajes.CountAsync(m =>
            m.NegocioId == NegocioId && m.ClienteId == usuario.Id && m.AutorRol != "Cliente" && !m.LeidoPorCliente);
        return Ok(count);
    }

    // ---- Lado staff (Admin/Barbero): una conversación por cliente ----

    // GET api/chat/conversaciones -- inbox: un cliente por fila, con su
    // último mensaje y cuántos de él quedan sin leer. Ordenado por
    // actividad más reciente primero, como cualquier bandeja de chat.
    [HttpGet("conversaciones")]
    public async Task<ActionResult<List<ChatConversacionDto>>> GetConversaciones()
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Forbid();

        // El proveedor de Postgres no puede traducir "último mensaje +
        // conteo condicional" agrupado en una sola consulta SQL (EF tira
        // InvalidOperationException: "could not be translated"). Se trae
        // la fila mínima por mensaje (ya filtrada por NegocioId y
        // ordenada por fecha) y se agrupa DESPUÉS, en memoria -- ahí
        // cualquier expresión de C# vale, no hay traducción de por medio.
        // El volumen de mensajes de una barbería es chico, así que traer
        // todo no pesa.
        var mensajes = await _db.ChatMensajes
            .Where(m => m.NegocioId == NegocioId)
            .OrderByDescending(m => m.FechaEnvio)
            .Select(m => new { m.ClienteId, m.ClienteNombre, m.Texto, m.FechaEnvio, m.AutorRol, m.LeidoPorStaff })
            .ToListAsync();

        var conversaciones = mensajes
            .GroupBy(m => new { m.ClienteId, m.ClienteNombre })
            .Select(g => new ChatConversacionDto(
                g.Key.ClienteId,
                g.Key.ClienteNombre,
                g.First().Texto, // ya viene ordenado desc por fecha desde la consulta
                g.Max(m => m.FechaEnvio),
                g.Count(m => m.AutorRol == "Cliente" && !m.LeidoPorStaff)
            ))
            .OrderByDescending(c => c.UltimoMensajeFecha)
            .ToList();

        return Ok(conversaciones);
    }

    // GET api/chat/conversaciones/{clienteId} -- el hilo con ese cliente.
    // Marca como leído lo que él mandó (se está abriendo su conversación
    // ahora mismo).
    [HttpGet("conversaciones/{clienteId}")]
    public async Task<ActionResult<List<ChatMensajeDto>>> GetConversacion(Guid clienteId)
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Forbid();

        var mensajes = await _db.ChatMensajes
            .Where(m => m.NegocioId == NegocioId && m.ClienteId == clienteId)
            .OrderBy(m => m.FechaEnvio)
            .ToListAsync();

        if (mensajes.Count == 0) return NotFound();

        var sinLeer = mensajes.Where(m => m.AutorRol == "Cliente" && !m.LeidoPorStaff).ToList();
        if (sinLeer.Count > 0)
        {
            foreach (var m in sinLeer) m.LeidoPorStaff = true;
            await _db.SaveChangesAsync();
        }

        return Ok(mensajes.Select(ToDto));
    }

    [HttpPost("conversaciones/{clienteId}")]
    public async Task<ActionResult<ChatMensajeDto>> EnviarAConversacion(Guid clienteId, EnviarMensajeDto dto)
    {
        var usuario = HttpContext.UsuarioActual()!;
        if (usuario.Rol == "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Forbid();

        var error = ValidarTexto(dto.Texto, out var texto);
        if (error is not null) return error;

        // El nombre del cliente sale de un mensaje anterior si la
        // conversación ya existe, o de su cuenta si el staff es quien la
        // abre por primera vez (ej. le escribe primero después de
        // agendarle una cita por teléfono).
        var clienteNombre = await _db.ChatMensajes
            .Where(m => m.NegocioId == NegocioId && m.ClienteId == clienteId)
            .Select(m => m.ClienteNombre)
            .FirstOrDefaultAsync();

        if (clienteNombre is null)
        {
            var cliente = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == clienteId && u.NegocioId == NegocioId && u.Rol == "Cliente");
            if (cliente is null) return NotFound("Ese cliente no existe.");
            clienteNombre = cliente.Nombre;
        }

        var mensaje = new ChatMensaje
        {
            NegocioId = NegocioId,
            ClienteId = clienteId,
            ClienteNombre = clienteNombre,
            AutorId = usuario.Id,
            AutorNombre = usuario.Nombre,
            AutorRol = usuario.Rol,
            Texto = texto,
            LeidoPorStaff = true,
            LeidoPorCliente = false,
        };
        _db.ChatMensajes.Add(mensaje);
        await _db.SaveChangesAsync();

        return Ok(ToDto(mensaje));
    }

    // Badge del sidebar (Layout.jsx) -- total de mensajes sin leer sumando
    // TODAS las conversaciones del negocio, no solo la abierta.
    [HttpGet("no-leidos")]
    public async Task<ActionResult<int>> GetNoLeidos()
    {
        if (HttpContext.UsuarioActual()!.Rol == "Cliente") return Forbid();
        if (!await PlanPermiteChat()) return Ok(0);

        var count = await _db.ChatMensajes.CountAsync(m =>
            m.NegocioId == NegocioId && m.AutorRol == "Cliente" && !m.LeidoPorStaff);
        return Ok(count);
    }
}
