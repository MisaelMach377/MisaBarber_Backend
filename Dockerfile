# Railway no soporta build automático (Nixpacks/Railpack) para .NET todavía
# -- necesita un Dockerfile sí o sí. Build en 2 etapas: la primera tiene el
# SDK completo (pesado, solo para compilar), la segunda es la imagen final
# que de verdad corre, con solo el runtime de ASP.NET -- mucho más liviana.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia solo el .csproj primero para cachear el "restore" -- si después
# solo cambia el código (no las dependencias), Docker reusa esta capa en
# vez de volver a bajar todos los paquetes de NuGet en cada build.
COPY misabarber.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "misabarber.dll"]
