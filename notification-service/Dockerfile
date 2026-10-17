FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY notification-service/Notification-Service.csproj notification-service/
RUN dotnet restore notification-service/Notification-Service.csproj

COPY notification-service/ notification-service/
RUN dotnet publish notification-service/Notification-Service.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

RUN chown -R "$APP_UID:0" /app

ENV ASPNETCORE_HTTP_PORTS=8082 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8082

USER $APP_UID

ENTRYPOINT ["dotnet", "Notification-Service.dll"]
