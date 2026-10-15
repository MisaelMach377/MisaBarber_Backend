using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using misabarber.Data;
using misabarber.DTOs;
using misabarber.Models;
using misabarber.Utils;

namespace misabarber.Controllers;

// Mismo criterio de auth que ServiciosController: [RequiereAuth] sin Rol
// puntual en la clase (cualquier sesión válida del negocio puede LEER el
// catálogo -- Admin/Barbero desde el panel, Cliente desde MiCuenta.jsx,
// ver Productos.jsx en ambos frontends), y cada acción de escritura
// (Create/Update/CambiarEstado/Delete) restringe a Admin puntualmente más
// abajo, porque el catálogo de productos lo administra el dueño, no
// cualquier rol autenticado.
[ApiController]
[Route("api/productos")]
[RequiereAuth]
public class ProductosController : ControllerBase
{
    private static readonly string[] EstadosValidos = { "Activo", "Inactivo" };
    private static readonly string[] TiposMovimiento = { "Entrada", "Salida", "Ajuste" };
    private const int CantidadMaxima = 100000;

    private readonly MisaBarberContext _db;

    public ProductosController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static ProductoDto ToDto(Producto p) =>
        new(p.Id, p.Nombre, p.Marca, p.Descripcion, p.Precio, p.Stock, p.StockReservado, p.StockMinimo, p.FotoUrl, p.Estado, p.FechaCreacion);

    [HttpGet]
    public async Task<ActionResult<List<ProductoDto>>> GetAll()
    {
        var lista = await _db.Productos.Where(p => p.NegocioId == NegocioId).OrderBy(p => p.Nombre).ToListAsync();
        return Ok(lista.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductoDto>> GetById(Guid id)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (p is null) return NotFound();
        return Ok(ToDto(p));
    }

    [HttpPost]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<ProductoDto>> Create(ProductoCreateDto dto)
    {
        var error = Validar(dto);
        if (error is not null) return BadRequest(error);

        var producto = new Producto
        {
            NegocioId = NegocioId,
            Nombre = dto.Nombre.Trim(),
            Marca = string.IsNullOrWhiteSpace(dto.Marca) ? null : dto.Marca.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            Precio = dto.Precio,
            Stock = dto.Stock,
            StockMinimo = dto.StockMinimo,
            FotoUrl = dto.FotoUrl,
        };

        _db.Productos.Add(producto);

        // Si nace con unidades, el historial arranca con una Entrada "Stock inicial":
        // así la suma de movimientos siempre cuadra con el stock actual.
        if (producto.Stock > 0)
            AgregarMovimiento(producto, "Entrada", producto.Stock, producto.Stock, "Stock inicial");

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", producto.Id, producto.Nombre, "Creado");
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = producto.Id }, ToDto(producto));
    }

    [HttpPut("{id}")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<ProductoDto>> Update(Guid id, ProductoCreateDto dto)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (p is null) return NotFound();

        var error = Validar(dto);
        if (error is not null) return BadRequest(error);
        if (dto.Stock < p.StockReservado)
            return BadRequest($"Hay {p.StockReservado} unidades reservadas por clientes; el stock no puede quedar por debajo de eso.");

        p.Nombre = dto.Nombre.Trim();
        p.Marca = string.IsNullOrWhiteSpace(dto.Marca) ? null : dto.Marca.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        p.Precio = dto.Precio;
        p.StockMinimo = dto.StockMinimo;

        // Editar el stock a mano desde el formulario sigue permitido, pero ya no
        // pasa en silencio: queda como "Ajuste" en el historial.
        if (dto.Stock != p.Stock)
        {
            var diferencia = dto.Stock - p.Stock;
            p.Stock = dto.Stock;
            AgregarMovimiento(p, "Ajuste", diferencia, p.Stock, "Edición del producto");
        }

        if (dto.FotoUrl is not null)
            p.FotoUrl = dto.FotoUrl;

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", p.Id, p.Nombre, "Editado");
        await _db.SaveChangesAsync();
        return Ok(ToDto(p));
    }

    [HttpPut("{id}/estado")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<ProductoDto>> CambiarEstado(Guid id, CambiarEstadoDto dto)
    {
        if (!EstadosValidos.Contains(dto.Estado))
            return BadRequest("Estado no válido.");

        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (p is null) return NotFound();

        var estadoAnterior = p.Estado;
        p.Estado = dto.Estado;
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", p.Id, p.Nombre, $"Estado: {estadoAnterior} -> {dto.Estado}");
        await _db.SaveChangesAsync();
        return Ok(ToDto(p));
    }

    [HttpDelete("{id}")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var p = await _db.Productos.FirstOrDefaultAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (p is null) return NotFound();

        if (await _db.ReservasProductos.AnyAsync(r => r.ProductoId == id && r.Estado == "Pendiente"))
            return BadRequest("Este producto tiene reservas pendientes. Resuélvelas (entregar o cancelar) antes de eliminarlo.");

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", p.Id, p.Nombre, "Eliminado");
        _db.Productos.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---- Movimientos de stock -------------------------------------------------

    private void AgregarMovimiento(Producto p, string tipo, int cantidadConSigno, int stockResultante, string? motivo)
    {
        var autor = HttpContext.UsuarioActual()!;
        _db.MovimientosStock.Add(new MovimientoStock
        {
            NegocioId = p.NegocioId,
            ProductoId = p.Id,
            Tipo = tipo,
            Cantidad = cantidadConSigno,
            StockResultante = stockResultante,
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            UsuarioId = autor.Id,
            UsuarioNombre = autor.Nombre,
        });
    }

    // Registra una entrada, salida o ajuste. El cambio de Stock se hace con un UPDATE
    // atómico en la base ("Stock = Stock + n ... WHERE Stock + n >= 0"), no leyendo el
    // número, sumando en memoria y guardando: así dos personas moviendo el mismo
    // producto a la vez no se pisan, y el stock nunca queda negativo. Todo va en una
    // transacción junto con la fila del historial.
    [HttpPost("{id}/movimientos")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<ProductoDto>> RegistrarMovimiento(Guid id, MovimientoStockCreateDto dto)
    {
        if (!TiposMovimiento.Contains(dto.Tipo))
            return BadRequest("El tipo de movimiento no es válido.");
        if (dto.Motivo is { Length: > 200 })
            return BadRequest("El motivo no puede pasar de 200 caracteres.");

        if (dto.Tipo == "Ajuste")
        {
            if (dto.Cantidad < 0 || dto.Cantidad > CantidadMaxima)
                return BadRequest("El stock contado debe ser 0 o mayor.");
        }
        else if (dto.Cantidad < 1 || dto.Cantidad > CantidadMaxima)
        {
            return BadRequest("La cantidad debe ser un entero mayor a 0.");
        }

        var existe = await _db.Productos.AnyAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (!existe) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();

        int filas;
        int delta;
        if (dto.Tipo == "Ajuste")
        {
            var anterior = await _db.Productos.AsNoTracking()
                .Where(x => x.Id == id && x.NegocioId == NegocioId)
                .Select(x => new { x.Stock, x.StockReservado })
                .FirstAsync();
            // No se puede contar menos de lo que ya está apartado para clientes:
            // primero hay que cancelar/entregar esas reservas.
            if (dto.Cantidad < anterior.StockReservado)
                return BadRequest($"Hay {anterior.StockReservado} unidades reservadas por clientes; el stock no puede quedar por debajo de eso.");
            delta = dto.Cantidad - anterior.Stock;
            if (delta == 0) return BadRequest("El stock contado es igual al actual.");

            var nuevo = dto.Cantidad;
            filas = await _db.Productos
                .Where(x => x.Id == id && x.NegocioId == NegocioId)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.Stock, nuevo));
        }
        else
        {
            delta = dto.Tipo == "Entrada" ? dto.Cantidad : -dto.Cantidad;
            filas = await _db.Productos
                .Where(x => x.Id == id && x.NegocioId == NegocioId && x.Stock + delta >= x.StockReservado)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.Stock, p => p.Stock + delta));
        }

        if (filas == 0)
            return BadRequest("No hay stock disponible suficiente para esa salida (parte del stock está reservada por clientes).");

        // ExecuteUpdate no pasa por el change tracker: se vuelve a leer el producto.
        var producto = await _db.Productos.AsNoTracking().FirstAsync(x => x.Id == id && x.NegocioId == NegocioId);

        AgregarMovimiento(producto, dto.Tipo, delta, producto.Stock, dto.Motivo);
        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", producto.Id, producto.Nombre,
            $"Stock: {dto.Tipo} {(delta > 0 ? "+" : "")}{delta} -> {producto.Stock}", dto.Motivo);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(ToDto(producto));
    }

    // Historial de un producto, del más reciente al más antiguo. Solo Admin: el
    // cliente ve el catálogo, pero no quién movió el stock ni cuándo.
    [HttpGet("{id}/movimientos")]
    [RequiereAuth(Rol = "Admin")]
    public async Task<ActionResult<List<MovimientoStockDto>>> GetMovimientos(Guid id, [FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 200);

        var existe = await _db.Productos.AnyAsync(x => x.Id == id && x.NegocioId == NegocioId);
        if (!existe) return NotFound();

        var lista = await _db.MovimientosStock.AsNoTracking()
            .Where(m => m.ProductoId == id && m.NegocioId == NegocioId)
            .OrderByDescending(m => m.Fecha)
            .Take(take)
            .Select(m => new MovimientoStockDto(m.Id, m.Tipo, m.Cantidad, m.StockResultante, m.Motivo, m.UsuarioNombre, m.Fecha))
            .ToListAsync();

        return Ok(lista);
    }

    private static string? Validar(ProductoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return "El nombre es obligatorio.";
        if (dto.Precio < 0)
            return "El precio no puede ser negativo.";
        if (dto.Stock < 0)
            return "El stock no puede ser negativo.";
        if (dto.StockMinimo < 0 || dto.StockMinimo > CantidadMaxima)
            return "El stock mínimo no es válido.";
        return null;
    }
}
