# Use .NET 9.0 preview SDK image
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /app

# Copy and restore project
COPY ./*.csproj ./
RUN dotnet restore

# Copy all and publish
COPY . ./
RUN dotnet publish -c Release -o out

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "jokessvc.dll"]
