# syntax=docker/dockerfile:1

# The WAR army data (units, costs, items, lores) is exported at build time from
# warhammer-armies-revamped.
#   WAR_PIN  the commit Warhammer/wwwroot/data/war was last exported from. Move
#            it with the data: a build without WAR_REF fails while they differ.
#   WAR_REF  optional: export from this branch, tag or full commit SHA instead.
ARG WAR_REPO=https://github.com/Anders1222/warhammer-armies-revamped.git
ARG WAR_PIN=b7322e8a33691a975867dfef78250656da377dbd
ARG WAR_REF

# ---- WAR data: run export.py at WAR_REF, or at the pin ------------------------
# ADD resolves the ref to a commit and the export is cached against it, so
# building the same ref again reuses the export (until the base image or the
# build cache changes). The output is platform-independent JSON, so this stage
# runs on the build machine's own architecture.
FROM --platform=$BUILDPLATFORM python:3.13-slim AS war-data
ARG BUILDARCH
ARG TYPST_VERSION=0.15.1
ARG TYPST_SHA256_AMD64=a6d077d0a95eed5a2eba715b2dae06be954f624ccbf85758a03f389ded33118c
ARG TYPST_SHA256_ARM64=5aa8d74a3d906e60ea12a66ac2f37f8eef1b14cbad7182a745e393a10c23dcee

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl xz-utils \
 && rm -rf /var/lib/apt/lists/*
RUN case "$BUILDARCH" in \
        amd64) arch=x86_64;  sum=$TYPST_SHA256_AMD64 ;; \
        arm64) arch=aarch64; sum=$TYPST_SHA256_ARM64 ;; \
        *) echo "no typst build for $BUILDARCH" >&2; exit 1 ;; \
    esac \
 && curl -fsSL -o /tmp/typst.tar.xz \
        "https://github.com/typst/typst/releases/download/v${TYPST_VERSION}/typst-${arch}-unknown-linux-musl.tar.xz" \
 && echo "$sum  /tmp/typst.tar.xz" | sha256sum -c - \
 && tar -xJf /tmp/typst.tar.xz -C /usr/local/bin --strip-components=1 \
        "typst-${arch}-unknown-linux-musl/typst" \
 && rm /tmp/typst.tar.xz
# Typst's embedded fonts only, so the export cannot vary with the machine.
ENV TYPST_IGNORE_SYSTEM_FONTS=true

COPY docker/stub_lfs_images.py /usr/local/bin/
ARG WAR_REPO
ARG WAR_PIN
ARG WAR_REF
ADD ${WAR_REPO}#${WAR_REF:-${WAR_PIN}} /war-src
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
# troop-types.json, which are this repo's own. Without WAR_REF the export is of
# the pin, which must be exactly what data/war holds (line endings aside, for a
# Windows checkout), so a refresh that forgets the pin fails here instead of
# shipping the pin's older data. Then the folder becomes exactly the export.
ARG WAR_PIN
ARG WAR_REF
COPY --from=war-data /war-data/ /war-data/
RUN if [ -z "$WAR_REF" ]; then \
        diff -rq --strip-trailing-cr -x core-rules.json -x troop-types.json \
            Warhammer/wwwroot/data/war /war-data \
        || { echo "Warhammer/wwwroot/data/war is not the export at WAR_PIN" \
                  "($WAR_PIN): move the pin in the Dockerfile with the data." >&2; \
             exit 1; }; \
    fi \
 && find Warhammer/wwwroot/data/war -type f \
        ! -name core-rules.json ! -name troop-types.json -delete \
 && cp -a /war-data/. Warhammer/wwwroot/data/war/

# The data may come from WAR_REF, not from what CI tested, so the image only
# ships data the app passes its tests with. RUN_TESTS=false skips the suite,
# but only for the pin's data, which the diff above proved is the committed
# data that test.yml already tests; pull-request builds pass it, since they
# never push. Data from WAR_REF is always tested.
ARG RUN_TESTS=true
RUN if [ "$RUN_TESTS" != false ] || [ -n "$WAR_REF" ]; then \
        dotnet test Warhammer.Tests/Warhammer.Tests.csproj -c Release --no-restore; \
    fi
# nginx serves the .gz files; the .br ones (no brotli module) would only add size.
RUN dotnet publish Warhammer/Warhammer.csproj -c Release --no-restore -o /app/publish \
 && find /app/publish/wwwroot -name '*.br' -delete

# ---- Runtime: nginx serves the static site (there is no backend) -------------
FROM nginx:stable-alpine

ARG WAR_PIN
ARG WAR_REF
LABEL org.opencontainers.image.title="Warhammer Calculator Edition" \
      warhammer.war-data.ref="${WAR_REF:-${WAR_PIN}}"

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://127.0.0.1/ || exit 1
