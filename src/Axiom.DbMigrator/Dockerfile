FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# NuGet 包缓存挂载：源码变更击穿 COPY 层后，restore/publish 仍复用本地包缓存，无需重新下载。
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages     dotnet restore "src/Axiom.DbMigrator/Axiom.DbMigrator.csproj"
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages     dotnet publish "src/Axiom.DbMigrator/Axiom.DbMigrator.csproj" -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Axiom.DbMigrator.dll"]
