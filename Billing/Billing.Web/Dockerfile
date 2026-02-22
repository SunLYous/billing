FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Packages.props", "."]

COPY ["Billing.Application/Billing.Application.csproj", "Billing.Application/"]
COPY ["Billing.Domain/Billing.Domain.csproj", "Billing.Domain/"]
COPY ["Billing.Infrastructure/Billing.Infrastructure.csproj", "Billing.Infrastructure/"]
COPY ["Billing.Web/Billing.Web.csproj", "Billing.Web/"]

RUN dotnet restore "Billing.Web/Billing.Web.csproj"

COPY . .

WORKDIR "/src/Billing.Web"
RUN dotnet build "./Billing.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Billing.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Billing.Web.dll"]