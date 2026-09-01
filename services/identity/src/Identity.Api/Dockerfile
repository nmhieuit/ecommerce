# Build context is the REPOSITORY ROOT, not this folder — the project needs the solution-level
# Directory.*.props and the shared ServiceDefaults project it references (Principle VII):
#
#   docker build -f services/identity/src/Identity.Api/Dockerfile -t identity-api .
#
# Only this service's own sources are copied in. Nothing from another service enters the image,
# so there is no path to another service's code or configuration at runtime.
#
# No `migrator` build stage yet, unlike services/parties: this Dockerfile mirrors
# services/gateway/src/Gateway.Api/Dockerfile because at Setup/Foundational time (tasks.md T008)
# this service has no EF store to bundle migrations for. User Story 1 (tasks.md T020-T021) adds the
# Duende configuration/operational store and the user credential store — a `migrations`/`migrator`
# stage (see services/parties/src/Parties.Api/Dockerfile for the pattern) is added alongside them,
# not before.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached independently of source edits.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY services/identity/src/Identity.Api/Identity.Api.csproj services/identity/src/Identity.Api/
RUN dotnet restore services/identity/src/Identity.Api/Identity.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY services/identity/src/Identity.Api/ services/identity/src/Identity.Api/
RUN dotnet publish services/identity/src/Identity.Api/Identity.Api.csproj \
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
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
