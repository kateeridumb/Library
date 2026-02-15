FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY LibraryMPT.sln .
COPY LibraryMPT.csproj .

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY . .

# Build and publish (excluding API project)
RUN dotnet publish LibraryMPT.csproj -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LibraryMPT.dll"]

