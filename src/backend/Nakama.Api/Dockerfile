FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Nakama.sln ./
COPY src/backend/Nakama.Api/Nakama.Api.csproj src/backend/Nakama.Api/
COPY src/backend/Nakama.Provisioning/Nakama.Provisioning.csproj src/backend/Nakama.Provisioning/
RUN dotnet restore src/backend/Nakama.Provisioning/Nakama.Provisioning.csproj

COPY src/backend/Nakama.Api/ src/backend/Nakama.Api/
COPY src/backend/Nakama.Provisioning/ src/backend/Nakama.Provisioning/
RUN dotnet publish src/backend/Nakama.Api/Nakama.Api.csproj --configuration Release --no-restore --output /app/publish
RUN dotnet publish src/backend/Nakama.Provisioning/Nakama.Provisioning.csproj --configuration Release --no-restore --output /app/provisioning

# Railway runs pre-deploy commands from the final image.  Ship an EF migration
# bundle there so schema changes can be applied before the API is made live
# without placing the SDK, source tree, or database credentials in production.
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.0 \
    && /tools/dotnet-ef migrations bundle \
        --project src/backend/Nakama.Api/Nakama.Api.csproj \
        --startup-project src/backend/Nakama.Api/Nakama.Api.csproj \
        --configuration Release \
        --no-build \
        --output /app/efbundle \
        --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=build /app/efbundle ./efbundle
COPY --from=build /app/provisioning ./provisioning
RUN chmod +x ./efbundle
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Nakama.Api.dll"]
