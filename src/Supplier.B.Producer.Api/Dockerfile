FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Supplier.B.Producer.Api/Supplier.B.Producer.Api.csproj", "src/Supplier.B.Producer.Api/"]
RUN dotnet restore "./src/Supplier.B.Producer.Api/Supplier.B.Producer.Api.csproj"
COPY . .
WORKDIR "/src/src/Supplier.B.Producer.Api"
RUN dotnet build "./Supplier.B.Producer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Supplier.B.Producer.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Supplier.B.Producer.Api.dll"]
