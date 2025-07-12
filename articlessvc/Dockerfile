# Use .NET 9.0 preview SDK image
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /app

COPY ./*.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "articlessvc.dll"]
