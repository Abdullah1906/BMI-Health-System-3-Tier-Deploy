FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the csproj file using its relative path from the root context
COPY src/ScalingDemo.Api/ScalingDemo.Api.csproj src/ScalingDemo.Api/
RUN dotnet restore src/ScalingDemo.Api/ScalingDemo.Api.csproj

# Copy all source files
COPY . .
RUN dotnet publish src/ScalingDemo.Api/ScalingDemo.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "ScalingDemo.Api.dll"]
