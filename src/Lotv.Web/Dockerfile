# Build context must be the solution root (docker build -f src/Lotv.Web/Dockerfile .)

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore — csproj files first for layer caching
COPY ["src/Lotv.Core/Lotv.Core.csproj", "src/Lotv.Core/"]
COPY ["src/Lotv.Web/Lotv.Web.csproj",   "src/Lotv.Web/"]
RUN dotnet restore "src/Lotv.Web/Lotv.Web.csproj"

# Build + publish
COPY src/ src/
WORKDIR "/src/src/Lotv.Web"
RUN dotnet publish "Lotv.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore

# ── Stage 2: Serve with nginx ─────────────────────────────────────────────────
FROM nginx:alpine AS final

# Copy compiled Blazor WASM static assets
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# SPA routing + caching config
COPY src/Lotv.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
