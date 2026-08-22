# Build context is the REPOSITORY ROOT, not this folder — the project needs the solution-level
# Directory.*.props and the shared projects it references (Principle VII):
#
#   docker build -f services/baskets/src/Baskets.Api/Dockerfile -t baskets-api .
#
# Only this service's own sources are copied in. Nothing from another service enters the image,
# so there is no path to another service's code or configuration at runtime (spec FR-005).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached independently of source edits.
# .editorconfig travels with the props files: it carries analyzer severities the build depends on
# (the CA1861 suppression scoped to generated EF migrations, for one), and a build that cannot see
# it fails on rules a local build passes.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY shared/Tenancy/Tenancy.csproj shared/Tenancy/
# Referenced since 011-consumer-contract-tests, for the BasketCheckedOut payload this service
# constructs at checkout (shared/EventContracts).
COPY shared/EventContracts/EventContracts.csproj shared/EventContracts/
COPY services/baskets/src/Baskets.Api/Baskets.Api.csproj services/baskets/src/Baskets.Api/
RUN dotnet restore services/baskets/src/Baskets.Api/Baskets.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY shared/Tenancy/ shared/Tenancy/
COPY shared/EventContracts/ shared/EventContracts/
COPY services/baskets/src/Baskets.Api/ services/baskets/src/Baskets.Api/
RUN dotnet publish services/baskets/src/Baskets.Api/Baskets.Api.csproj \
    --configuration Release --no-restore --output /app/publish

# The schema, as its own runnable artefact. Migrations run as a step that must succeed before the
# service starts, rather than at service startup — startup migration races when more than one
# instance starts, and these services are meant to be horizontally scalable
# (005-one-command-local-run research.md Decision 4).
#
# A bundle rather than an SDK image: Microsoft.EntityFrameworkCore.Design is referenced with
# PrivateAssets="all", so `dotnet ef` exists at build time only. The bundle carries the migrations
# and nothing else.
FROM build AS migrations
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore     && dotnet dotnet-ef migrations bundle         --project services/baskets/src/Baskets.Api/Baskets.Api.csproj         --configuration Release --no-build --force --output /app/efbundle

# The ASP.NET image, not the base runtime one: the bundle is produced from a web project, so it
# declares Microsoft.AspNetCore.App as its framework and refuses to start without it — "No
# frameworks were found", which reads like a broken image rather than a missing shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrations /app/efbundle .
USER $APP_UID
# Takes --connection; the stack passes the same database this service's own configuration names.
ENTRYPOINT ["./efbundle"]

# `final` stays last so it remains the default build target: an image built with no --target is
# still the service, not the migrator.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The runtime image ships no HTTP client, so the stack's health gate would have nothing to probe
# with and would fall back to a TCP connect — which reports a service healthy while its database is
# unreachable, the exact silent success the gate exists to catch
# (005-one-command-local-run research.md Decision 9).
#
# Installed as root, deliberately before the switch below: the container still runs as the non-root
# user Principle VI requires.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Constitution Principle VI (Secure by Default): never run as root. $APP_UID is the non-root
# user the .NET base images provide.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Baskets.Api.dll"]
