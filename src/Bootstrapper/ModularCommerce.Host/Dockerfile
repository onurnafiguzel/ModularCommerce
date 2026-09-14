# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY --parents src/**/*.csproj Directory.Packages.props Directory.Build.props ./
RUN dotnet restore src/Bootstrapper/ModularCommerce.Host/ModularCommerce.Host.csproj

COPY . .
RUN dotnet publish src/Bootstrapper/ModularCommerce.Host/ModularCommerce.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ModularCommerce.Host.dll"]
