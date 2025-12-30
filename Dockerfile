# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY src/JellySearch/JellySearch.csproj ./JellySearch/
RUN dotnet restore ./JellySearch/JellySearch.csproj

# Copy source code and build
COPY src/JellySearch/ ./JellySearch/
WORKDIR /src/JellySearch
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

ENV JELLYFIN_URL=http://jellyfin:8096 \
    JELLYFIN_CONFIG_DIR=/config \
    MEILI_URL=http://meilisearch:7700

COPY --from=build /app/publish /app

RUN chown 1000:100 /app -R

USER 1000:100

EXPOSE 5000

WORKDIR /app
ENTRYPOINT ["dotnet", "jellysearch.dll"]
