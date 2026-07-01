FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BtlFullstack.sln ./
COPY src/Shared.Contracts/Shared.Contracts.csproj src/Shared.Contracts/
COPY src/CirculationService/CirculationService.csproj src/CirculationService/

RUN dotnet restore src/CirculationService/CirculationService.csproj

COPY . .
WORKDIR /src/src/CirculationService
RUN dotnet publish CirculationService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CirculationService.dll"]
