# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
# Use in-memory database for container environments
ENV USE_INMEMORY_DB=true


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION
ENV APP_VERSION=${APP_VERSION}
WORKDIR /src
COPY ["AiStockTradeApp.csproj", "."]
RUN dotnet restore "./AiStockTradeApp.csproj"
COPY . .
WORKDIR "/src/."
RUN if [ -n "$APP_VERSION" ]; then \
			echo "Building with semantic version $APP_VERSION"; \
			BASE_VERSION=$(echo "$APP_VERSION" | sed 's/[+-].*$//' ); \
			dotnet build "./AiStockTradeApp.csproj" -c $BUILD_CONFIGURATION -o /app/build \
				/p:Version=$APP_VERSION \
				/p:InformationalVersion=$APP_VERSION \
				/p:FileVersion=$BASE_VERSION \
				/p:AssemblyVersion=$BASE_VERSION; \
		else \
			echo "APP_VERSION not provided; performing standard build"; \
			dotnet build "./AiStockTradeApp.csproj" -c $BUILD_CONFIGURATION -o /app/build; \
		fi

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG APP_VERSION
ENV APP_VERSION=${APP_VERSION}
RUN if [ -n "$APP_VERSION" ]; then \
			echo "Publishing with semantic version $APP_VERSION"; \
			BASE_VERSION=$(echo "$APP_VERSION" | sed 's/[+-].*$//' ); \
			dotnet publish "./AiStockTradeApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false \
				/p:Version=$APP_VERSION \
				/p:InformationalVersion=$APP_VERSION \
				/p:FileVersion=$BASE_VERSION \
				/p:AssemblyVersion=$BASE_VERSION; \
		else \
			echo "APP_VERSION not provided; performing standard publish"; \
			dotnet publish "./AiStockTradeApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false; \
		fi

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
ARG APP_VERSION
ENV APP_VERSION=${APP_VERSION}
# Use in-memory database for container environments
ENV USE_INMEMORY_DB=true
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AiStockTradeApp.dll"]