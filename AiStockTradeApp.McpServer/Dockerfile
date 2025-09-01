# Use the official .NET 9 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET 9 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj", "AiStockTradeApp.McpServer/"]
COPY ["AiStockTradeApp.Entities/AiStockTradeApp.Entities.csproj", "AiStockTradeApp.Entities/"]

RUN dotnet restore "AiStockTradeApp.McpServer/AiStockTradeApp.McpServer.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/AiStockTradeApp.McpServer"
RUN dotnet build "AiStockTradeApp.McpServer.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "AiStockTradeApp.McpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create the final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV STOCK_API_BASE_URL=http://localhost:5000

# Create a non-root user
RUN addgroup --system --gid 1001 mcpserver && \
    adduser --system --uid 1001 --ingroup mcpserver mcpserver

USER mcpserver

ENTRYPOINT ["dotnet", "AiStockTradeApp.McpServer.dll"]
