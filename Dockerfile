FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105
RUN apt update && apt install -y libgdiplus libc6-dev
COPY . /app
WORKDIR /app
RUN dotnet restore
EXPOSE 5000
ENTRYPOINT [ "dotnet", "run" ]
