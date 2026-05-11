# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY ["src/FurniSpace.API/FurniSpace.API.csproj", "src/FurniSpace.API/"]
COPY ["src/FurniSpace.Application/FurniSpace.Application.csproj", "src/FurniSpace.Application/"]
COPY ["src/FurniSpace.Domain/FurniSpace.Domain.csproj", "src/FurniSpace.Domain/"]
COPY ["src/FurniSpace.Infrastructure/FurniSpace.Infrastructure.csproj", "src/FurniSpace.Infrastructure/"]
COPY ["src/FurniSpace.Shared/FurniSpace.Shared.csproj", "src/FurniSpace.Shared/"]
RUN dotnet restore "src/FurniSpace.API/FurniSpace.API.csproj"

# Stage 2: Build
FROM restore AS build
COPY . .
RUN dotnet build "src/FurniSpace.API/FurniSpace.API.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "src/FurniSpace.API/FurniSpace.API.csproj" \
    -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 4: Runtime (minimal image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FurniSpace.API.dll"]