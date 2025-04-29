# Use the official .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy the project files into the container
COPY *.sln .
COPY *.csproj ./HeatMapAPI/
# Add other projects if you have them

# Restore dependencies for all projects
RUN dotnet restore

# Copy the rest of the source code
COPY . . # Adjusted to copy all files from the current folder

# Build and publish the main application
WORKDIR /source/HeatMapAPI
RUN dotnet publish -c Release -o /app/publish --no-restore

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