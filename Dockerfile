# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore and publish
RUN dotnet publish dartford_api.csproj -c Release -o /app/out

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/out .

# Bind to the correct port for Render
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Run your app
ENTRYPOINT ["dotnet", "dartford_api.dll"]
