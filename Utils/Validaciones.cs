using System.Text.RegularExpressions;

namespace misabarber.Utils;

public static class Validaciones
{
    private static readonly Regex SoloDigitos = new(@"^\d{1,15}$", RegexOptions.Compiled);

    // Solo dígitos, máximo 15 (tope E.164). Perú se guarda sin prefijo (9xxxxxxxx);
    // otros países llevan el prefijo pegado (ej. 573001234567). Se
    // valida acá además de en el front — el front nunca alcanza solo, porque
    // cualquiera le puede pegar directo a la API con Postman/curl y saltarse
    // el input del formulario.
    public static bool TelefonoValido(string? telefono) =>
        string.IsNullOrWhiteSpace(telefono) || SoloDigitos.IsMatch(telefono);
}
