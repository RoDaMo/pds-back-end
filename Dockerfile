#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG PGUSER
ARG PGHOST
ARG PGDATABASE
ARG PGPASSWORD
ARG PGPORT
ARG CLOUD_ID
ARG ELASTIC_API_KEY
ARG ELASTIC_USER
ARG ELASTIC_PASSWORD

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["pds-back-end.csproj", "."]
RUN dotnet restore "./pds-back-end.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "pds-back-end.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "pds-back-end.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD ASPNETCORE_URLS=http://*:$PORT dotnet pds-back-end.dll
