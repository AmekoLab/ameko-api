FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG CONFIGURATION=Release
WORKDIR /src

# Copy only the API project file to leverage layer caching
COPY ["FPTU.Capstone.AMKCollective.API.csproj", "./"]

RUN dotnet restore "./FPTU.Capstone.AMKCollective.API.csproj"

# Copy everything else and publish
COPY . .
RUN dotnet publish "FPTU.Capstone.AMKCollective.API.csproj" -c ${CONFIGURATION} -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Expose ports and listen on port 80
ENV ASPNETCORE_URLS="http://+:80"
EXPOSE 80

# Metadata useful for CI/CD
LABEL org.opencontainers.image.title="FPTU.Capstone.AMKCollective.API"
LABEL org.opencontainers.image.description="FPTU Capstone AMKCollective ASP.NET Core Web API"

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "FPTU.Capstone.AMKCollective.API.dll"]
