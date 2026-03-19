FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/Billing.Domain/Billing.Domain.csproj Billing.Domain/
COPY src/Billing.Application/Billing.Application.csproj Billing.Application/
COPY src/Billing.Infrastructure/Billing.Infrastructure.csproj Billing.Infrastructure/
COPY src/Billing.Web/Billing.Web.csproj Billing.Web/
RUN dotnet restore Billing.Web/Billing.Web.csproj

COPY src/ .
RUN dotnet publish Billing.Web/Billing.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Billing.Web.dll"]
