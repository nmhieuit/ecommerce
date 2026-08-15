# Build context is the REPOSITORY ROOT, not this folder — the project needs the solution-level
# Directory.*.props and the shared ServiceDefaults project it references (Principle VII):
#
#   docker build -f services/orders/src/Orders.Api/Dockerfile -t orders-api .
#
# Only this service's own sources are copied in. Nothing from another service enters the image,
# so there is no path to another service's code or configuration at runtime (spec FR-005).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so restore is cached independently of source edits.
COPY Directory.Build.props Directory.Packages.props ./
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY services/orders/src/Orders.Api/Orders.Api.csproj services/orders/src/Orders.Api/
RUN dotnet restore services/orders/src/Orders.Api/Orders.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY services/orders/src/Orders.Api/ services/orders/src/Orders.Api/
RUN dotnet publish services/orders/src/Orders.Api/Orders.Api.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Constitution Principle VI (Secure by Default): never run as root. $APP_UID is the non-root
# user the .NET base images provide.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
