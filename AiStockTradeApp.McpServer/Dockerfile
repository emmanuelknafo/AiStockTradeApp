# syntax=docker/dockerfile:1

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Copy project files and shared dependencies
COPY ["AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj", "/source/AiStockTradeApp.McpServer/"]
COPY ["AiStockTradeApp.Entities/AiStockTradeApp.Entities.csproj", "/source/AiStockTradeApp.Entities/"]

WORKDIR /source/AiStockTradeApp.McpServer

# Restore dependencies
RUN dotnet restore "AiStockTradeApp.McpServer.csproj"

# Copy source code
COPY . /source/

# Build and publish with architecture-specific runtime
ARG TARGETARCH
RUN case "$TARGETARCH" in \
      "amd64") RID="linux-musl-x64" ;; \
      "arm64") RID="linux-musl-arm64" ;; \
      *) RID="linux-musl-x64" ;; \
    esac && \
    dotnet publish -c Release -o /app -r $RID --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final

WORKDIR /app

# Copy published application
COPY --from=build /app .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000
ENV STOCK_API_BASE_URL=https://app-aistock-dev-002.azurewebsites.net

# Expose port
EXPOSE 5000

# Use non-root user (Alpine sets $APP_UID automatically)
USER $APP_UID

ENTRYPOINT ["dotnet", "AiStockTradeApp.McpServer.dll"]
