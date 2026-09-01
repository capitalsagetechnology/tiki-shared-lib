# Tiki.Shared

The one dependency every Tiki microservice repo takes on from day one.

Tiki is not a monorepo — Identity, Wallet, Transaction, Compliance, and Integration
Service each live in their own repo, with their own solution, their own CI pipeline,
and their own deploy lifecycle. What keeps independently-deployed services from
re-solving the same infrastructure problems in different ways is this one shared
package: published, versioned, and pinned like any third-party dependency — **never**
a project reference, **never** copy-pasted between repos.

## The one rule

> If two services would ever need *different* behavior from a piece of code, it does
> not belong in `Tiki.Shared`. Everything here is generic enough that every consuming
> service gets identical behavior from it — no per-service branching, no service-name
> switch statements, no optional business logic.

## What goes in

- Cross-cutting infrastructure wiring: caching, messaging, tracing, gRPC, auth, health
  checks, logging, JSON conventions.
- A generic exception hierarchy and a `Result<T>` outcome type for the Application layer.
- A dynamic filter/sort/paginate engine (`Querydsl`) that works over any `IQueryable<T>`.
- Truly universal, non-decision vocabulary (`CountryCode`, `CurrencyCode`, `Channel`)
  and the empty `BaseEvent` envelope.

## What does not go in

- **No domain logic.** No `KycStatus`, no `WalletDebited`, no compliance rules — those
  belong to the service that decides them, published from that service's own
  `*.Contracts` package.
- **Not a contracts registry.** gRPC client stubs and Kafka message record types live
  in each service's own repo.
- **Not per-service configuration.** `Tiki.Shared` reads from `IConfiguration`; it
  never hardcodes an environment or a connection string.
- **Not a place for one service's special case.** A need felt by one service first is
  welcome once a second service needs the same thing identically — not before.

## Architecture principles

1. **No concrete domain events or domain-decision enums.** Only the empty `BaseEvent`
   envelope and truly universal vocabulary live in `Core`.
2. **No proprietary tracer.** Vendor-neutral OpenTelemetry throughout.
3. **Real two-tier caching, not an either/or switch.** `ITieredCache` is L1
   (in-memory) *and* L2 (Redis) always, never a config flag choosing one.
4. **Kafka wired directly against Redpanda — no messaging framework in between.**
   `Tiki.Shared/Messaging` talks to Redpanda directly via Confluent.Kafka, with no
   framework-level indirection (no MassTransit or equivalent).
5. **No EF Core dependency outside `Persistence` — and never a concrete provider,
   even there.** `Tiki.Shared/Querydsl` is pure LINQ-expression-building over whatever
   `IQueryable<T>` a service's own Infrastructure-layer repository supplies; it has no
   EF Core reference of its own. `Tiki.Shared/Persistence` is the one deliberate
   exception — `BaseEntity`, a global tenant/soft-delete query filter, and an audit
   `SaveChangesInterceptor` — because it is consumed only from a service's own
   Infrastructure-layer `DbContext`, never from Domain or Application. It references
   the base, provider-agnostic `Microsoft.EntityFrameworkCore` package only; a concrete
   provider (Npgsql, SqlServer, ...) stays in the consuming service.

## Modules

| Module | Purpose |
|---|---|
| `Core` | Exceptions, middleware, universal enums, `BaseEvent`, paging model |
| `Results` | `Result<T>` / `Error` — the standard Application-layer outcome type |
| `Validation` | FluentValidation base classes + a `ValidationBehavior` pipeline step |
| `Telemetry` | `AddTikiTelemetry()` — OpenTelemetry tracing/metrics wiring |
| `Caching` | `ITieredCache` — L1 memory + L2 Redis, one API |
| `Querydsl` | Dynamic filter/sort/paginate over `IQueryable<T>`, zero EF Core |
| `Messaging` | Kafka/Redpanda producer, consumer base class, retry/DLQ routing |
| `Grpc` | Service-token interceptors, trace propagation |
| `Auth` | `ServiceContext`, `IServiceTokenProvider` |
| `HealthChecks` | `/health/live`, `/health/ready` with Postgres/Redis/Redpanda checks |
| `Logging` | Request logging, client IP capture, `[Sensitive]` masking, Serilog enrichers |
| `Http` | `AddTikiExternalHttpClient()` — session-lifecycle logging for outbound calls |
| `Extensions` | Shared JSON conventions, `IServiceCollection` wiring |
| `Persistence` | `BaseEntity`, tenant/soft-delete query filter, audit `SaveChangesInterceptor` — Infrastructure-layer only |

## Getting started

```bash
dotnet add package Tiki.Shared --version <pinned-version>
```

```csharp
builder.Services
    .AddTikiTelemetry("wallet-service", builder.Configuration)
    .AddTikiCache(builder.Configuration)
    .AddTikiMessaging(builder.Configuration)
    .AddTikiHealthChecks(builder.Configuration);
```

See each module's own doc comments for the full wiring surface. No source-reading
should be required to get telemetry, caching, messaging, and auth wired in under
30 minutes.

## Non-negotiables

- No reference, direct or transitive, to a concrete database provider or any
  vendor-specific tracing/logging SDK, anywhere. `Persistence` is the one module
  allowed to reference the base `Microsoft.EntityFrameworkCore` package itself —
  never a provider.
- A minor/patch release never changes a public method signature or DI registration
  name. Breaking changes are a major version bump with a migration note.
- Every module ships with a unit-test project that runs with no external dependency
  (no live Redis, Postgres, or Redpanda required).

See `CHANGELOG.md` for release history.
