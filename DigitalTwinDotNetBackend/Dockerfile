FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DigitalTwinDotNetBackend.csproj ./
RUN dotnet restore DigitalTwinDotNetBackend.csproj

COPY . ./

RUN dotnet publish DigitalTwinDotNetBackend.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5251

COPY --from=build /app/publish ./

EXPOSE 5251
ENTRYPOINT ["dotnet", "DigitalTwinDotNetBackend.dll"]
