# Multi-stage build for Nexora EduManage API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/EduManage.Domain/EduManage.Domain.csproj", "src/EduManage.Domain/"]
COPY ["src/EduManage.Application/EduManage.Application.csproj", "src/EduManage.Application/"]
COPY ["src/EduManage.Infrastructure/EduManage.Infrastructure.csproj", "src/EduManage.Infrastructure/"]
COPY ["src/EduManage.Api/EduManage.Api.csproj", "src/EduManage.Api/"]

RUN dotnet restore "src/EduManage.Api/EduManage.Api.csproj"

# Copy full source and build
COPY . .
WORKDIR "/src/src/EduManage.Api"
RUN dotnet build "EduManage.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EduManage.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user for security
USER $APP_UID

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduManage.Api.dll"]
