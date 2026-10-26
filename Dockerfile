# Build and run learn-Assist on Linux.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY learn-Assist.csproj ./
RUN dotnet restore ./learn-Assist.csproj

COPY . ./
RUN dotnet publish ./learn-Assist.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /out \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        libfreetype6 \
        libx11-6 \
        libx11-xcb1 \
        libxext6 \
        libxrender1 \
        libxcb1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out/ ./
RUN chmod +x ./learn-Assist

ENTRYPOINT ["./learn-Assist"]
