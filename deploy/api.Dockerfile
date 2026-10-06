FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY backend/BizFlow.Domain/*.csproj backend/BizFlow.Domain/packages.lock.json backend/BizFlow.Domain/
COPY backend/BizFlow.Application/*.csproj backend/BizFlow.Application/packages.lock.json backend/BizFlow.Application/
COPY backend/BizFlow.Infrastructure/*.csproj backend/BizFlow.Infrastructure/packages.lock.json backend/BizFlow.Infrastructure/
COPY backend/BizFlow.Api/*.csproj backend/BizFlow.Api/packages.lock.json backend/BizFlow.Api/
RUN dotnet restore backend/BizFlow.Api/BizFlow.Api.csproj --locked-mode
COPY backend/ backend/
RUN dotnet publish backend/BizFlow.Api/BizFlow.Api.csproj --no-restore --configuration Release --output /out/api /p:UseAppHost=false

FROM build AS migration-build
RUN dotnet tool restore \
    && dotnet ef migrations bundle --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure --configuration Release --output /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f AS migrate
WORKDIR /app
COPY --from=migration-build /out/efbundle ./efbundle
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f AS api
WORKDIR /app
COPY --from=build /out/api/ ./
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "BizFlow.Api.dll"]
