FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# COPY DigitalTwinDotNetBackend.csproj ./
# RUN dotnet restore DigitalTwinDotNetBackend.csproj

COPY . .

RUN dotnet restore DigitalTwinDotNetBackend.sln

# RUN dotnet publish DigitalTwinDotNetBackend.csproj -c Release -o /app/publish /p:UseAppHost=false
RUN dotnet publish DigitalTwinDotNetBackend/ApiHost/ApiHost.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:5251

COPY --from=build /app/publish ./

EXPOSE 5251
ENTRYPOINT ["dotnet", "ApiHost.dll"]
