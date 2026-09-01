# Changelog

All notable changes to `Tiki.Shared` are documented here. This project follows
[Semantic Versioning](https://semver.org/): a breaking change is always a major
version bump with a migration note called out explicitly below — never a silent
behavior change in a minor or patch release.

## [Unreleased]

### Added
- Initial project scaffold: `Core`, `Results`, `Validation`, `Telemetry`, `Caching`,
  `Querydsl`, `Messaging`, `Grpc`, `Auth`, `HealthChecks`, `Logging`, `Extensions`.
- `ITieredCache` with L1 (`IMemoryCache`) + L2 (`IDistributedCache`/Redis) read-through
  and write-through behavior, per-key TTLs, and a skip-L1 option.
- `QuerydslExecutor` — dynamic filter/sort/paginate over `IQueryable<T>` with no
  EF Core dependency.
- Kafka/Redpanda messaging: `ITikiMessageProducer`, `TikiConsumerBackgroundService`,
  and the retry/DLQ topic pattern (`{topic}.retry`, `{topic}.dlq`).
- gRPC service-token interceptors (client + server) with automatic trace propagation.
- `ServiceContext` (`AsyncLocal`-backed ambient trace id / calling-service id) and
  `IServiceTokenProvider` with an interim HMAC implementation.
- `/health/live` and `/health/ready` endpoints covering Postgres, Redis, and Redpanda.
- Serilog enrichers and shared `System.Text.Json` conventions.

[Unreleased]: https://github.com/tiki/tiki-shared-lib/compare/main...HEAD
