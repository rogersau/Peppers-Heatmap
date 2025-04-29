# Use the official .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files first to leverage Docker layer caching for dependencies
COPY HeatMapAPI.sln .
COPY HeatMapAPI.csproj .

# Restore dependencies for the solution
# This restores packages for all projects defined in the solution
RUN dotnet restore HeatMapAPI.sln

# Copy the rest of the application source code
# Copy directories needed for the build
COPY Controllers ./Controllers/
COPY Converters ./Converters/
COPY Helpers ./Helpers/
COPY Maps ./Maps/
COPY Models ./Models/
COPY Properties ./Properties/
COPY Services ./Services/
COPY wwwroot ./wwwroot/
COPY appsettings.json .
COPY appsettings.Development.json .
# Add any other necessary files/folders like Program.cs if not implicitly copied by '.' before
COPY Program.cs .

# Build and publish the main application
# No need to change WORKDIR, publish from /source
RUN dotnet publish HeatMapAPI.csproj -c Release -o /app/publish --no-restore

# Use the official .NET runtime image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose the port the application runs on (default is 8080 for ASP.NET Core in containers)
EXPOSE 8080

# Set environment variables (optional, can be overridden at runtime)
ENV ENABLE_SWAGGER=false

# Define the entry point for the container
ENTRYPOINT ["dotnet", "HeatMapAPI.dll"]