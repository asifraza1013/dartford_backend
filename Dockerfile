# --- Build stage ---
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project file
COPY inflan_api.csproj .

# Restore dependencies
RUN dotnet restore inflan_api.csproj

# Copy everything else
COPY . .

# Restore and publish
RUN dotnet publish inflan_api.csproj -c Release -o /app/out --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/out .

# Create directories for uploads
RUN mkdir -p wwwroot/uploads wwwroot/campaignDocs

# Set environment variables for local development
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

# Run your app
ENTRYPOINT ["dotnet", "inflan_api.dll"]
