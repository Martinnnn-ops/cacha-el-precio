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

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Gateway.dll"]
