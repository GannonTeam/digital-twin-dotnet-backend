# =======================
# BUILD STAGE
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and all projects
COPY ../DigitalTwinDotNetBackend.sln ./
COPY ../DigitalTwinDotNetBackend/ ./DigitalTwinDotNetBackend/

# Restore dependencies
RUN dotnet restore DigitalTwinDotNetBackend.sln

# Publish only ApiHost (entrypoint)
RUN dotnet publish DigitalTwinDotNetBackend/ApiHost/ApiHost.csproj -c Release -o /app/publish

# =======================
# RUNTIME STAGE
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:5251

# Copy published output
COPY --from=build /app/publish .

EXPOSE 5251

ENTRYPOINT ["dotnet", "ApiHost.dll"]