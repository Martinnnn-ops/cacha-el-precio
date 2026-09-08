# ---------------------------------------------------------------------------
# Imagen del BFF. Mismo patron que product-service: se construye en dos etapas
# para que la imagen final NO lleve Maven ni el codigo fuente, solo el jar y un
# JRE. Son ~600 MB de diferencia, y menos cosas dentro es menos que parchar.
# ---------------------------------------------------------------------------
FROM maven:3.9.16-eclipse-temurin-25-noble AS builder

WORKDIR /workspace

# Los pom van primero y solos: mientras no cambien, Docker reutiliza la capa de
# dependencias y no vuelve a bajarlas en cada build.
COPY pom.xml ./
COPY gateway/pom.xml gateway/pom.xml

RUN mvn -q -f gateway/pom.xml dependency:go-offline

COPY gateway/src gateway/src

RUN mvn -q -f gateway/pom.xml package -DskipTests

FROM eclipse-temurin:25-jre-noble

# Usuario propio, sin shell: si alguien logra ejecutar algo dentro del
# contenedor, no lo hace como root.
RUN groupadd --system gateway \
    && useradd --system --gid gateway --home-dir /app --shell /usr/sbin/nologin gateway \
    && mkdir -p /app \
    && chown -R gateway:gateway /app

WORKDIR /app

COPY --from=builder --chown=gateway:gateway \
    /workspace/gateway/target/gateway-0.1.0.jar \
    /app/gateway.jar

ENV GATEWAY_PORT=8080

EXPOSE 8080

USER gateway

ENTRYPOINT ["java", "-jar", "/app/gateway.jar"]
