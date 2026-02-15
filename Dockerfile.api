FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY LibraryMPT.sln .

# Copy main project file
COPY LibraryMPT.csproj .

# Copy API project file
COPY LibraryMPT.Api/LibraryMPT.Api.csproj ./LibraryMPT.Api/

# Restore dependencies (this will restore both projects)
RUN dotnet restore LibraryMPT.sln

# Copy everything else
COPY . .

# Build and publish API project
WORKDIR /src/LibraryMPT.Api
RUN dotnet publish -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LibraryMPT.Api.dll"]

