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

    private readonly MisaBarberContext _db;

    public ProductosController(MisaBarberContext db)
    {
        _db = db;
    }

    private Guid NegocioId => HttpContext.UsuarioActual()!.NegocioId;

    private static ProductoDto ToDto(Producto p) =>
        new(p.Id, p.Nombre, p.Marca, p.Descripcion, p.Precio, p.Stock, p.FotoUrl, p.Estado, p.FechaCreacion);

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
            FotoUrl = dto.FotoUrl,
        };

        _db.Productos.Add(producto);
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

        p.Nombre = dto.Nombre.Trim();
        p.Marca = string.IsNullOrWhiteSpace(dto.Marca) ? null : dto.Marca.Trim();
        p.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        p.Precio = dto.Precio;
        p.Stock = dto.Stock;
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

        Auditoria.Registrar(_db, HttpContext.UsuarioActual()!, "Producto", p.Id, p.Nombre, "Eliminado");
        _db.Productos.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validar(ProductoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return "El nombre es obligatorio.";
        if (dto.Precio < 0)
            return "El precio no puede ser negativo.";
        if (dto.Stock < 0)
            return "El stock no puede ser negativo.";
        return null;
    }
}
