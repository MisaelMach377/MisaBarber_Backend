using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Utils;

namespace misabarber.Controllers;

// Reportes de productos (solo lectura, solo Admin): ventas por producto en un
// rango y la lista de reposición.
[ApiController]
[Route("api/stock")]
[RequiereAuth(Rol = "Admin")]
public class StockReportesController : ControllerBase
{
    private readonly MisaBarberContext _db;

    public StockReportesController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    // "desde"/"hasta" son fechas de pared de Perú (yyyy-MM-dd, ambas inclusive).
    // Las ventas se guardan en UTC, así que el rango se corre 5 horas: el día
    // Lima D va de D 05:00 UTC a D+1 05:00 UTC (Perú es UTC-5 fijo, ver Reloj).
    [HttpGet("reporte")]
    public async Task<ActionResult<ReporteProductosDto>> Reporte([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        if (hasta.Date < desde.Date) return BadRequest("El rango de fechas no es válido.");
        if ((hasta.Date - desde.Date).TotalDays > 366) return BadRequest("El rango máximo es de un año.");

        var desdeUtc = desde.Date.AddHours(5);
        var hastaUtc = hasta.Date.AddDays(1).AddHours(5);

        var ventas = _db.VentasProductos.AsNoTracking()
            .Where(v => v.NegocioId == NegocioId && v.Fecha >= desdeUtc && v.Fecha < hastaUtc);

        var agregado = await ventas
            .GroupBy(v => new { v.ProductoId, v.ProductoNombre })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.ProductoNombre,
                Unidades = g.Sum(v => v.Cantidad),
                Ingresos = g.Sum(v => v.Total),
                Ultima = g.Max(v => v.Fecha),
            })
            .ToListAsync();

        var filas = agregado
            .OrderByDescending(a => a.Ingresos)
            .Select(a => new ReporteProductoFilaDto(a.ProductoId, a.ProductoNombre, a.Unidades, a.Ingresos, a.Ultima))
            .ToList();

        var numeroVentas = await ventas.CountAsync();

        // Productos activos con stock que NO vendieron nada en el rango: plata
        // parada en el estante.
        var sinVentas = await _db.Productos.AsNoTracking()
            .Where(p => p.NegocioId == NegocioId && p.Estado == "Activo" && p.Stock > 0
                        && !ventas.Any(v => v.ProductoId == p.Id))
            .OrderByDescending(p => p.Stock * p.Precio)
            .Select(p => new ReporteSinVentasDto(p.Id, p.Nombre, p.Marca, p.Stock, p.Stock * p.Precio))
            .ToListAsync();

        return Ok(new ReporteProductosDto(
            filas.Sum(f => f.Ingresos), filas.Sum(f => f.Unidades), numeroVentas, filas, sinVentas));
    }

    // Productos activos con stock <= su mínimo, con una cantidad sugerida a
    // pedir. Sugerido = lo que falta para tener el DOBLE del mínimo, o lo que se
    // vendió en los últimos 30 días si eso es mayor (un producto que rota mucho
    // necesita más colchón que uno que casi no se mueve), mínimo 1. Es solo una
    // sugerencia: el Admin edita la cantidad en pantalla antes de pedir.
    [HttpGet("reposicion")]
    public async Task<ActionResult<List<ReposicionItemDto>>> Reposicion()
    {
        var hace30 = DateTime.UtcNow.AddDays(-30);

        var bajos = await _db.Productos.AsNoTracking()
            .Where(p => p.NegocioId == NegocioId && p.Estado == "Activo" && p.Stock <= p.StockMinimo)
            .OrderBy(p => p.Stock).ThenBy(p => p.Nombre)
            .ToListAsync();

        var ids = bajos.Select(p => p.Id).ToList();
        var vendidas = await _db.VentasProductos.AsNoTracking()
            .Where(v => v.NegocioId == NegocioId && v.Fecha >= hace30 && v.ProductoId != null && ids.Contains(v.ProductoId.Value))
            .GroupBy(v => v.ProductoId!.Value)
            .Select(g => new { Id = g.Key, Unidades = g.Sum(v => v.Cantidad) })
            .ToDictionaryAsync(x => x.Id, x => x.Unidades);

        var lista = bajos.Select(p =>
        {
            var v30 = vendidas.GetValueOrDefault(p.Id);
            var sugerido = Math.Max(Math.Max(2 * p.StockMinimo - p.Stock, v30 - p.Stock), 1);
            return new ReposicionItemDto(p.Id, p.Nombre, p.Marca, p.Stock, p.StockReservado, p.StockMinimo, v30, sugerido);
        }).ToList();

        return Ok(lista);
    }
}
