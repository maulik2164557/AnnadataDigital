# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["AgriDirect.csproj", "./"]
RUN dotnet restore "AgriDirect.csproj"

# Copy remaining source code
COPY . .

# Build and publish release
RUN dotnet publish "AgriDirect.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Copy existing database if present
COPY agridirect.db* ./

# Ensure write permissions for SQLite database and uploads directory
RUN mkdir -p /app/wwwroot/uploads/avatars /app/wwwroot/uploads/products && \
    chmod -R 777 /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

ENTRYPOINT ["dotnet", "AgriDirect.dll"]
