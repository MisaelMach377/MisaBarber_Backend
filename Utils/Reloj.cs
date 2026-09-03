namespace misabarber.Utils;

// La app entera guarda y compara horas "de pared" de Perú, sin zona
// horaria (ver el comentario en Data/DateTimeConverters.cs) -- eso solo
// funciona si "la hora de ahora" se calcula siempre igual, sin importar
// en qué máquina corre el backend. DateTime.Now / DateTime.Today dan la
// hora del RELOJ DEL SISTEMA OPERATIVO donde corre el proceso: en tu
// compu (Windows, zona horaria de Lima) coincide con la hora de Perú por
// pura casualidad, pero en Railway (ver Dockerfile) el contenedor corre
// en UTC -- ahí DateTime.Now está 5 horas ADELANTADO de la hora real de
// Lima, lo que hacía que GetDisponibilidad descartara como "ya pasadas"
// horas del día de hoy que en realidad todavía no llegaban (y, cerca de
// la medianoche, que GetAll mostrara "0 citas hoy" con el día ya
// cambiado en el servidor mientras en Perú seguía siendo el día anterior).
// Perú es UTC-5 fijo todo el año (no tiene horario de verano), así que
// restarle 5 horas a UtcNow -- que SIEMPRE es UTC sin importar dónde
// corra el proceso -- alcanza para tener la hora de Perú de forma
// confiable en cualquier entorno, sin necesidad de TimeZoneInfo.
public static class Reloj
{
    public static DateTime AhoraPeru() => DateTime.UtcNow.AddHours(-5);

    public static DateTime HoyPeru() => AhoraPeru().Date;
}
