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
- `src/Grpc.Contracts/` — one gRPC contract package per owning service
  (`Tiki.Grpc.Contracts.Identity/.Wallet/.Transaction/.Compliance/.Integration`), each
  versioned and released independently of `Tiki.Shared` itself (see
  `.github/workflows/publish-grpc-contract.yml`). `Tiki.Grpc.Contracts.Compliance` ships a
  real `GetVerificationStatus` RPC; the other four are placeholder shapes. See
  `tools/pack-grpc-contract.sh` and `tools/hash-grpc-contract.sh`.
- `Validation/Rules/` — FluentValidation extension methods for universal data-shape checks:
  `MoneyRules` (ISO 4217 minor-unit precision), `PhoneNumberRules` (E.164),
  `EmailRules`, `CountryCurrencyRules` (against `Core/Enums`), `IdentifierRules` (GUID
  shape), `DateRules` (future/age/range bounds). Never business rules.
- `ServiceContext.TenantId` — a third ambient value alongside trace id and calling
  service, set from the inbound `X-Tenant-Id` header.
- `Logging/ClientIpAccessor` — resolves the real caller IPv4 from the first hop of
  `X-Forwarded-For`, falling back to `RemoteIpAddress`, unwrapping an
  IPv6-mapped-IPv4 address to plain IPv4.
- `Logging/RequestLoggingMiddleware` — one structured log line per inbound request
  (method, path, status, duration, client IP, tenant, caller, trace id), wired into
  `UseTikiCore()` ahead of error handling and auth so even a rejected request is logged.
- `Core/Attributes/SensitiveAttribute` + `Logging/SensitiveDataMaskingPolicy` — a Serilog
  `IDestructuringPolicy` that masks every `[Sensitive]`-attributed property (full redact,
  last-4-visible, or hashed) on any type destructured for structured logging, for every
  sink, without a developer having to remember to mask it at the call site. Wired via the
  new `LoggingExtensions.ConfigureTikiLogging(serviceName)`.
- `ServiceContext.SessionId` — a GUID minted once per inbound request by
  `RequestLoggingMiddleware`, shared by every outbound call that request goes on to make.
- `Http/SessionLifecycleLoggingHandler` + `Http/HttpClientExtensions.AddTikiExternalHttpClient` —
  a `DelegatingHandler` logging started/completed/failed for every outbound call made
  through a client built via `AddTikiExternalHttpClient`, tagged with session id and trace
  id so one session id's log lines show the full downstream lifecycle of one inbound
  request, in order, with timing at each step. Never logs a query string or body. No
  retry/circuit-breaker/idempotency handler existed in this repo yet to compose alongside
  — `AddTikiExternalHttpClient` is structured so those can attach to the same builder later.
- `Persistence/` — the one module in `Tiki.Shared` allowed to reference EF Core directly
  (the base, provider-agnostic `Microsoft.EntityFrameworkCore` package only — never a
  concrete provider), since it is consumed only from a service's own Infrastructure-layer
  `DbContext`, never Domain or Application:
  - `Entities/BaseEntity` — `Id`, `TenantId`, `CreatedAt`/`CreatedBy`,
    `UpdatedAt`/`UpdatedBy`, `IsDeleted`, `RowVersion` (`[Timestamp]`). Zero package
    dependencies of its own, so Domain can inherit from it freely.
  - `ModelBuilderExtensions.ApplyTikiConventions(ModelBuilder, Func<Guid?>)` — a global
    query filter (`TenantId == current tenant && !IsDeleted`) plus a `TenantId` index on
    every `BaseEntity`-derived type. `IgnoreQueryFilters()` is the sanctioned escape hatch
    for a tenant-spanning admin query; raw SQL needs its own explicit `WHERE` clause.
  - `TenantAuditSaveChangesInterceptor` — stamps `TenantId`/`CreatedAt`/`CreatedBy` on
    insert and `UpdatedAt`/`UpdatedBy` on update, from the same ambient accessors.

  Update to `build-test.yml`'s disallowed-package check: the base `Microsoft.EntityFrameworkCore`
  package is now permitted in `Tiki.Shared`; a concrete provider (Npgsql, SqlServer,
  Sqlite, ...) still fails the build.

[Unreleased]: https://github.com/tiki/tiki-shared-lib/compare/main...HEAD
