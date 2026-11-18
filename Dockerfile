# syntax=docker/dockerfile:1

# The WAR army data (units, costs, items, lores) is exported at build time from
# warhammer-armies-revamped. WAR_REF picks what it is exported from: a branch,
# a tag or a full commit SHA. The default is the commit Warhammer/wwwroot/data/war
# was last exported from, so the default image ships the data the repo tests;
# move both together.
ARG WAR_REPO=https://github.com/Anders1222/warhammer-armies-revamped.git
ARG WAR_REF=b7322e8a33691a975867dfef78250656da377dbd

# ---- WAR data: run export.py at WAR_REF ---------------------------------------
# ADD resolves WAR_REF to a commit and caches by it: while the ref points at a
# commit this machine (or the CI cache) has exported before, the export is
# reused, not run again. The output is platform-independent JSON, so this stage
# runs on the build machine's own architecture.
FROM --platform=$BUILDPLATFORM python:3.13-slim AS war-data
ARG BUILDARCH
ARG TYPST_VERSION=0.15.1

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl xz-utils \
 && rm -rf /var/lib/apt/lists/*
RUN case "$BUILDARCH" in \
        amd64) arch=x86_64 ;; \
        arm64) arch=aarch64 ;; \
        *) echo "no typst build for $BUILDARCH" >&2; exit 1 ;; \
    esac \
 && curl -fsSL "https://github.com/typst/typst/releases/download/v${TYPST_VERSION}/typst-${arch}-unknown-linux-musl.tar.xz" \
  | tar -xJ -C /usr/local/bin --strip-components=1 "typst-${arch}-unknown-linux-musl/typst"
# Typst's embedded fonts only, so the export cannot vary with the machine.
ENV TYPST_IGNORE_SYSTEM_FONTS=true

COPY docker/stub_lfs_images.py /usr/local/bin/
ARG WAR_REPO
ARG WAR_REF
ADD ${WAR_REPO}#${WAR_REF} /war-src
WORKDIR /war-src
# The checkout has no Git LFS objects (the cover art); see stub_lfs_images.py.
RUN python /usr/local/bin/stub_lfs_images.py . \
 && python export.py --out /war-data

# ---- Build: swap in the export, test, publish the Blazor WebAssembly app ------
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

# The export writes every file of the WAR folder but core-rules.json and
# troop-types.json, which are this repo's own. Clear the rest first, so the
# folder is exactly what the export wrote at WAR_REF.
RUN find Warhammer/wwwroot/data/war -type f \
        ! -name core-rules.json ! -name troop-types.json -delete
COPY --from=war-data /war-data/ Warhammer/wwwroot/data/war/

# The data now comes from WAR_REF, not from the commit the tests ran against in
# CI, so the image only ships data the app passes its tests with.
RUN dotnet test Warhammer.Tests/Warhammer.Tests.csproj -c Release --no-restore
RUN dotnet publish Warhammer/Warhammer.csproj -c Release --no-restore -o /app/publish

# ---- Runtime: nginx serves the static site (there is no backend) -------------
FROM nginx:stable-alpine

ARG WAR_REF
LABEL org.opencontainers.image.title="Warhammer Calculator Edition" \
      warhammer.war-data.ref="${WAR_REF}"

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1/ || exit 1
