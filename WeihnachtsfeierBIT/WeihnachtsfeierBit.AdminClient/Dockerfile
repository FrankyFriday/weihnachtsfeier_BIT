FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet restore WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/WeihnachtsfeierBit.AdminClient.csproj

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/ WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/ WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/ WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet publish WeihnachtsfeierBIT/WeihnachtsfeierBit.AdminClient/WeihnachtsfeierBit.AdminClient.csproj \
    --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeihnachtsfeierBit.AdminClient.dll"]