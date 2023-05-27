#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG PGUSER
ARG PGHOST
ARG PGDATABASE
ARG PGPASSWORD
ARG PGPORT
ARG ELASTIC_KEY
ARG ELASTIC_URL
ARG REDIS_URL
ARG AUTH_KEY
ARG AUTH_ISSUER
ARG AUTH_AUDIENCE
ARG AWS_ACCESS_KEY_ID
ARG AWS_SECRET_ACCESS_KEY
# ARG CRYPT_KEY

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["PlayOffsApi.csproj", "."]
RUN dotnet restore "./PlayOffsApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "PlayOffsApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PlayOffsApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
CMD ASPNETCORE_URLS=http://*:$PORT dotnet PlayOffsApi.dll
