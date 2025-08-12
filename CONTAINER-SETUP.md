# AI Stock Trade App - Container Setup

This document explains how to run the AI Stock Trade App in different container environments.

## Quick Start (In-Memory Database)

The simplest way to run the application in a container is using the in-memory database:

```bash
# Build and run with Docker
docker build -t ai-stock-trade-app ./ai-stock-trade-app
docker run -p 8080:8080 -p 8081:8081 ai-stock-trade-app
```

The application will be available at:
- HTTP: http://localhost:8080
- HTTPS: https://localhost:8081

## Production Setup (With SQL Server)

For a more production-like setup with persistent data, use Docker Compose:

```bash
# Start the entire stack (app + SQL Server)
docker-compose up -d

# View logs
docker-compose logs -f ai-stock-trade-app

# Stop the stack
docker-compose down
```

### Docker Compose Services

- **ai-stock-trade-app**: The main application
- **sqlserver**: Microsoft SQL Server 2022 Express

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `USE_INMEMORY_DB` | Use in-memory database instead of SQL Server | `true` (in container) |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Development` |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | Configured in docker-compose |

## Database Configuration

### Option 1: In-Memory Database (Default for Containers)

The Dockerfile sets `USE_INMEMORY_DB=true` by default. This means:
- ? No external database required
- ? Fast startup
- ? Data is lost when container stops
- ? Not suitable for production

### Option 2: SQL Server (via Docker Compose)

Use the provided `docker-compose.yml`:
- ? Persistent data storage
- ? Production-like setup
- ? Automatic database initialization
- ? Requires more resources

### Option 3: External SQL Server

Set the connection string via environment variable:

```bash
docker run -p 8080:8080 \
  -e USE_INMEMORY_DB=false \
  -e "ConnectionStrings__DefaultConnection=Server=your-sql-server;Database=StockTraderDb;User Id=your-user;Password=your-password;TrustServerCertificate=true" \
  ai-stock-trade-app
```

## Troubleshooting

### SQL Server Connection Issues

If you see errors like "network-related or instance-specific error":

1. **Check if using in-memory database**: Set `USE_INMEMORY_DB=true`
2. **Verify SQL Server is running**: `docker-compose ps`
3. **Check connection string**: Ensure server name matches service name in docker-compose
4. **Network connectivity**: Ensure containers are on the same network

### Container Startup Issues

1. **Port conflicts**: Change ports in docker-compose.yml if 8080/8081 are in use
2. **Memory issues**: SQL Server requires at least 2GB RAM
3. **Permission issues**: Check Docker daemon permissions

### Application Logs

View application logs:
```bash
# Docker run
docker logs <container-id>

# Docker Compose
docker-compose logs ai-stock-trade-app
```

## Development vs Production

### Development (In-Memory)
```bash
docker run -p 8080:8080 -e USE_INMEMORY_DB=true ai-stock-trade-app
```

### Production (With SQL Server)
```bash
docker-compose up -d
```

## Health Checks

The application provides health check endpoints:

- `/health` - Application health status
- `/version` - Application version information

Test health:
```bash
curl http://localhost:8080/health
curl http://localhost:8080/version
```

## Data Persistence

When using Docker Compose with SQL Server:
- Database data is stored in Docker volume `sqlserver_data`
- Data persists across container restarts
- To reset data: `docker-compose down -v` (removes volumes)

## Security Notes

?? **Important**: The default SQL Server setup uses weak credentials for development only.

For production:
1. Change the SA password in docker-compose.yml
2. Use Azure SQL Database or managed SQL Server
3. Configure proper authentication and TLS