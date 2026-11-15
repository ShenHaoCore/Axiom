FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Axiom.DbMigrator/Axiom.DbMigrator.csproj"
RUN dotnet publish "src/Axiom.DbMigrator/Axiom.DbMigrator.csproj" -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Axiom.DbMigrator.dll"]
