using System.Net.Http.Json;

namespace misabarber.Services;

// Envío de correos transaccionales por la API HTTP de Brevo (no SMTP): los
// planes de Railway normalmente bloquean las conexiones SMTP salientes
// (puertos 25/465/587), en cambio HTTPS siempre sale. Las 3 claves viajan
// por configuración (Brevo__ApiKey, Brevo__SenderEmail, Brevo__SenderName en
// Railway), nunca hardcodeadas. Si faltan, EnviarAsync devuelve false y deja
// un warning en el log en vez de tumbar nada -- mismo patrón que Cloudinary.
public class EmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(HttpClient http, IConfiguration config, ILogger<EmailService> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public async Task<bool> EnviarAsync(string para, string asunto, string html)
    {
        var apiKey = _config["Brevo:ApiKey"];
        var remitente = _config["Brevo:SenderEmail"];
        var nombre = _config["Brevo:SenderName"] ?? "MisaBarber";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remitente))
        {
            _log.LogWarning("Brevo no está configurado (Brevo:ApiKey / Brevo:SenderEmail): no se envió el correo.");
            return false;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            req.Headers.Add("api-key", apiKey);
            req.Content = JsonContent.Create(new
            {
                sender = new { name = nombre, email = remitente },
                to = new[] { new { email = para } },
                subject = asunto,
                htmlContent = html,
            });

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogError("Brevo respondió {Status}: {Body}", (int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falló el envío de correo con Brevo.");
            return false;
        }
    }
}
