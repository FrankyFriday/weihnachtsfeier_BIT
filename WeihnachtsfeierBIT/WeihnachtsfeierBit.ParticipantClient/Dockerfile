FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/*.csproj WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet restore WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/WeihnachtsfeierBit.ParticipantClient.csproj

COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/ WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/ WeihnachtsfeierBIT/WeihnachtsfeierBit.Framework/
COPY WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/ WeihnachtsfeierBIT/WeihnachtsfeierBit.ServiceDefaults/
RUN dotnet publish WeihnachtsfeierBIT/WeihnachtsfeierBit.ParticipantClient/WeihnachtsfeierBit.ParticipantClient.csproj \
    --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WeihnachtsfeierBit.ParticipantClient.dll"]
