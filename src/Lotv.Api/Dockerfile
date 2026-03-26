# Build context must be the solution root (docker build -f src/Lotv.Api/Dockerfile .)

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore — copy only csproj files first to maximise layer caching
COPY ["src/Lotv.Core/Lotv.Core.csproj",   "src/Lotv.Core/"]
COPY ["src/Lotv.Api/Lotv.Api.csproj",     "src/Lotv.Api/"]
RUN dotnet restore "src/Lotv.Api/Lotv.Api.csproj"

# Build
COPY src/ src/
WORKDIR "/src/src/Lotv.Api"
RUN dotnet build "Lotv.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ── Stage 2: Publish ──────────────────────────────────────────────────────────
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Lotv.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ── Stage 3: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080

# Run as non-root
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Lotv.Api.dll"]
