FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BtlFullstack.sln ./
COPY src/Shared.Contracts/Shared.Contracts.csproj src/Shared.Contracts/
COPY src/CatalogService/CatalogService.csproj src/CatalogService/

RUN dotnet restore src/CatalogService/CatalogService.csproj

COPY . .
WORKDIR /src/src/CatalogService
RUN dotnet publish CatalogService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CatalogService.dll"]
