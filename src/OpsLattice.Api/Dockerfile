# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from the project files only. This layer is reused until a project
# file or package version changes, so ordinary code edits don't re-download
# NuGet packages.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OpsLattice.Api/OpsLattice.Api.csproj src/OpsLattice.Api/
COPY src/OpsLattice.Agents/OpsLattice.Agents.csproj src/OpsLattice.Agents/
COPY src/OpsLattice.Scenarios.Airport/OpsLattice.Scenarios.Airport.csproj src/OpsLattice.Scenarios.Airport/
COPY src/OpsLattice.Simulator/OpsLattice.Simulator.csproj src/OpsLattice.Simulator/

# The cache mount keeps downloaded packages between builds, including
# --no-cache builds; only "docker builder prune" clears it.
# --disable-parallel downloads one package at a time, which copes better
# with slow or unreliable connections.
RUN --mount=type=cache,id=opslattice-nuget,target=/root/.nuget/packages \
    dotnet restore src/OpsLattice.Api/OpsLattice.Api.csproj --disable-parallel

COPY . .

RUN --mount=type=cache,id=opslattice-nuget,target=/root/.nuget/packages \
    dotnet publish src/OpsLattice.Api/OpsLattice.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "OpsLattice.Api.dll"]
