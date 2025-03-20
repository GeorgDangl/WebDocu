#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Dangl.WebDocumentation/Dangl.WebDocumentation.csproj", "src/Dangl.WebDocumentation/"]
RUN dotnet restore "src/Dangl.WebDocumentation/Dangl.WebDocumentation.csproj"
COPY . .
WORKDIR "/src/src/Dangl.WebDocumentation"
RUN dotnet build "Dangl.WebDocumentation.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dangl.WebDocumentation.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV ASPNETCORE_URLS=http://*:80
ENTRYPOINT ["dotnet", "Dangl.WebDocumentation.dll"]