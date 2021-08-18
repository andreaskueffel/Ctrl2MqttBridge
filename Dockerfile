#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG newversion=1.0.0
FROM mcr.microsoft.com/dotnet/runtime AS base
ARG newversion
WORKDIR /app
EXPOSE 51883

FROM mcr.microsoft.com/dotnet/sdk AS build
ARG newversion
WORKDIR /src
COPY ["src/Ctrl2MqttBridge/Ctrl2MqttBridge.csproj", ""]
RUN dotnet restore "./Ctrl2MqttBridge.csproj"
COPY src/. .
WORKDIR "/src/Ctrl2MqttBridge/."
RUN dotnet build "Ctrl2MqttBridge.csproj" -c Release -f net50 -p:Version=$newversion -o /app/build

FROM build AS publish
ARG newversion
RUN dotnet publish "Ctrl2MqttBridge.csproj" -c Release -f net50 -p:Version=$newversion -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ctrl2MqttBridge.dll"]
