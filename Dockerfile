FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/DocManagement.Domain/DocManagement.Domain.csproj src/DocManagement.Domain/
COPY src/DocManagement.Application/DocManagement.Application.csproj src/DocManagement.Application/
COPY src/DocManagement.Infrastructure/DocManagement.Infrastructure.csproj src/DocManagement.Infrastructure/
COPY src/DocManagement.Api/DocManagement.Api.csproj src/DocManagement.Api/
RUN dotnet restore src/DocManagement.Api/DocManagement.Api.csproj
COPY src/ src/
RUN dotnet publish src/DocManagement.Api/DocManagement.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DocManagement.Api.dll"]
