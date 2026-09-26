FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Marketplace.API/Marketplace.API.csproj"
RUN dotnet publish "src/Marketplace.API/Marketplace.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app

RUN apk update && apk upgrade --no-cache && apk add --no-cache icu-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .
RUN mkdir -p /app/uploads

EXPOSE 5001
ENTRYPOINT ["dotnet", "Marketplace.API.dll"]
