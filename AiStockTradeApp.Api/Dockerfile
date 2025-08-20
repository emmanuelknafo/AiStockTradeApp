# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root

# Install required dependencies for Playwright
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install Node.js (required for Playwright)
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs  

RUN node --version
RUN npm install -g npm@latest
RUN npm --version

# Use a shared path for Playwright browsers so runtime (non-root) can access them
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ARG PLAYWRIGHT_VERSION=1.55.0
# Install global Playwright CLI and browsers with OS deps into the shared path
RUN mkdir -p /ms-playwright \
    && npm install -g "playwright@${PLAYWRIGHT_VERSION}" \
    && playwright install --with-deps \
    && playwright --version \
    && chmod -R a+rX /ms-playwright


USER $APP_UID

# Set working directory
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

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
# Ensure app files are owned by the non-root user and Playwright's embedded Node is executable
USER root
RUN chown -R $APP_UID:$APP_UID /app \
    && chown -R $APP_UID:$APP_UID /ms-playwright \
    && chmod -R a+rX /ms-playwright \
    && if [ -d "/app/.playwright" ]; then chmod -R a+rx /app/.playwright; fi
USER $APP_UID
ENTRYPOINT ["dotnet", "AiStockTradeApp.Api.dll"]