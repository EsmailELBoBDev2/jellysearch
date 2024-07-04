FROM mcr.microsoft.com/dotnet/aspnet:8.0

ENV JELLYFIN_URL=http://jellyfin:8096 \
    JELLYFIN_CONFIG_DIR=/config \
    MEILI_URL=http://meilisearch:7700

COPY app /app

RUN chown 1000:100 /app -R

USER 1000:100

WORKDIR /app
ENTRYPOINT ["dotnet", "jellysearch.dll"]
