using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using misabarber.Utils;

namespace misabarber.Controllers;

// Sube fotos de Clientes/Barberos/Usuarios/Negocios a Cloudinary (antes
// se guardaban en wwwroot/uploads/{carpeta}/, ver el comentario en
// Program.cs sobre por qué se cambió) y devuelve la URL absoluta para
// guardar en FotoUrl/LogoUrl. Sigue exigiendo sesión (RequiereAuth) --
// cualquiera que le pegara a este endpoint sin login podría subir un
// archivo y gastar la cuota gratuita de la cuenta de Cloudinary.
[ApiController]
[Route("api/upload")]
[RequiereAuth]
public class UploadController : ControllerBase
{
    // Puede ser null si Cloudinary todavía no está configurado en este
    // ambiente (ver registro en Program.cs) -- se valida al entrar a Subir().
    private readonly Cloudinary? _cloudinary;

    private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long TamanoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    // Todo lo que sube este endpoint es una foto de perfil/logo (Cliente,
    // Barbero, Usuario o Negocio) -- se ve en círculos/avatares chicos de
    // la UI (ver FotoPicker.jsx), así que un solo ancho máximo alcanza
    // para las 4 carpetas, a diferencia de MisaDesk que sí distingue
    // avatar vs. documento.
    private const int AnchoMaximo = 600;

    public UploadController(Cloudinary? cloudinary)
    {
        _cloudinary = cloudinary;
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

        // "carpeta" viene de la URL -- se sanea a solo letras/números
        // (ya no por riesgo de "../" como cuando era una ruta de disco,
        // sino simplemente para no terminar creando carpetas sueltas con
        // basura en Cloudinary si alguien manda cualquier cosa acá).
        var carpetaSegura = new string(carpeta.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(carpetaSegura))
            carpetaSegura = "general";

        if (_cloudinary is null)
        {
            return StatusCode(500,
                "Cloudinary no está configurado en este ambiente. Faltan Cloudinary:CloudName / ApiKey / ApiSecret " +
                "(User Secrets en local, variables de entorno en Railway).");
        }

        // "misabarber/{carpeta}" -- el prefijo separa esto de cualquier
        // otro proyecto que use la misma cuenta de Cloudinary (ej.
        // MisaDesk sube a "misadesk/{carpeta}"), así nunca se mezclan ni
        // se pisan entre sí aunque compartan cuenta. Transformation
        // redimensiona al ancho máximo (sin agrandar fotos chicas, "limit"
        // no estira) y deja que Cloudinary elija el mejor formato/calidad
        // -- clave para no gastar de más la cuota del plan gratis (una
        // foto de celular sin comprimir pesa 2-5MB, con esto baja bastante).
        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"misabarber/{carpetaSegura}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false,
            Transformation = new Transformation().Width(AnchoMaximo).Crop("limit").Quality("auto").FetchFormat("auto"),
        };
        var resultado = await _cloudinary.UploadAsync(uploadParams);

        if (resultado.Error is not null)
            return StatusCode(500, $"No se pudo subir el archivo: {resultado.Error.Message}");

        var url = resultado.SecureUrl?.ToString() ?? resultado.Url?.ToString();
        if (string.IsNullOrEmpty(url))
            return StatusCode(500, "Cloudinary no devolvió una URL para el archivo subido.");

        return Ok(new { url });
    }
}
