FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY apps/api/PulseBoard.Api.csproj apps/api/
RUN dotnet restore apps/api/PulseBoard.Api.csproj

COPY apps/api apps/api
RUN dotnet publish apps/api/PulseBoard.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PulseBoard.Api.dll"]
