FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105 AS builder
WORKDIR /build
COPY *.csproj ./
RUN dotnet restore
COPY . /build
RUN dotnet publish -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2.3
RUN apt update && apt install -y libgdiplus libc6-dev
WORKDIR /app
COPY --from=builder /app/ ./
EXPOSE 5000
ENTRYPOINT [ "dotnet", "chordgenerator.dll" ]
