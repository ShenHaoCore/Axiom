FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# NuGet 包缓存挂载：源码变更击穿 COPY 层后，restore/publish 仍复用本地包缓存，无需重新下载。
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages     dotnet restore "src/Axiom.ReferenceData.Api/Axiom.ReferenceData.Api.csproj"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages     dotnet publish "src/Axiom.ReferenceData.Api/Axiom.ReferenceData.Api.csproj" -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# curl 供 compose healthcheck 探测 /health/ready。
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*
USER $APP_UID
EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "Axiom.ReferenceData.Api.dll"]
