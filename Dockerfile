FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем .sln и все .csproj файлы для корректного dotnet restore
# Это более надежно, если структура проекта сложная или есть зависимости между проектами вне основного пути
COPY ["WeigthApiService.sln", "."]
COPY ["WeigthApiService.Api/WeigthApiService.Api.csproj", "WeigthApiService.Api/"]
COPY ["WeigthApiService.Core/WeightApiService.Core.csproj", "WeightApiService.Core/"]
COPY ["WeightApiService.Infrastructure/WeightApiService.Infrastructure.csproj", "WeightApiService.Infrastructure/"]
COPY ["WeightMeasurement.Worker/WeightMeasurement.Worker.csproj", "WeightMeasurement.Worker/"]
# Добавь другие .csproj, если они есть и нужны

# Восстанавливаем зависимости для всего решения или конкретного проекта
# Для воркера достаточно его проекта, если все зависимости правильно указаны
RUN dotnet restore "WeightMeasurement.Worker/WeightMeasurement.Worker.csproj"
# Или, если проще, для всего солюшена:
# RUN dotnet restore "WeigthApiService.sln"

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