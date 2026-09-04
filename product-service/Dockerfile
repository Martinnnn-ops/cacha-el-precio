FROM maven:3.9.16-eclipse-temurin-25-noble AS builder

WORKDIR /workspace

COPY pom.xml ./
COPY product-service/pom.xml product-service/pom.xml

RUN mvn -q -f product-service/pom.xml dependency:go-offline

COPY product-service/openapi.properties product-service/openapi.properties
COPY product-service/src product-service/src

RUN mvn -q -f product-service/pom.xml package -DskipTests

FROM eclipse-temurin:25-jre-noble

RUN groupadd --system product-service \
    && useradd --system --gid product-service --home-dir /app --shell /usr/sbin/nologin product-service \
    && mkdir -p /app/data \
    && chown -R product-service:product-service /app

WORKDIR /app

COPY --from=builder --chown=product-service:product-service \
    /workspace/product-service/target/product-service-0.1.0.jar \
    /app/product-service.jar

ENV PRODUCT_PORT=8081 \
    PRODUCT_GRPC_PORT=50051 \
    PRODUCT_DB_URL="jdbc:sqlite:/app/data/product.db?foreign_keys=on&busy_timeout=5000&journal_mode=WAL"

EXPOSE 8081 50051

VOLUME ["/app/data"]

USER product-service

ENTRYPOINT ["java", "-jar", "/app/product-service.jar"]
