FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY StudyGuard.Server.csproj ./

RUN dotnet restore StudyGuard.Server.csproj

COPY . .

RUN dotnet publish StudyGuard.Server.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 1000

ENTRYPOINT ["dotnet", "StudyGuard.Server.dll"]