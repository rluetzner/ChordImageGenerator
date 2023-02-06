FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105 AS builder
COPY . /app
WORKDIR /app
RUN dotnet restore
RUN dotnet publish -c Release

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.3
RUN apt update && apt install -y libgdiplus libc6-dev
WORKDIR /app
COPY --from=builder /app/bin/Release/netcoreapp2.2/publish ./
EXPOSE 5000
ENTRYPOINT [ "dotnet", "chordgenerator.dll" ]
