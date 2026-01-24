# GekkoLab Docker Image
# Multi-stage build for .NET 9 ARM64 (Raspberry Pi)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY GekkoLab/*.csproj ./GekkoLab/
RUN dotnet restore ./GekkoLab/GekkoLab.csproj

# Copy source code
COPY GekkoLab/ ./GekkoLab/

# Build and publish
WORKDIR /src/GekkoLab
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS runtime
WORKDIR /app


# Create data directory for SQLite database
RUN mkdir -p /app/gekkodata

# Copy published app
COPY --from=build /app/publish .

# Expose port
EXPOSE 5050

# Set environment variables
ENV ASPNETCORE_URLS=http://*:5050
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "GekkoLab.dll"]
