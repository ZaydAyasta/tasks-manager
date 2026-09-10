FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Nakama.sln ./
COPY src/backend/Nakama.Api/Nakama.Api.csproj src/backend/Nakama.Api/
RUN dotnet restore src/backend/Nakama.Api/Nakama.Api.csproj

COPY src/backend/Nakama.Api/ src/backend/Nakama.Api/
RUN dotnet publish src/backend/Nakama.Api/Nakama.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Nakama.Api.dll"]
