# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AiStockTradeApp.Api/AiStockTradeApp.Api.csproj", "AiStockTradeApp.Api/"]
COPY ["AiStockTradeApp.Entities/AiStockTradeApp.Entities.csproj", "AiStockTradeApp.Entities/"]
COPY ["AiStockTradeApp.DataAccess/AiStockTradeApp.DataAccess.csproj", "AiStockTradeApp.DataAccess/"]
COPY ["AiStockTradeApp.Services/AiStockTradeApp.Services.csproj", "AiStockTradeApp.Services/"]
RUN dotnet restore "./AiStockTradeApp.Api/AiStockTradeApp.Api.csproj"
COPY . .
WORKDIR "/src/AiStockTradeApp.Api"
RUN dotnet build "./AiStockTradeApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AiStockTradeApp.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Pre-download Playwright browsers into /ms-playwright so runtime has no outbound dependency for installs
RUN apt-get update \ 
	&& apt-get install -y --no-install-recommends ca-certificates curl gnupg \ 
	&& bash /root/.nuget/packages/microsoft.playwright/1.54.0/build/Playwright.sh install \ 
	&& rm -rf /var/lib/apt/lists/*

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
USER root
# Install Playwright runtime OS dependencies required by Firefox/Chromium in container
RUN apt-get update \ 
	&& apt-get install -y --no-install-recommends \
	   libasound2 \
	   libatk-bridge2.0-0 libatk1.0-0 \
	   libcups2 libdbus-1-3 libdrm2 libgbm1 \
	   libglib2.0-0 libnss3 \
	   libx11-6 libx11-xcb1 libxcb1 libxcomposite1 libxdamage1 libxext6 libxfixes3 libxkbcommon0 libxrandr2 \
	   libpango-1.0-0 \
	   libwayland-client0 libwayland-egl1 libwayland-server0 \
	   libxss1 libxshmfence1 \
	   xdg-utils libu2f-udev libvulkan1 \
	   fonts-liberation ca-certificates curl \ 
	&& rm -rf /var/lib/apt/lists/*

# Copy pre-downloaded browsers
COPY --from=publish /ms-playwright /ms-playwright
USER $APP_UID
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AiStockTradeApp.Api.dll"]