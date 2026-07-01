FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY BtlFullstack.sln ./
COPY src/Shared.Contracts/Shared.Contracts.csproj src/Shared.Contracts/
COPY src/IdentityReportService/IdentityReportService.csproj src/IdentityReportService/

RUN dotnet restore src/IdentityReportService/IdentityReportService.csproj

COPY . .
WORKDIR /src/src/IdentityReportService
RUN dotnet publish IdentityReportService.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IdentityReportService.dll"]
