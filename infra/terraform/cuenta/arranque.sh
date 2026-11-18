#!/bin/bash
# Deja la maquina lista para "docker compose up". No despliega nada todavia:
# eso necesita el .env con secretos, que no viaja en el repositorio.
dnf -y update
dnf -y install docker git
systemctl enable --now docker
usermod -aG docker ec2-user
mkdir -p /usr/local/lib/docker/cli-plugins
curl -fsSL --retry 4 --retry-all-errors --connect-timeout 15 \
  https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64 \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
# buildx: el paquete de Docker de la AMI trae 0.12.1, y el plugin de compose
# nuevo exige 0.17 o mas para construir imagenes. Se pone una version fija en
# /usr/local/lib, que tiene precedencia sobre la de /usr/libexec.
curl -fsSL --retry 4 --retry-all-errors --connect-timeout 15 \
  https://github.com/docker/buildx/releases/download/v0.17.1/buildx-v0.17.1.linux-amd64 \
  -o /usr/local/lib/docker/cli-plugins/docker-buildx
chmod +x /usr/local/lib/docker/cli-plugins/docker-buildx
mkdir -p /opt/cacha-el-precio && chown ec2-user:ec2-user /opt/cacha-el-precio
# El agente SSM viene en la AMI, pero pide credenciales al arrancar. Se
# reinicia al final para que tome las del perfil ya asociado.
systemctl restart amazon-ssm-agent 2>/dev/null || true
touch /var/log/cacha-listo
