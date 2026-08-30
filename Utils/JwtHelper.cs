using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace misabarber.Utils;

// Claims mínimos que viajan en el token — lo justo para saber quién hace
// el request, de qué barbería es (multi-tenant, ver Models/Negocio.cs) y
// qué rol tiene sin ir a la base de datos en cada llamada.
public record UsuarioClaims(
    Guid Id,
    string Nombre,
    string Email,
    string Rol,
    Guid NegocioId,
    Guid? BarberoId,
    Guid? ClienteId
);

// JWT (HS256) armado a mano con primitivas nativas de .NET (HMACSHA256 +
// System.Text.Json) en vez del paquete
// Microsoft.AspNetCore.Authentication.JwtBearer: este entorno de
// desarrollo no tiene salida a NuGet, así que en vez de dejar el login sin
// terminar se implementó el estándar JWT/HS256 directo — header y payload
// en JSON codificados en Base64Url, firmados con HMACSHA256 y la clave de
// appsettings ("Jwt:Secret"). Es el mismo algoritmo que usa el paquete
// oficial, solo sin la librería envolvente; si más adelante hay acceso a
// NuGet, esto se reemplaza sin tocar cómo el resto del código lo usa
// (siempre a través de Generar/Validar, nunca tocando el token a mano).
public static class JwtHelper
{
    private const int DiasValidez = 7;

    public static string Generar(UsuarioClaims usuario, string secreto)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var ahora = DateTimeOffset.UtcNow;
        var payload = new
        {
            sub = usuario.Id,
            nombre = usuario.Nombre,
            email = usuario.Email,
            rol = usuario.Rol,
            negocioId = usuario.NegocioId,
            barberoId = usuario.BarberoId,
            clienteId = usuario.ClienteId,
            iat = ahora.ToUnixTimeSeconds(),
            exp = ahora.AddDays(DiasValidez).ToUnixTimeSeconds(),
        };

        var headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var firma = Firmar($"{headerB64}.{payloadB64}", secreto);

        return $"{headerB64}.{payloadB64}.{firma}";
    }

    // Devuelve los claims si el token es válido (firma correcta y todavía
    // no expiró), o null si no — el llamador decide qué hacer con un
    // token inválido (el middleware de Program.cs simplemente no marca al
    // usuario como autenticado). Un token viejo (emitido antes de agregar
    // negocioId al payload) no trae esa propiedad y también se trata como
    // inválido -- fuerza a re-loguear una sola vez tras este cambio, en
    // vez de dejar pasar un usuario sin Negocio asignado.
    public static UsuarioClaims? Validar(string token, string secreto)
    {
        var partes = token.Split('.');
        if (partes.Length != 3) return null;

        var firmaEsperada = Firmar($"{partes[0]}.{partes[1]}", secreto);
        var firmaEsperadaBytes = Encoding.UTF8.GetBytes(firmaEsperada);
        var firmaRecibidaBytes = Encoding.UTF8.GetBytes(partes[2]);

        if (firmaEsperadaBytes.Length != firmaRecibidaBytes.Length) return null;
        if (!CryptographicOperations.FixedTimeEquals(firmaEsperadaBytes, firmaRecibidaBytes)) return null;

        JsonElement payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(Base64UrlDecode(partes[1]));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }

        if (!payload.TryGetProperty("exp", out var expEl)) return null;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expEl.GetInt64()) return null;

        try
        {
            if (!payload.TryGetProperty("negocioId", out var negocioIdEl) || negocioIdEl.ValueKind == JsonValueKind.Null)
                return null;

            var barberoId = payload.TryGetProperty("barberoId", out var b) && b.ValueKind != JsonValueKind.Null
                ? b.GetGuid()
                : (Guid?)null;

            var clienteId = payload.TryGetProperty("clienteId", out var c) && c.ValueKind != JsonValueKind.Null
                ? c.GetGuid()
                : (Guid?)null;

            return new UsuarioClaims(
                payload.GetProperty("sub").GetGuid(),
                payload.GetProperty("nombre").GetString() ?? "",
                payload.GetProperty("email").GetString() ?? "",
                payload.GetProperty("rol").GetString() ?? "",
                negocioIdEl.GetGuid(),
                barberoId,
                clienteId
            );
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string Firmar(string data, string secreto)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + ((4 - s.Length % 4) % 4), '=');
        return Convert.FromBase64String(s);
    }
}
