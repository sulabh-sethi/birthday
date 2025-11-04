FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./src/Congrats.Worker/Congrats.Worker.csproj \
    && dotnet publish ./src/Congrats.Worker/Congrats.Worker.csproj -c Release -o /out /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "Congrats.Worker.dll"]
