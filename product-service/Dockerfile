FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY product-service/Product-Service.csproj product-service/
RUN dotnet restore product-service/Product-Service.csproj

COPY product-service/ product-service/
RUN dotnet publish product-service/Product-Service.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

RUN chown -R "$APP_UID:0" /app

ENV ASPNETCORE_HTTP_PORTS=8081 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8081

USER $APP_UID

ENTRYPOINT ["dotnet", "Product-Service.dll"]
