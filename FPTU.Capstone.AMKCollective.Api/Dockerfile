# Stage 1: Build stage with SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG CONFIGURATION=Release
WORKDIR /src

# Copy solution file first for better caching
COPY ["../FPTU.Capstone.AMKCollective.sln", "./"]

# Copy all project files to restore dependencies (better layer caching)
COPY ["FPTU.Capstone.AMKCollective.API.csproj", "FPTU.Capstone.AMKCollective.Api/"]
COPY ["../FPTU.Capstone.AMKCollective.Application/FPTU.Capstone.AMKCollective.Application.csproj", "FPTU.Capstone.AMKCollective.Application/"]
COPY ["../FPTU.Capstone.AMKCollective.Domain/FPTU.Capstone.AMKCollective.Domain.csproj", "FPTU.Capstone.AMKCollective.Domain/"]
COPY ["../FPTU.Capstone.AMKCollective.Infrastructure/FPTU.Capstone.AMKCollective.Infrastructure.csproj", "FPTU.Capstone.AMKCollective.Infrastructure/"]

# Restore all dependencies (this layer will be cached if csproj files don't change)
RUN dotnet restore "FPTU.Capstone.AMKCollective.Api/FPTU.Capstone.AMKCollective.API.csproj"

# Copy all source code
COPY . "FPTU.Capstone.AMKCollective.Api/"
COPY ../FPTU.Capstone.AMKCollective.Application/ "FPTU.Capstone.AMKCollective.Application/"
COPY ../FPTU.Capstone.AMKCollective.Domain/ "FPTU.Capstone.AMKCollective.Domain/"
COPY ../FPTU.Capstone.AMKCollective.Infrastructure/ "FPTU.Capstone.AMKCollective.Infrastructure/"

# Build and publish
WORKDIR "/src/FPTU.Capstone.AMKCollective.Api"
RUN dotnet build "FPTU.Capstone.AMKCollective.API.csproj" -c ${CONFIGURATION} --no-restore
RUN dotnet publish "FPTU.Capstone.AMKCollective.API.csproj" -c ${CONFIGURATION} -o /app/publish --no-build --no-restore

# Stage 2: Runtime stage with minimal image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Set environment variables
ENV ASPNETCORE_URLS="http://+:80" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Expose port
EXPOSE 80

# Metadata
LABEL org.opencontainers.image.title="FPTU.Capstone.AMKCollective.API" \
      org.opencontainers.image.description="FPTU Capstone AMKCollective ASP.NET Core Web API" \
      org.opencontainers.image.vendor="FPTU" \
      maintainer="fptu-team@example.com"

# Copy published files
COPY --from=build /app/publish ./

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

ENTRYPOINT ["dotnet", "FPTU.Capstone.AMKCollective.API.dll"]
