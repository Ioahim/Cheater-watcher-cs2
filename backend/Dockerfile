# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CheaterWatcher.slnx ./
COPY src/CheaterWatcher.Api/*.csproj ./src/CheaterWatcher.Api/
COPY tests/CheaterWatcher.Tests/*.csproj ./tests/CheaterWatcher.Tests/
RUN dotnet restore src/CheaterWatcher.Api/CheaterWatcher.Api.csproj
COPY src/CheaterWatcher.Api/ ./src/CheaterWatcher.Api/
COPY tests/ ./tests/
RUN dotnet publish src/CheaterWatcher.Api/CheaterWatcher.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:5089
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5089
USER app
VOLUME ["/app/data", "/app/demos"]
ENTRYPOINT ["dotnet", "CheaterWatcher.Api.dll"]
