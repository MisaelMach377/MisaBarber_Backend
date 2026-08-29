using System.Security.Cryptography;

namespace misabarber.Utils;

// Hash de contraseñas con PBKDF2/HMAC-SHA256 (nativo de
// System.Security.Cryptography, el mismo algoritmo que usa ASP.NET Core
// Identity por default) — no reversible a propósito, a diferencia del
// CryptoPassword (AES reversible) de MyPortalVESQL: ahí se necesita poder
// MOSTRAR la contraseña original en un caso puntual, acá no existe ese
// caso de uso, así que un hash de una sola vía es la opción más segura.
// Formato guardado: "<salt en base64>.<hash en base64>".
public static class PasswordHasher
{
    private const int TamanoSalt = 16;
    private const int TamanoHash = 32;
    private const int Iteraciones = 100_000;
    private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, Algoritmo, TamanoHash);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string passwordHash)
    {
        var partes = passwordHash.Split('.', 2);
        if (partes.Length != 2) return false;

        byte[] salt, hashGuardado;
        try
        {
            salt = Convert.FromBase64String(partes[0]);
            hashGuardado = Convert.FromBase64String(partes[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var hashIntento = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, Algoritmo, hashGuardado.Length);
        return CryptographicOperations.FixedTimeEquals(hashIntento, hashGuardado);
    }
}
