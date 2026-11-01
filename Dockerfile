# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore first so the package layer is cached between code changes.
COPY src/VeloStore/VeloStore.csproj src/VeloStore/
RUN dotnet restore src/VeloStore/VeloStore.csproj

COPY src/ src/
RUN dotnet publish src/VeloStore/VeloStore.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app .

# The aspnet image ships with a non-root "app" user.
USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "VeloStore.dll"]
