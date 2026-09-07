# Changelog

All notable changes to `Tiki.Shared` are documented here. This project follows
[Semantic Versioning](https://semver.org/): a breaking change is always a major
version bump with a migration note called out explicitly below — never a silent
behavior change in a minor or patch release.

## [0.3.0] — 2026-09-07

Team management and multi-tenant access. **Breaking** — a user can now hold different roles in
different tenants, which the previous single-tenant session could not express.

### Changed — BREAKING: `TikiSession` carries per-tenant access, not one tenant

`TikiSession.TenantId` and `TikiSession.Permissions` are replaced by `GlobalPermissions`
(applying in every tenant, including ones created later) and `TenantAccess` — a
`tenantId → TenantGrant` map. Permission checks now take the tenant they apply in:
`session.HasPermission(module, action, tenantId)`.

`ISessionStore.UpdatePermissionsAsync` becomes `UpdateAccessAsync`, taking both halves.

### Changed — BREAKING: permissions are a module × action grid

`TikiPermissions`' ad-hoc constants (`wallet:credit`, `compliance:override`, …) are replaced by
`TikiModule` × `PermissionAction` (`Read`/`Write`, write implying read). `[RequiresPermission]`
now takes the pair: `[RequiresPermission(TikiModule.Tenants, PermissionAction.Write)]`.

Operations needing a third level of authority — approving a payout, freezing a wallet — are
deliberately *not* extra actions. They are approval workflows with their own records, because
"who approved this and why" needs an audit trail a permission bit cannot carry.

### Added — active-tenant selection

`X-Tiki-Select-Tenant` lets a client choose which of its tenants a request acts in;
`TenantSelection.Resolve` validates it against the session before the gateway stamps the
trusted `X-Tenant-Id`. Deliberately two header names: letting a client send `X-Tenant-Id`
directly would mean removing it from the strip list, and every service treats that header as
proven. A selection the session does not grant is **denied**, never silently substituted.

### Fixed — ambient tenant was silently null (`UseTikiAmbientContext`)

`ServiceContext.TenantId` was set inside the JWT bearer handler. Execution context is
copy-on-write, so an `AsyncLocal` written inside an awaited call is not visible to the caller
afterwards — the value was null by the time authorization, and the EF Core tenant query filter,
read it. It went unnoticed while the session had a single tenant, because the tenant argument
was ignored. The active tenant is now carried on `HttpContext.Items` and copied onto
`ServiceContext` by `UseTikiAmbientContext()`, which must be registered directly after
`UseAuthentication()`.

### Fixed — `RequestLoggingMiddleware` no longer trusts `X-Tenant-Id`

It ran before authentication and copied the header into `ServiceContext.TenantId`, which drives
the tenant query filter in every service — so any client could send it and read another
tenant's rows. The header is now logged as `UnverifiedTenantIdHeader` and never used.

### Fixed — the nonce store is now genuinely atomic

`DistributedNonceStore` did a read followed by a write, so two concurrent replays could both
observe "unseen". Replaced by `RedisNonceStore` using `SET NX EX`, one command.

## [0.2.0] — 2026-09-07

Security release. Two of the three changes below are **breaking**; both replace a
mechanism that was weaker than it looked.

### Changed — BREAKING: service-to-service auth is now a signed request, not a bearer token

`IServiceTokenProvider` / `HmacServiceTokenProvider` are **removed**, along with
`ServiceTokenValidationMiddleware`, `ServiceTokenClientInterceptor` and
`ServiceTokenAuthInterceptor`.

The old token was an HMAC over `serviceId.expiry` and nothing else. That made it a
password: anyone who observed one could replay it against **any** endpoint with **any**
body until it expired, and because every service shared one secret, a single leak allowed
impersonating any service to any other.

The replacement signs a canonical form of the request itself — method, path, sorted query,
timestamp, nonce and a SHA-256 of the body — so a captured signature cannot be pointed at a
different route or have its payload edited. Freshness is bounded by a clock-skew window and
replay is closed by a nonce store. Keys are now per service, so a compromise is contained to
one service and rotation is a per-pair change.

**Migrating.** In `Program.cs`:

```diff
- services.AddSingleton<IServiceTokenProvider, HmacServiceTokenProvider>();
+ services.AddTikiServiceAuth(builder.Configuration);
...
- app.UseMiddleware<ServiceTokenValidationMiddleware>();
+ app.UseTikiServiceAuth();
```

Configuration moves from `Tiki:Auth:HmacServiceToken` to `Tiki:Auth:ServiceIdentity`, which
takes this service's own `ServiceId` and `SigningSecret` plus a `TrustedCallers` map of the
services allowed to call it. `[RequireServiceToken]` is unchanged. Outbound calls use
`AddTikiServiceClient<T>()` (HTTP) or `AddTikiGrpcClient<T>()` (gRPC) and need no call-site
changes.

### Added — sessions in Redis, so logout and permission changes take effect immediately

A signed JWT cannot be revoked: once issued it is valid until it expires, and nothing a user
does can call it back. Access tokens now carry only a session pointer (`sid`), and authority
lives in a Redis session record that Identity writes and every service reads.

- `TikiSession` — user, tenant, actor type, and the resolved permission set.
- `ISessionStore` / `RedisSessionStore` — create, revoke, revoke-all-for-user, list a user's
  sessions, and rewrite permissions on every live session in place.
- `CachingSessionStore` — a short in-process window (5s by default) in front of Redis, so a
  burst of requests from one user costs one round trip rather than one each. The trade-off
  is explicit: revocation propagates within that window. Set `CacheWindow` to zero for
  strictly-immediate revocation.
- `[RequiresPermission(...)]` + `TikiPermissions` — declarative permission checks evaluated
  against the already-loaded session, no database query and no call to Identity.
- `ISessionAccessor` — the current request's session, anywhere in its call graph.

### Added — `AddTikiJwtAuth()`, replacing hand-rolled `AddJwtBearer` in every service

Every service was configuring JWT validation itself and the copies had drifted. Identity's
own registration had `ValidateIssuer = false` and `ValidateAudience = false`, which would
have accepted a token minted by anything holding the same signing key.

`AddTikiJwtAuth()` requires issuer and audience, pins the signing algorithm, sets
`MapInboundClaims = false` (the default rewrites short claim names to SOAP-era URIs, so code
reading `sub` silently finds nothing), cuts `ClockSkew` from the 5-minute default to 30
seconds, rejects a refresh token presented as a bearer token, and validates the session.
Supports a symmetric key today and JWKS for the move to asymmetric signing.

### Added — the gateway header contract

`TikiHeaderNames` names every header the gateway and services exchange in one place,
including `StrippedFromClient`: the headers the gateway must remove from inbound client
requests. Without that list, a client could send `X-Tenant-Id` and read another tenant's
data.

### Added — `Gateway`, `Auth.Sessions`, `Auth.Authorization` modules; `UnauthorizedException` and `ForbiddenException` (mapped to 401/403).

### Added — full tenant management to `Tiki.Grpc.Contracts.Identity` (0.2.0)

`TenantDetails` gains `country_iso2`, `can_hold_wallet`, `default_currency`,
`supported_currencies`, `is_active` and timestamps. New RPCs: `GetTenantByCountry`,
`ListTenants`, and `GetTenantWalletPolicy` — the last so Wallet can answer "provision a
wallet, and in what currency?" in one round trip instead of re-deriving the rule from a
full tenant record.

**Breaking**: `TenantDetails.default_currencies` (repeated, field 4) is replaced by
`default_currency` (singular, field 7) plus `supported_currencies` (field 8), and
`country` moves from field 5 to field 4.

## [Unreleased]

## [grpc-integration-v0.2.0] - 2026-09-03

Additive publish of `Tiki.Grpc.Contracts.Integration`.

### Added
- `SmartComplyService` in `smartcomply.proto`: `SubmitKycCheck`, `SubmitMonitoring`,
  `SubmitScreening`.
- `VolumeService` in `volume.proto`: `GetPaymentStatus`, `InitiateRefund`.
  One proto per provider so KYCAID can still join as a sibling later.


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
  real `GetVerificationStatus` RPC; Identity, Wallet, and Transaction remain
  placeholder shapes. See `tools/pack-grpc-contract.sh` and
  `tools/hash-grpc-contract.sh`. Integration's first published shape is
  `Tiki.Grpc.Contracts.Integration` 0.1.0 below.
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

## [grpc-integration-v0.1.0] - 2026-09-02

First publish of `Tiki.Grpc.Contracts.Integration`.

### Added
- `VeriffService` in `veriff.proto`: `CreateSession`, `GetDecision`,
  `UpdateSessionStatus`. One proto per provider so KYCAID, Volume, and SmartComply
  can be siblings in the same owning package.

### Changed
- Placeholder `IntegrationService` / `integration.proto` replaced by
  `VeriffService` / `veriff.proto`.

[Unreleased]: https://github.com/tiki/tiki-shared-lib/compare/main...HEAD
[grpc-integration-v0.2.0]: https://github.com/tiki/tiki-shared-lib/releases/tag/grpc-integration-v0.2.0
[grpc-integration-v0.1.0]: https://github.com/tiki/tiki-shared-lib/releases/tag/grpc-integration-v0.1.0
