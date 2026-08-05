# syntax=docker/dockerfile:1

# Build à lancer depuis la racine du dépôt (contexte = ".") car Cerebro.Server référence
# Cerebro.Shared par chemin relatif :
#   docker build -f src/Cerebro.Server/Dockerfile -t cerebro-server .

# --- Étape 1 : compilation du dashboard TypeScript --------------------------------------
# Séparée du SDK .NET pour ne jamais dépendre de Node dans l'étape de publish (voir la même
# logique dans Cerebro.Server.csproj / BuildDashboardTypeScript).
FROM node:22-alpine AS dashboard-build
WORKDIR /src

COPY src/Cerebro.Server/package.json src/Cerebro.Server/package-lock.json ./
RUN npm ci

COPY src/Cerebro.Server/tsconfig.json ./
COPY src/Cerebro.Server/scripts ./scripts
COPY src/Cerebro.Server/wwwroot/ts ./wwwroot/ts
RUN npm run build

# --- Étape 2 : restore + publish .NET ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Copie des seuls .csproj d'abord : le cache Docker de la couche restore survit tant que les
# dépendances ne changent pas, même si le code source change.
COPY src/Cerebro.Server/Cerebro.Server.csproj src/Cerebro.Server/
COPY src/Cerebro.Shared/Cerebro.Shared.csproj src/Cerebro.Shared/
RUN dotnet restore src/Cerebro.Server/Cerebro.Server.csproj

COPY src/Cerebro.Server/ src/Cerebro.Server/
COPY src/Cerebro.Shared/ src/Cerebro.Shared/

# Le dashboard est déjà compilé (étape 1) : on copie les artefacts directement plutôt que de
# laisser BuildDashboardTypeScript tourner ici (pas de node_modules dans cette étape, la cible
# se contenterait de passer avec un avertissement, ce qui est correct mais inutile).
COPY --from=dashboard-build /src/wwwroot/js src/Cerebro.Server/wwwroot/js
COPY --from=dashboard-build /src/wwwroot/lib/signalr src/Cerebro.Server/wwwroot/lib/signalr

RUN dotnet publish src/Cerebro.Server/Cerebro.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Étape 3 : image finale, runtime seul -------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Utilisateur non-root : le conteneur ne tourne jamais en root en prod. L'image de base fournit
# déjà un utilisateur "app" (UID/GID 1654) prévu pour cet usage - pas besoin d'en recréer un.
RUN mkdir -p /app/db /app/screenshots \
    && chown -R app:app /app

COPY --from=dotnet-build --chown=app:app /app/publish .

USER app

# db/ (SQLite) et screenshots/ (captures candidats) doivent survivre aux redéploiements -
# à monter en volume nommé ou bind mount en prod.
VOLUME ["/app/db", "/app/screenshots"]

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Cerebro.Server.dll"]
