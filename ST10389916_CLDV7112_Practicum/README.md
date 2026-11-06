# Real-Time Processing System

This .NET 10 ASP.NET Core API accepts events at `POST /events`, processes them in a bounded background queue, and persists them to Azure SQL when `SQL_CONNECTION_STRING` is configured. Without a connection string it remains runnable locally and logs processed events.

## Run locally

1. Execute `schema.sql` in Azure SQL.
2. Set `SQL_CONNECTION_STRING` to the database connection string.
3. Run the project and post JSON such as:

```json
{"eventType":"OrderCreated","amount":149.99,"occurredAt":"2026-01-01T10:00:00Z","data":{"orderId":"A100"}}
```

Use `/health` for health checks and `/metrics` for autoscaling/monitoring signals.

## Azure deployment

Deploy `infra/main.bicep` to a resource group. It provisions App Service, Application Insights, Azure SQL Database, Event Hubs with auto-inflate, and App Service autoscaling from 1 to 10 instances at CPU thresholds of 70%/30%.