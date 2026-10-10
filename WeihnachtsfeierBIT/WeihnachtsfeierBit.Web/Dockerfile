FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet restore WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/WeihnachtsfeierBit.Web.csproj

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/ WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/ WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet publish WeihnachtsfeierBIT/WeihnachtsfeierBit.Web/WeihnachtsfeierBit.Web.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeihnachtsfeierBit.Web.dll"]