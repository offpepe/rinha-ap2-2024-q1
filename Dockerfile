﻿FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Rinha2024.Dotnet.csproj", "./"]
RUN dotnet restore "Rinha2024.Dotnet.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Rinha2024.Dotnet.csproj" -c Release -o /app/build

FROM build AS publish
RUN apt update
RUN apt install -y clang zlib1g-dev
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./Rinha2024.Dotnet"]
