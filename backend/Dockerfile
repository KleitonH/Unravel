FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Unravel.API/Unravel.API.csproj", "src/Unravel.API/"]
COPY ["src/Unravel.Application/Unravel.Application.csproj", "src/Unravel.Application/"]
COPY ["src/Unravel.Domain/Unravel.Domain.csproj", "src/Unravel.Domain/"]
COPY ["src/Unravel.Infrastructure/Unravel.Infrastructure.csproj", "src/Unravel.Infrastructure/"]
RUN dotnet restore "src/Unravel.API/Unravel.API.csproj"
COPY . .
RUN dotnet build "src/Unravel.API/Unravel.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/Unravel.API/Unravel.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Unravel.API.dll"]
