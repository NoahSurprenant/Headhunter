# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base

RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Headhunter.API/Headhunter.API.csproj", "Headhunter.API/"]
COPY ["Headhunter.Database/Headhunter.Database.csproj", "Headhunter.Database/"]
RUN dotnet restore "./Headhunter.API/Headhunter.API.csproj"
COPY . .
WORKDIR "/src/Headhunter.API"
RUN dotnet build "./Headhunter.API.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:DefineConstants=DOCKER_BUILD

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Headhunter.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:DefineConstants=DOCKER_BUILD

# This stage is used to build the client project
FROM node:20.11.1-alpine AS angular
WORKDIR /usr/angular
COPY Headhunter.Client/package.json ./
COPY Headhunter.Client/package-lock.json ./
RUN npm install
COPY Headhunter.Client ./
RUN npm run build

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=angular /usr/angular/dist/headhunter.client/browser wwwroot
ENTRYPOINT ["dotnet", "Headhunter.API.dll"]