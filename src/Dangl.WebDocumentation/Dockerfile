#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-bionic AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-bionic AS build
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
ENTRYPOINT ["dotnet", "Dangl.WebDocumentation.dll"]