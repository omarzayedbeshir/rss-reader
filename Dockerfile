FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p Data
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
CMD ["dotnet", "RssReader.dll"]
