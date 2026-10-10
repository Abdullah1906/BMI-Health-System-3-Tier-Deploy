FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the .csproj changes.
COPY ScalingDemo.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish ScalingDemo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# The .NET 8 images ship a non-root 'app' user; port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "ScalingDemo.Api.dll"]
