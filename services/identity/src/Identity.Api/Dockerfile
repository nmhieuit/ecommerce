# Build context is the REPOSITORY ROOT, not this folder — the project needs the solution-level
# Directory.*.props and the shared ServiceDefaults/Identity projects it references (Principle VII):
#
#   docker build -f services/identity/src/Identity.Api/Dockerfile -t identity-api .
#
# Only this service's own sources are copied in. Nothing from another service enters the image,
# so there is no path to another service's code or configuration at runtime.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached independently of source edits.
# .editorconfig travels with the props files: it carries analyzer severities the build depends on
# (the CA1861 suppression scoped to generated EF migrations, for one), and a build that cannot see
# it fails on rules a local build passes.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY shared/Identity/Identity.csproj shared/Identity/
COPY services/identity/src/Identity.Api/Identity.Api.csproj services/identity/src/Identity.Api/
RUN dotnet restore services/identity/src/Identity.Api/Identity.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY shared/Identity/ shared/Identity/
COPY services/identity/src/Identity.Api/ services/identity/src/Identity.Api/
RUN dotnet publish services/identity/src/Identity.Api/Identity.Api.csproj \
    --configuration Release --no-restore --output /app/publish

# Three EF migration bundles, one per DbContext (Data/MigrationsHistoryTables.cs) — the one
# exception among this platform's services to "one context, one bundle" (services/parties' pattern):
# the user credential store and Duende's two stores are separate DbContexts sharing the "identity"
# database, so each needs its own bundle and its own `dotnet ef` invocation. Each bundle resolves its
# connection string the same way services/parties' does — via its own IDesignTimeDbContextFactory
# reading ConnectionStrings__IdentityDb from the environment (Data/*DbContextFactory.cs) — so no
# --connection argument is passed at run time, matching parties-migrate's convention.
FROM build AS migrations
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore \
    && dotnet dotnet-ef migrations bundle \
        --project services/identity/src/Identity.Api/Identity.Api.csproj \
        --context ApplicationIdentityDbContext \
        --configuration Release --no-build --force --output /app/efbundle-identity \
    && dotnet dotnet-ef migrations bundle \
        --project services/identity/src/Identity.Api/Identity.Api.csproj \
        --context ConfigurationDbContext \
        --configuration Release --no-build --force --output /app/efbundle-configuration \
    && dotnet dotnet-ef migrations bundle \
        --project services/identity/src/Identity.Api/Identity.Api.csproj \
        --context PersistedGrantDbContext \
        --configuration Release --no-build --force --output /app/efbundle-persistedgrant

# The ASP.NET image, not the base runtime one: bundles built from a web project declare
# Microsoft.AspNetCore.App as their framework and refuse to start without it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrations /app/efbundle-identity ./efbundle-identity
COPY --from=migrations /app/efbundle-configuration ./efbundle-configuration
COPY --from=migrations /app/efbundle-persistedgrant ./efbundle-persistedgrant
USER $APP_UID
# All three run against the same database; MigrationsHistoryTables.cs keeps each context's applied-
# migration record separate, so running them in any order is safe.
ENTRYPOINT ["/bin/sh", "-c", "./efbundle-identity && ./efbundle-configuration && ./efbundle-persistedgrant"]

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
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
