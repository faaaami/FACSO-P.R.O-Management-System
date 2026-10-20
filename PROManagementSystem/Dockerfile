# Build:  docker build -f PROManagementSystem/Dockerfile -t fasco-pro-api .
# (run from the repository root)

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Tesseract OCR for document extraction. The Linux binaries bundled with the
# NuGet package need a newer OpenSSL than this image has, so the
# distribution's libraries are used instead (see scripts/link-linux-ocr.sh).
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libtesseract5 \
    && (apt-get install -y --no-install-recommends liblept5 || apt-get install -y --no-install-recommends libleptonica6) \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PROManagementSystem/PROManagementSystem.Api.csproj", "PROManagementSystem/"]
COPY ["PROManagementSystem.Application/PROManagementSystem.Application.csproj", "PROManagementSystem.Application/"]
COPY ["PROManagementSystem.Domain/PROManagementSystem.Domain.csproj", "PROManagementSystem.Domain/"]
COPY ["PROManagementSystem.Infrastructure/PROManagementSystem.Infrastructure.csproj", "PROManagementSystem.Infrastructure/"]
RUN dotnet restore "./PROManagementSystem/PROManagementSystem.Api.csproj"
COPY . .
WORKDIR "/src/PROManagementSystem"
RUN dotnet publish "./PROManagementSystem.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY scripts/link-linux-ocr.sh /tmp/link-linux-ocr.sh
RUN sh /tmp/link-linux-ocr.sh /app && rm /tmp/link-linux-ocr.sh \
    && mkdir -p /app/storage && chown -R $APP_UID /app/storage
USER $APP_UID
# Uploaded documents. Mount a persistent, backed-up volume here and set
# Storage__RootPath=/app/storage.
VOLUME ["/app/storage"]
ENTRYPOINT ["dotnet", "PROManagementSystem.Api.dll"]
