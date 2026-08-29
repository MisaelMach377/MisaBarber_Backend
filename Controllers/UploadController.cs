using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using misabarber.Utils;

namespace misabarber.Controllers;

// Sube fotos de Clientes/Barberos/Usuarios a wwwroot/uploads/{carpeta}/ y
// devuelve la URL relativa para guardar en FotoUrl. Ahora exige sesión
// (ver RequiereAuth) — antes de que existiera login, este era de los
// primeros endpoints que había que proteger, porque cualquiera que le
// pegara a la API podía subir un archivo.
[ApiController]
[Route("api/upload")]
[RequiereAuth]
public class UploadController : ControllerBase
{
    private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long TamanoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;

    public UploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("{carpeta}")]
    [RequestSizeLimit(TamanoMaximoBytes)]
    public async Task<IActionResult> Subir(string carpeta, IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No se envió ningún archivo.");

        if (file.Length > TamanoMaximoBytes)
            return BadRequest("La imagen no puede pesar más de 5 MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(ext))
            return BadRequest("Formato no permitido. Usa JPG, PNG o WEBP.");

        // "carpeta" viene de la URL — se sanea a solo letras/números para
        // que nadie pueda meter "../" y terminar escribiendo fuera de
        // wwwroot/uploads.
        var carpetaSegura = new string(carpeta.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(carpetaSegura))
            carpetaSegura = "general";

        var nombreArchivo = $"{Guid.NewGuid()}{ext}";
        var raizWeb = string.IsNullOrEmpty(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;
        var directorio = Path.Combine(raizWeb, "uploads", carpetaSegura);
        Directory.CreateDirectory(directorio);

        var rutaFisica = Path.Combine(directorio, nombreArchivo);
        await using (var stream = System.IO.File.Create(rutaFisica))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { url = $"/uploads/{carpetaSegura}/{nombreArchivo}" });
    }
}
