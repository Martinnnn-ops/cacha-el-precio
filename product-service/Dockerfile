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

RUN mkdir -p /app/data \
    && chown -R "$APP_UID:0" /app

ENV ASPNETCORE_HTTP_PORTS=8081 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Sqlite="Data Source=/app/data/product.db"

EXPOSE 8081
VOLUME ["/app/data"]

USER $APP_UID

ENTRYPOINT ["dotnet", "Product-Service.dll"]
