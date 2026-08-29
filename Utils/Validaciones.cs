using System.Text.RegularExpressions;

namespace misabarber.Utils;

public static class Validaciones
{
    private static readonly Regex SoloDigitos = new(@"^\d{1,9}$", RegexOptions.Compiled);

    // Máximo 9 dígitos numéricos (celular peruano estándar: 9xxxxxxxx). Se
    // valida acá además de en el front — el front nunca alcanza solo, porque
    // cualquiera le puede pegar directo a la API con Postman/curl y saltarse
    // el input del formulario.
    public static bool TelefonoValido(string? telefono) =>
        string.IsNullOrWhiteSpace(telefono) || SoloDigitos.IsMatch(telefono);
}
