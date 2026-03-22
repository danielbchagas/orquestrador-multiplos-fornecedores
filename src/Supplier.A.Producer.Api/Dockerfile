FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Supplier.A.Producer.Api/Supplier.A.Producer.Api.csproj", "src/Supplier.A.Producer.Api/"]
RUN dotnet restore "./src/Supplier.A.Producer.Api/Supplier.A.Producer.Api.csproj"
COPY . .
WORKDIR "/src/src/Supplier.A.Producer.Api"
RUN dotnet build "./Supplier.A.Producer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Supplier.A.Producer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Supplier.A.Producer.Api.dll"]
