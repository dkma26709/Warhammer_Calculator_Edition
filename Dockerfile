# syntax=docker/dockerfile:1

# ---- Build: publish the Blazor WebAssembly app -------------------------------
# The published output is static files that run in the browser, so this stage
# always runs on the build machine's own architecture, even when the image
# targets another one (e.g. building linux/arm64 on an amd64 CI runner).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore on its own layer so packages stay cached until the project file
# changes. Restore for Release: a plain restore resolves for Debug and pulls the
# hot-reload module into the published output.
COPY Warhammer/Warhammer.csproj Warhammer/
RUN dotnet restore Warhammer/Warhammer.csproj -p:Configuration=Release

COPY Warhammer/ Warhammer/
RUN dotnet publish Warhammer/Warhammer.csproj -c Release --no-restore -o /app/publish

# ---- Runtime: nginx serves the static site (there is no backend) -------------
FROM nginx:stable-alpine

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1/ || exit 1
