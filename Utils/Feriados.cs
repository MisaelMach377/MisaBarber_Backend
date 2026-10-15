namespace misabarber.Utils;

// Feriados nacionales de Perú, para que no cuenten como "día hábil" al
// calcular el vencimiento de una reserva (ver ReservasController).
// Son los feriados de FECHA FIJA más Jueves y Viernes Santo (que cambian cada
// año con la Pascua, por eso se calculan). NO incluye los "feriados puente" o
// días no laborables que el Gobierno decreta año a año: esos hay que sumarlos
// a mano en ExtraordinariosPorDecreto cuando salgan.
public static class Feriados
{
    // (mes, día)
    private static readonly (int Mes, int Dia)[] Fijos =
    {
        (1, 1),   // Año Nuevo
        (5, 1),   // Día del Trabajo
        (6, 7),   // Batalla de Arica y Día de la Bandera
        (6, 29),  // San Pedro y San Pablo
        (7, 23),  // Día de la Fuerza Aérea
        (7, 28),  // Fiestas Patrias
        (7, 29),  // Fiestas Patrias
        (8, 6),   // Batalla de Junín
        (8, 30),  // Santa Rosa de Lima
        (10, 8),  // Combate de Angamos
        (10, 31), // Día de las Iglesias Evangélicas
        (11, 1),  // Todos los Santos
        (12, 8),  // Inmaculada Concepción
        (12, 9),  // Batalla de Ayacucho
        (12, 25), // Navidad
    };

    // Fechas puntuales decretadas por el Gobierno (feriados puente, etc.).
    public static readonly HashSet<DateTime> ExtraordinariosPorDecreto = new();

    public static bool EsFeriado(DateTime fecha)
    {
        var d = fecha.Date;
        if (Fijos.Any(f => f.Mes == d.Month && f.Dia == d.Day)) return true;
        if (ExtraordinariosPorDecreto.Contains(d)) return true;

        var pascua = DomingoDePascua(d.Year);
        return d == pascua.AddDays(-3) || d == pascua.AddDays(-2); // Jueves y Viernes Santo
    }

    // Algoritmo de Meeus/Jones/Butcher (calendario gregoriano).
    private static DateTime DomingoDePascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(anio, mes, dia);
    }
}
