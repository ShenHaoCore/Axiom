FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Axiom.AuthHub/Axiom.AuthHub.csproj"
RUN dotnet publish "src/Axiom.AuthHub/Axiom.AuthHub.csproj" -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Axiom.AuthHub.dll"]
