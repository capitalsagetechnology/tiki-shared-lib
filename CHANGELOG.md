# Changelog

All notable changes to `Tiki.Shared` are documented here. This project follows
[Semantic Versioning](https://semver.org/): a breaking change is always a major
version bump with a migration note called out explicitly below — never a silent
behavior change in a minor or patch release.

## [0.8.0] — 2026-09-11

### Added — `exchangerates.proto` (Tiki.Grpc.Contracts.Integration)

New provider proto for ExchangeRatesAPI FX rates (`ExchangeRatesService`) backed by GET
`/v1/latest`. Reuses the live-rates and cache-settings messages already defined in
`currencylayer.proto`: `GetLiveRates`, `GetCacheSettings`, and `UpdateCacheSettings`.

## [0.7.4] — 2026-09-09

### Added — `pateno.proto` (Tiki.Grpc.Contracts.Integration)

New provider proto for Pateno (Interac e-Transfer and bill payment, via DC Bank's Client API
integration surface) - `PatenoService` with five RPCs matching the five endpoints
tiki-integrations-api's Pateno adapter calls: `CreateEtransferRequestMoney`,
`SearchIncomingTransfers`, `CreateEtransferTransactionWithCustomer`, `SearchPayee`, and
`CreateIndividualBillPayment`. Additive only - one new file under
`Tiki.Grpc.Contracts.Integration/Protos/`, picked up by the existing `Protos/*.proto` wildcard
with no `.csproj` change. No existing message or RPC changed, so nothing else in this repo's
published surface moves.

Request/response field names are taken directly from Pateno's own published API reference (the
Postman collection served from docs.pateno.com) and confirmed against live sandbox calls.

## [0.7.3] — 2026-09-09

### Added — `CurrencyLayerService.GetCacheSettings` / `UpdateCacheSettings`

`Tiki.Grpc.Contracts.Integration`'s `CurrencyLayerService` (FX rates, `currencylayer.proto`) gains
two RPCs alongside the existing `GetLiveRates`: `GetCacheSettings` reads the rate cache's current
refresh/eviction cadence, and `UpdateCacheSettings` changes it. Both return the same
`CacheSettingsReply` (`cache_refresh_minutes`, `cache_eviction_days`), so the cadence is
runtime-updatable rather than fixed at deploy time. Additive — `GetLiveRates` and its messages are
unchanged.

## [0.7.1] — 2026-09-08

### Fixed — a service could not start, or stay alive, while Redis was unreachable

`AddTikiServiceAuth` and `AddTikiSessions` created the Redis multiplexer with StackExchange's
default `abortConnect=true`, so `Connect()` threw whenever Redis was down. Because that is a DI
factory, the throw repeated on every resolution — ten seconds per request, on every endpoint
that reached it. And every endpoint did: the service-authentication middleware resolved the
request verifier (and through it the nonce store, and through that Redis) for every request,
including `/health/live`. A service whose only problem was that Redis started after it answered
500 to its liveness probe until the orchestrator gave up and restarted it — which did not help.

Two changes. The multiplexer is now created with `AbortOnConnectFail=false` and a bounded
connect timeout: created once, reconnects in the background, and an operation attempted while
Redis is down fails fast. And the middleware resolves the verifier only for a request that
actually carries a signature or reaches an endpoint that requires one. Liveness is about the
process again; readiness is what reports the dependency.

## [0.7.0] — 2026-09-07

### Changed — authorization denies by default

`AddTikiJwtAuth` previously called `AddAuthorization()` with no fallback policy, which means an
endpoint carrying no authorization attribute at all is anonymous. Every service inherited that:
a new controller was public until someone remembered `[Authorize]`, and nothing failed if they
did not — no error, no failing test, no line of code stating the endpoint was ever meant to be
protected.

It now registers a fallback policy requiring an authenticated user, so the mistake inverts:
forget the attribute and the endpoint answers 401 the first time it is called. Opening something
up takes an explicit `[AllowAnonymous]`, which is visible in review and greppable across the
platform.

**Migration.** Anything genuinely public needs `[AllowAnonymous]` before upgrading — sign-in,
sign-up, password reset, public reference data — and so does anything authenticated by something
other than the JWT pipeline. In this platform that is the gRPC surface, which
`ServiceAuthInterceptor` authenticates by HMAC signature: `app.MapGrpcService<T>()` becomes
`app.MapGrpcService<T>().AllowAnonymous()`, or every service-to-service call is rejected before
the interceptor runs.

`MapTikiHealthChecks` handles its own case — both endpoints are now explicitly anonymous, because
a probe has no credential and an orchestrator that reads 401 from `/health/live` restarts the
container forever.

### Added — `TikiHeaderNames.ClientBaseUrl`

`X-Tiki-Client-Base-Url`, so a service raising an email can say which front end its action link
belongs to. Untrusted by design and not in `StrippedFromClient`: the sender that legitimately
sets it — the admin BFF — reaches other services through the gateway like any other client. The
Notification service checks the value against a configured allow-list, so the header picks one of
the permitted clients and cannot add one.

## [0.6.0] — 2026-09-07

### Added — `Tiki.Contracts.Notifications`

The Kafka event contracts for the new Notification service, so a producer and the consumer cannot
drift: `EmailRequested`, `SmsRequested`, the topic names, the template catalogue and the model
keys the templates read.

`EmailRequested.ClientBaseUrl` lets a caller say which client an action link belongs to — the
backoffice today, a customer or partner app later — without the Notification service being
redeployed per client. The Notification service validates it against a configured allow-list and
refuses anything unlisted, so the caller picks the destination but not the set of possible ones.

Events derive from `BaseEvent`, so a delivered email carries the trace id of the request that
asked for it.

### Changed — CI packs every contracts folder

The publish step globbed `src/Grpc.Contracts/*/*.csproj`, which would have silently skipped the
new package under `src/Event.Contracts/`. It now globs `src/*.Contracts/*/*.csproj` — a list
someone has to remember to extend is a list that eventually is not extended.

## [0.5.0] — 2026-09-07

### Changed — the health endpoints answer JSON, and `/health/ready` names the dependency that failed

`/health/live` and `/health/ready` returned the ASP.NET default: the bare string `Healthy` as
`text/plain`. Enough for a container healthcheck, which reads only the status code. Not enough for
anything else — a failing `/health/ready` said that *something* was unreachable and nothing more,
so working out whether it was Postgres, Redis or Redpanda meant going to the logs of the pod that
was already refusing traffic.

Both now return `application/json`:

```jsonc
// GET /health/live  -> 200
{ "status": "healthy", "service": "wallet-service", "checkedAt": "...", "totalDurationMs": 0.01 }

// GET /health/ready -> 503
{
  "status": "unhealthy",
  "service": "wallet-service",
  "checkedAt": "2026-09-07T15:04:11.2210000+00:00",
  "totalDurationMs": 12.35,
  "checks": {
    "postgres": { "status": "healthy",   "durationMs": 3.1 },
    "redis":    { "status": "unhealthy", "description": "Redis connectivity check failed.",
                  "durationMs": 1.2, "error": "RedisConnectionException" },
    "redpanda": { "status": "healthy",   "description": "3 broker(s) reachable.", "durationMs": 8.0 }
  }
}
```

**Status codes are unchanged** — healthy and degraded are 200, unhealthy is 503 — because that is
what container healthchecks and the gateway's active probing read. The body is for whoever is
reading the failure, not for the machine acting on it. Nothing in this platform parsed the old
body, so nothing needs a change to keep working.

Two details worth knowing:

- **`error` is an exception's type name, never its message.** These endpoints are unauthenticated
  and reachable by anything on the mesh, and a driver exception carries the connection it failed
  on — host, database, sometimes credentials. `NpgsqlException` tells an operator which layer
  broke; the message would tell a reader the topology. A test asserts a planted password never
  reaches the body.
- **`/health/live` omits `checks` entirely** rather than returning an empty object, because an
  empty object reads as "checked everything, found nothing wrong" rather than "checked nothing" —
  and checking nothing is the whole point of a liveness probe.

`MapTikiHealthChecks()` takes an optional service name, defaulting to `Tiki:Telemetry:ServiceName`
so a health body and a trace agree on what the service is called. `HealthCheckExtensions.WriteAsync`
is public, so a host that maps the endpoints itself — the workers, which map by tag — gets the same
body rather than a second, nearly-identical one.

## [0.4.0] — 2026-09-07

Versioning and CI. No API change to `Tiki.Shared` itself — but every package this repo publishes
now carries **one** version, and every consumer pins **one** number.

### Changed — one `TikiVersion` for every package in this repo

`Tiki.Shared` and all five `Tiki.Grpc.Contracts.*` packages take their version from
`<TikiVersion>` in `Directory.Build.props`, and the pipeline publishes them together.

They used to version independently, on the theory that each should release on its own cadence.
What that produced in practice was six numbers to keep straight across five consuming repos, and
consumers pinning combinations that had never existed: `tiki-compliance-api` asked for three
contract versions that were never published, and `tiki-integrations-api` pinned `Tiki.Shared`
0.1.0 — two breaking releases behind the API it was actually compiled against, which is why it no
longer built at all.

One number removes the question "which contract version goes with which `Tiki.Shared`?", and it is
answerable by reading one line. A package with no changes republishes as a no-op.

0.4.0 rather than 0.3.1, because it has to clear every version already published: `Tiki.Shared`
0.3.0 and `Tiki.Grpc.Contracts.Integration` 0.3.0 both exist, and a version is immutable once
pushed.

**Migrating.** In each consuming repo's `Directory.Packages.props`:

```diff
+   <TikiVersion>0.4.0</TikiVersion>
-   <PackageVersion Include="Tiki.Shared" Version="0.3.0" />
-   <PackageVersion Include="Tiki.Grpc.Contracts.Identity" Version="0.2.0" />
+   <PackageVersion Include="Tiki.Shared" Version="$(TikiVersion)" />
+   <PackageVersion Include="Tiki.Grpc.Contracts.Identity" Version="$(TikiVersion)" />
```

### Changed — three workflows became one serial pipeline

`build-test.yml`, `publish-shared.yml` and `publish-grpc-contract.yml` all triggered on the same
push: three checkouts, three restores and three builds of one commit, finishing in an order nobody
controlled. "The tests passed" and "the package was published" were separate events with no
guaranteed relationship — a publish could complete while the test run for that same commit was
still going.

`ci.yml` is one job whose steps run in order, so nothing is published by a commit whose tests have
not already passed in that same run.

**The tag namespace collapses with the versions.** `shared-v*.*.*` and `grpc-<service>-v*.*.*` are
replaced by a single `tiki-v*.*.*`, verified against `<TikiVersion>`.

## [grpc-compliance-v0.2.0] — 2026-09-07

`Tiki.Grpc.Contracts.Compliance` only ever declared `GetVerificationStatus`, but
`tiki-compliance-api` had been written against a much larger surface — so the service did not
compile against the contract it is the owner of. Every message and enum below already existed in
Compliance's own domain and application layers; this publishes the shape they were always
implementing.

Additive: `GetVerificationStatus`, `GetVerificationStatusRequest`/`Response` and
`VerificationState` are unchanged, with their field numbers intact.

### Added — `StartKycVerification`

Opens a verification round for a subject and starts the requested tracks, creating the subject's
profile on first sight. Takes `KycSubject` — provider-neutral subject details, every field
optional but the names, because what a provider requires differs by provider, country and
verification type. Returns the round, the resolved provider and the sessions opened.

### Added — `GetCustomerKycProfile`

A subject's whole KYC standing: the three current tracks, `verified_until`, and every round ever
run. The request is a **oneof** over `profile_id` and `subject_id` rather than two optional
fields — a request carrying both would otherwise need a precedence rule, and a precedence rule is
a thing callers get wrong silently.

### Added — the shared KYC vocabulary

`KycVerificationType`, `KycVerificationStatus`, `KycVerificationReason`, `KycProvider`,
`KycVerificationRound` and `KycVerificationSession`.

Two decisions worth recording. `KYC_VERIFICATION_STATUS_NOT_STARTED` and `..._UNSPECIFIED` are
deliberately different values: the first means Compliance knows the track has not begun, the
second that the field was never set, and collapsing them would make a serialisation bug look like
a real state. And `KycProvider` is **reported, never requested** — the provider is resolved from
the tenant's configuration for the subject's country, so a caller cannot pick one, and a round
already run keeps naming the provider that ran it after that configuration changes.

`GeneratedContractShapeTests` now pins the generated C# member names, not just the proto values.
protoc strips the enum-name prefix and PascalCases the remainder, so `KYC_PROVIDER_SMARTCOMPLY`
becomes `Smartcomply` while `KYC_PROVIDER_SMART_COMPLY` would become `SmartComply` — a rename that
breaks every consumer's switch arm, produced by a proto edit that looks like tidying.

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
