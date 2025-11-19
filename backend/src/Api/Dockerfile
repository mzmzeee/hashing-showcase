# Use an official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/Api/Api.csproj", "src/Api/"]
COPY ["src/Data/Data.csproj", "src/Data/"]
COPY ["src/Logic/Logic.csproj", "src/Logic/"]
RUN dotnet restore "src/Api/Api.csproj" /p:UseNet9Only=true

# Copy everything else and build
COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build /p:UseNet9Only=true

FROM build AS publish
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseNet9Only=true -f net9.0

# Use a smaller runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
