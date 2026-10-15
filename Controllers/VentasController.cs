using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Venta directa en mostrador: "vendí 1 cera a Juan". Solo Admin (el stock y la
// caja son del dueño). Las ventas que vienen de una reserva entregada las crea
// ReservasStock.Entregar, no este controller.
[ApiController]
[Route("api/ventas")]
[RequiereAuth(Rol = "Admin")]
public class VentasController : ControllerBase
{
    private const int CantidadMaxima = 1000;

    private readonly MisaBarberContext _db;

    public VentasController(MisaBarberContext db)
    {
        _db = db;
    }

    private UsuarioClaims Actor => HttpContext.UsuarioActual()!;
    private Guid NegocioId => Actor.NegocioId;

    private static VentaDto ToDto(VentaProducto v) => new(
        v.Id, v.ProductoId, v.ProductoNombre, v.ClienteNombre, v.Cantidad,
        v.PrecioUnitario, v.Total, v.Origen, v.UsuarioNombre, v.Fecha);

    // Mismo cuidado de concurrencia que las reservas y los movimientos: el
    // descuento de stock es UN UPDATE condicional, y solo se puede vender lo
    // DISPONIBLE (Stock - StockReservado) -- lo apartado para un cliente con
    // reserva no se le puede vender a otro en el mostrador.
    [HttpPost]
    public async Task<ActionResult<VentaDto>> Crear(VentaCreateDto dto)
    {
        if (dto.Cantidad < 1 || dto.Cantidad > CantidadMaxima)
            return BadRequest("La cantidad debe ser un entero mayor a 0.");
        if (dto.PrecioUnitario is < 0)
            return BadRequest("El precio no puede ser negativo.");

        var producto = await _db.Productos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.ProductoId && p.NegocioId == NegocioId);
        if (producto is null) return BadRequest("El producto no existe.");

        string? clienteNombre = null;
        if (dto.ClienteId is not null)
        {
            clienteNombre = await _db.Clientes.AsNoTracking()
                .Where(c => c.Id == dto.ClienteId && c.NegocioId == NegocioId)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync();
            if (clienteNombre is null) return BadRequest("El cliente no existe.");
        }

        var precio = dto.PrecioUnitario ?? producto.Precio;
        var cantidad = dto.Cantidad;

        await using var tx = await _db.Database.BeginTransactionAsync();

        var filas = await _db.Productos
            .Where(p => p.Id == producto.Id && p.NegocioId == NegocioId && p.Stock - p.StockReservado >= cantidad)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - cantidad));
        if (filas == 0)
            return BadRequest("No hay stock disponible suficiente (parte del stock puede estar reservado por clientes).");

        var stockResultante = await _db.Productos.AsNoTracking()
            .Where(p => p.Id == producto.Id)
            .Select(p => p.Stock)
            .FirstAsync();

        _db.MovimientosStock.Add(new MovimientoStock
        {
            NegocioId = NegocioId,
            ProductoId = producto.Id,
            Tipo = "Salida",
            Cantidad = -cantidad,
            StockResultante = stockResultante,
            Motivo = $"Venta — {clienteNombre ?? "mostrador"}",
            UsuarioId = Actor.Id,
            UsuarioNombre = Actor.Nombre,
        });

        var venta = new VentaProducto
        {
            NegocioId = NegocioId,
            ProductoId = producto.Id,
            ProductoNombre = producto.Nombre,
            ClienteId = dto.ClienteId,
            ClienteNombre = clienteNombre,
            Cantidad = cantidad,
            PrecioUnitario = precio,
            Total = precio * cantidad,
            Origen = "Mostrador",
            UsuarioId = Actor.Id,
            UsuarioNombre = Actor.Nombre,
        };
        _db.VentasProductos.Add(venta);

        Auditoria.Registrar(_db, Actor, "Venta", venta.Id, producto.Nombre,
            "Venta en mostrador", $"{cantidad} × {producto.Nombre} a S/ {precio:0.00}" + (clienteNombre is null ? "" : $" — {clienteNombre}"));
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(ToDto(venta));
    }

    // Últimas ventas, de la más reciente a la más antigua.
    [HttpGet]
    public async Task<ActionResult<List<VentaDto>>> GetAll([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var lista = await _db.VentasProductos.AsNoTracking()
            .Where(v => v.NegocioId == NegocioId)
            .OrderByDescending(v => v.Fecha)
            .Take(take)
            .ToListAsync();
        return Ok(lista.Select(ToDto));
    }
}
