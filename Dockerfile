FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
# Keep global.json's feature band and latestPatch policy unchanged.
COPY global.json UniversityRegistration.csproj ./
RUN dotnet restore UniversityRegistration.csproj
COPY . .
RUN dotnet publish UniversityRegistration.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "UniversityRegistration.dll"]
