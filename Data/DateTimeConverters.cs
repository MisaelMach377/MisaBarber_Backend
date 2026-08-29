using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace misabarber.Data;

// Ver el comentario en MisaBarberContext.ConfigureConventions: esta app
// guarda horas "de pared" (la barbería opera en una sola zona horaria), no
// instantes UTC. Desde Npgsql 6, escribir un DateTime con Kind=Unspecified
// en una columna "timestamp with time zone" (el mapeo default de Npgsql
// para DateTime) tira exactamente el error que salió: "Cannot write
// DateTime with Kind=Unspecified... only UTC is supported". Y al revés,
// una columna "timestamp without time zone" rechaza Kind=Utc. Como en el
// código conviven DateTime.UtcNow (Kind=Utc), DateTime.Today (Kind=Local) y
// DateTimes deserializados del JSON del front (Kind=Unspecified), la forma
// de no perseguir esto propiedad por propiedad es normalizar el Kind acá,
// en un solo lugar, para TODO DateTime del modelo.
public class UnspecifiedDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UnspecifiedDateTimeConverter() : base(
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified))
    {
    }
}

public class UnspecifiedNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UnspecifiedNullableDateTimeConverter() : base(
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v)
    {
    }
}
