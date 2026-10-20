FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["src/ScalingDemo.Api/ScalingDemo.Api.csproj", "src/ScalingDemo.Api/"]
RUN dotnet restore "src/ScalingDemo.Api/ScalingDemo.Api.csproj"

# Copy remaining source code
COPY . .
WORKDIR "/src/src/ScalingDemo.Api"
RUN dotnet publish "ScalingDemo.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "ScalingDemo.Api.dll"]
