FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем .sln и все .csproj файлы
COPY ["WeigthApiService.sln", "."]
COPY ["WeigthApiService.Api/WeigthApiService.Api.csproj", "WeigthApiService.Api/"]
COPY ["WeigthApiService.Core/WeigthApiService.Core.csproj", "WeigthApiService.Core/"]
COPY ["WeigthApiService.Infrastructure/WeigthApiService.Infrastructure.csproj", "WeigthApiService.Infrastructure/"]
COPY ["WeightMeasurement.Worker/WeightMeasurement.Worker.csproj", "WeightMeasurement.Worker/"]

# Восстанавливаем зависимости
RUN dotnet restore "WeightMeasurement.Worker/WeightMeasurement.Worker.csproj"

# Копируем остальной код
COPY . .

WORKDIR "/src/WeightMeasurement.Worker"
RUN dotnet build "WeightMeasurement.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "WeightMeasurement.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WeightMeasurement.Worker.dll"]