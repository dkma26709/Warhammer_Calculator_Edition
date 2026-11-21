# syntax=docker/dockerfile:1

# The WAR army data (units, costs, items, lores) is not in the repo: it is
# exported from warhammer-armies-revamped at the commit in tools/war-data/pin by
# tools/war-data/fetch.py, the same script local builds and CI use.

# ---- WAR data: run fetch.py at the pin --------------------------------------
# The script downloads the upstream tarball and a checksum-verified typst
# itself. This layer reruns whenever tools/war-data/ (the pin or the script)
# changes. The output is platform-independent JSON, so this stage runs on the
# build machine's own architecture.
FROM --platform=$BUILDPLATFORM python:3.13-slim AS war-data
WORKDIR /src
COPY tools/war-data/ tools/war-data/
RUN python tools/war-data/fetch.py

# ---- Build: add the export, test, publish the Blazor WebAssembly app ---------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore on its own layer so packages stay cached until a project file
# changes. Restore for Release: a plain restore resolves for Debug and pulls the
# hot-reload module into the published output.
COPY Warhammer/Warhammer.csproj Warhammer/
COPY Warhammer.Tests/Warhammer.Tests.csproj Warhammer.Tests/
RUN dotnet restore Warhammer.Tests/Warhammer.Tests.csproj -p:Configuration=Release

COPY Warhammer/ Warhammer/
COPY Warhammer.Tests/ Warhammer.Tests/
# The export joins core-rules.json and troop-types.json, this repo's own files;
# the build's data guard (Warhammer.csproj) checks its stamp against the pin.
COPY tools/war-data/pin tools/war-data/pin
COPY --from=war-data /src/Warhammer/wwwroot/data/war/ Warhammer/wwwroot/data/war/
COPY --from=war-data /src/tools/war-data/.exported tools/war-data/.exported

# The data is always the pin's, which test.yml already tests, so pull-request
# builds pass RUN_TESTS=false and skip the suite (they never push). Pushes keep
# it, so a failing suite never reaches GHCR.
ARG RUN_TESTS=true
RUN if [ "$RUN_TESTS" != false ]; then \
        dotnet test Warhammer.Tests/Warhammer.Tests.csproj -c Release --no-restore; \
    fi
# nginx serves the .gz files; the .br ones (no brotli module) would only add size.
RUN dotnet publish Warhammer/Warhammer.csproj -c Release --no-restore -o /app/publish \
 && find /app/publish/wwwroot -name '*.br' -delete

# ---- Runtime: nginx serves the static site (there is no backend) -------------
FROM nginx:stable-alpine

# The data is the pin's: the org.opencontainers.image.revision label docker.yml
# sets names the commit whose tools/war-data/pin it was exported at.
LABEL org.opencontainers.image.title="Warhammer Calculator Edition"

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1/ || exit 1
