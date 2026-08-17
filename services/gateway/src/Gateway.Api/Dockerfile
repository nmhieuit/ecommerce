# Build context is the REPOSITORY ROOT, not this folder — the project needs the solution-level
# Directory.*.props and the shared ServiceDefaults project it references (Principle VII):
#
#   docker build -f services/gateway/src/Gateway.Api/Dockerfile -t gateway-api .
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
COPY services/gateway/src/Gateway.Api/Gateway.Api.csproj services/gateway/src/Gateway.Api/
RUN dotnet restore services/gateway/src/Gateway.Api/Gateway.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY services/gateway/src/Gateway.Api/ services/gateway/src/Gateway.Api/
RUN dotnet publish services/gateway/src/Gateway.Api/Gateway.Api.csproj \
    --configuration Release --no-restore --output /app/publish

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
ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
