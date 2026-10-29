# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
# The commit, for service.version in telemetry (.git is not in the build context).
ARG SOURCE_REVISION=
WORKDIR /src
COPY ["Headhunter.API/Headhunter.API.csproj", "Headhunter.API/"]
COPY ["Headhunter.Database/Headhunter.Database.csproj", "Headhunter.Database/"]
RUN dotnet restore "./Headhunter.API/Headhunter.API.csproj"
COPY . .
WORKDIR "/src/Headhunter.API"
RUN dotnet build "./Headhunter.API.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:DefineConstants=DOCKER_BUILD /p:SourceRevisionId=$SOURCE_REVISION

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG SOURCE_REVISION=
RUN dotnet publish "./Headhunter.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:DefineConstants=DOCKER_BUILD /p:SourceRevisionId=$SOURCE_REVISION

# EF Core migrations bundle: one executable that applies pending migrations, at /app/efbundle.
# Shipping it inside the image means the migrations a deploy runs always come from the same build as
# the app being deployed; the homelab migration Job (home/manifests/headhunter) runs it with
# ConnectionStrings__Headhunter set. Framework-dependent: it uses the .NET runtime in the base image.
# Building it creates the design-time context, which wants a connection string: a dummy.
FROM build AS migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.12
ENV PATH="$PATH:/root/.dotnet/tools"
ENV ConnectionStrings__Headhunter="Server=unused;Database=unused"
WORKDIR /src
RUN dotnet ef migrations bundle \
      --project Headhunter.Database --startup-project Headhunter.Database \
      --configuration Release --target-runtime linux-musl-x64 -o /app/efbundle

# The bundle's migration IDs, one per line: /app/migrations.txt. The migration Job and the app's wait
# for it compare them with the database's __EFMigrationsHistory. The build fails if the list is empty
# or not the YYYYMMDDHHMMSS_Name shape they rely on.
RUN dotnet ef migrations list --no-connect --prefix-output \
      --project Headhunter.Database --startup-project Headhunter.Database --configuration Release \
    | sed -n 's/^data:[[:space:]]*//p' > /app/migrations.txt \
 && test -s /app/migrations.txt \
 && ! grep -v -E '^[0-9]{14}_[A-Za-z0-9_]+$' /app/migrations.txt \
 && echo "Migrations in this build:" && cat /app/migrations.txt

# sqlcmd (Microsoft's go-sqlcmd), for the migration Job's checks and backup and the app's wait for it.
# One static binary, pinned by checksum: a new version needs the checksum updated.
FROM alpine:3.24 AS sqlcmd
ARG SQLCMD_VERSION=v1.10.0
RUN wget -q -O /tmp/sqlcmd.tar.bz2 \
      "https://github.com/microsoft/go-sqlcmd/releases/download/$SQLCMD_VERSION/sqlcmd-linux-amd64.tar.bz2" \
 && echo "92516d98c63d99b0994de5b61350c91f6915f9b76f139a59039fbcb225c2e987  /tmp/sqlcmd.tar.bz2" | sha256sum -c - \
 && tar -xjf /tmp/sqlcmd.tar.bz2 -C /usr/local/bin sqlcmd

# This stage is used to build the client project
FROM node:26.13.0-alpine AS angular
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
COPY --from=migrations /app/efbundle /app/efbundle
COPY --from=migrations /app/migrations.txt /app/migrations.txt
# The bundle is a single-file app that unpacks itself before it runs, into $HOME by default, which a
# read-only root filesystem doesn't allow. The migration Job has /tmp writable.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
COPY --from=sqlcmd /usr/local/bin/sqlcmd /usr/local/bin/sqlcmd
ENTRYPOINT ["dotnet", "Headhunter.API.dll"]