FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish PVZS/PVZS.csproj -c Release -o /app /p:PublishSingleFile=false /p:PublishSelfContained=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:1729 \
    PVZ_SERVER_DATA_DIR=/data
EXPOSE 1729
ENTRYPOINT ["dotnet", "PVZS.dll"]
