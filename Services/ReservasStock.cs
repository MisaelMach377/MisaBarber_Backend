using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.Models;

namespace misabarber.Services;

// Las dos operaciones que "cierran" una reserva Pendiente, en un solo lugar
// porque las usan tanto el ReservasController (cancelar / entregar a mano)
// como ReservasVencimientoService (vencer sola) -- y las tres tienen que
// respetar exactamente las mismas reglas de concurrencia:
//
//  1. El cambio de estado es un UPDATE condicional (WHERE Estado = 'Pendiente').
//     Si dos actores intentan cerrar la misma reserva a la vez (cliente
//     cancelando mientras el servicio la vence, o Admin entregando mientras el
//     cliente cancela), la base deja pasar a UNO; el otro ve 0 filas y se
//     retira sin tocar el stock. Así StockReservado nunca se libera dos veces.
//  2. El stock se toca en la MISMA transacción que el estado: o cambian los dos
//     o ninguno. Un crash a la mitad no deja unidades "perdidas" apartadas.
public static class ReservasStock
{
    // Cancelada | Vencida: la reserva se cae y las unidades vuelven a estar
    // disponibles (baja StockReservado; el Stock físico no cambió nunca).
    // Devuelve false si la reserva ya no estaba Pendiente (alguien llegó antes).
    public static async Task<bool> Liberar(MisaBarberContext db, Guid reservaId, string estadoFinal, string? resueltoPor)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var ahora = DateTime.UtcNow;
        var filas = await db.ReservasProductos
            .Where(r => r.Id == reservaId && r.Estado == "Pendiente")
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Estado, estadoFinal)
                .SetProperty(r => r.FechaResolucion, ahora)
                .SetProperty(r => r.ResueltoPorNombre, resueltoPor));
        if (filas == 0) return false;

        var datos = await db.ReservasProductos.AsNoTracking()
            .Where(r => r.Id == reservaId)
            .Select(r => new { r.ProductoId, r.Cantidad })
            .FirstAsync();

        var cantidad = datos.Cantidad;
        await db.Productos
            .Where(p => p.Id == datos.ProductoId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockReservado, p => p.StockReservado - cantidad));

        await tx.CommitAsync();
        return true;
    }

    // Entregada: el cliente vino y se llevó el producto. Ahora SÍ sale del
    // inventario físico (Stock -= n) y deja de estar apartado (StockReservado
    // -= n), más una Salida en el historial con el nombre del cliente -- que
    // era justo lo que faltaba: "se baja el stock pero no sabes a quién".
    public static async Task<bool> Entregar(MisaBarberContext db, Guid reservaId, Guid usuarioId, string usuarioNombre)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var ahora = DateTime.UtcNow;
        var filas = await db.ReservasProductos
            .Where(r => r.Id == reservaId && r.Estado == "Pendiente")
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Estado, "Entregada")
                .SetProperty(r => r.FechaResolucion, ahora)
                .SetProperty(r => r.ResueltoPorNombre, usuarioNombre));
        if (filas == 0) return false;

        var r = await db.ReservasProductos.AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Producto)
            .FirstAsync(x => x.Id == reservaId);

        var cantidad = r.Cantidad;
        await db.Productos
            .Where(p => p.Id == r.ProductoId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock - cantidad)
                .SetProperty(p => p.StockReservado, p => p.StockReservado - cantidad));

        var stockResultante = await db.Productos.AsNoTracking()
            .Where(p => p.Id == r.ProductoId)
            .Select(p => p.Stock)
            .FirstAsync();

        db.MovimientosStock.Add(new MovimientoStock
        {
            NegocioId = r.NegocioId,
            ProductoId = r.ProductoId,
            Tipo = "Salida",
            Cantidad = -cantidad,
            StockResultante = stockResultante,
            Motivo = $"Reserva entregada — {r.Cliente?.Nombre}",
            UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre,
        });

        // Queda registrada como venta (Origen "Reserva") para que los reportes de
        // productos incluyan también lo que se vendió vía reserva.
        db.VentasProductos.Add(new VentaProducto
        {
            NegocioId = r.NegocioId,
            ProductoId = r.ProductoId,
            ProductoNombre = r.Producto?.Nombre ?? "",
            ClienteId = r.ClienteId,
            ClienteNombre = r.Cliente?.Nombre,
            Cantidad = cantidad,
            PrecioUnitario = r.PrecioUnitario,
            Total = r.PrecioUnitario * cantidad,
            Origen = "Reserva",
            ReservaId = r.Id,
            UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre,
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }
}
