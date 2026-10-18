FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY gateway/Gateway.csproj gateway/
RUN dotnet restore gateway/Gateway.csproj

COPY gateway/ gateway/
RUN dotnet publish gateway/Gateway.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=build /app/publish .
# La topologia de la mensajeria viaja dentro de la imagen (T11): es la misma
# que lee tools/crear-mensajeria.py, asi el panel y el script no se contradicen.
COPY infra/mensajeria/topologia.json /app/topologia.json

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TOPOLOGIA_RUTA=/app/topologia.json

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Gateway.dll"]
