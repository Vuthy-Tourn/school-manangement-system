# Multi-stage build for Nexora Nexora API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/Nexora.Domain/Nexora.Domain.csproj", "src/Nexora.Domain/"]
COPY ["src/Nexora.Application/Nexora.Application.csproj", "src/Nexora.Application/"]
COPY ["src/Nexora.Infrastructure/Nexora.Infrastructure.csproj", "src/Nexora.Infrastructure/"]
COPY ["src/Nexora.Api/Nexora.Api.csproj", "src/Nexora.Api/"]

RUN dotnet restore "src/Nexora.Api/Nexora.Api.csproj"

# Copy full source and build
COPY . .
WORKDIR "/src/src/Nexora.Api"
RUN dotnet build "Nexora.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Nexora.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
USER $APP_UID

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nexora.Api.dll"]
