# Contributing to `tiki-shared-lib`

This repo is different from every other Tiki repo in one way that governs everything below:
**you are not changing a service, you are changing a dependency that five independently
deployed services already resolve.** A mistake here does not break one deploy — it breaks
whichever services next restore, at whatever time they happen to restore, with a stack trace
that points into a package rather than into their own code.

So the rules here are stricter than they would be in a service repo, and most of this document
is about the two things that go wrong: **putting the wrong thing in the library**, and
**publishing a version incorrectly**.

Read [`README.md`](README.md) first for what this package is. This document is what to *do*
when you change it.

> Named `CONTRIBUTING.md` rather than `CONTRIBUTE.md` because GitHub only surfaces the former
> automatically in the new-issue and new-PR views.

---

## Table of contents

1. [Before you write any code: does it belong here?](#1-before-you-write-any-code-does-it-belong-here)
2. [Repository map](#2-repository-map)
3. [Local setup](#3-local-setup)
4. [The development loop](#4-the-development-loop)
5. [Testing your change against a real service](#5-testing-your-change-against-a-real-service)
6. [Versioning: what kind of change is this?](#6-versioning-what-kind-of-change-is-this)
7. [The CHANGELOG is not optional](#7-the-changelog-is-not-optional)
8. [Branches, CI and how a version actually gets published](#8-branches-ci-and-how-a-version-actually-gets-published)
9. [Releasing `Tiki.Shared`](#9-releasing-tikishared)
10. [Changing a gRPC contract package](#10-changing-a-grpc-contract-package)
11. [Adding a new module](#11-adding-a-new-module)
12. [Testing standards](#12-testing-standards)
13. [Dependency rules — what CI will reject](#13-dependency-rules--what-ci-will-reject)
14. [Code style and documentation](#14-code-style-and-documentation)
15. [Pull request checklist](#15-pull-request-checklist)
16. [After you publish: rolling consumers forward](#16-after-you-publish-rolling-consumers-forward)
17. [Common failures and what they actually mean](#17-common-failures-and-what-they-actually-mean)

---

## 1. Before you write any code: does it belong here?

Run your change past the one rule from the README, in this exact form:

> **If two services would ever need *different* behavior from this code, it does not belong in
> `Tiki.Shared`.**

Everything in this package must give every consuming service *identical* behavior. No
per-service branching, no `if (serviceName == "wallet")`, no optional business logic behind a
config flag.

### The decision, as a sequence of questions

Ask them in order. The first "yes" is your answer.

| # | Question | If yes |
|---|---|---|
| 1 | Does it encode a **decision** a service makes? (`KycStatus`, `WalletFrozen`, a compliance threshold, a fee rule) | ❌ Belongs in the owning service, published from that service's own `*.Contracts` package. |
| 2 | Is it a **gRPC stub or a Kafka message record** for a specific service? | ❌ `src/Grpc.Contracts/Tiki.Grpc.Contracts.<Service>` for gRPC; the owning service's repo for Kafka records. See [§10](#10-changing-a-grpc-contract-package). |
| 3 | Does it need to know an **environment, a connection string, or a hostname**? | ❌ `Tiki.Shared` reads `IConfiguration` and never hardcodes an endpoint. |
| 4 | Does **exactly one service** need it today? | ⏸ Wait. Land it in that service. It is welcome here the moment a *second* service needs the *same* thing *identically* — not before. |
| 5 | Would it require a reference to a concrete DB provider, a vendor tracer, or a messaging framework? | ❌ Hard stop. See [§13](#13-dependency-rules--what-ci-will-reject). |
| 6 | Is it cross-cutting infrastructure wiring, a generic outcome/exception type, universal non-decision vocabulary, or a generic query/validation primitive? | ✅ It belongs here. Continue. |

### Worked examples

| Proposal | Verdict | Why |
|---|---|---|
| `IIdempotencyStore` with a Redis implementation | ✅ | Cross-cutting, identical semantics everywhere, no domain decision. |
| `MoneyRules.HasValidMinorUnits()` | ✅ | A data-*shape* check derived from ISO 4217, not a business rule. |
| `enum KycStatus { Pending, Approved, Rejected }` | ❌ | Compliance decides KYC. It belongs to Compliance. |
| `WalletDebitedEvent` | ❌ | A concrete domain event. Only the empty `BaseEvent` envelope lives here. |
| `MaximumTransferAmount` | ❌ | A business rule, and one that differs per tenant/market. |
| `CountryCode` / `CurrencyCode` | ✅ | ISO vocabulary. Nobody "decides" that NGN exists. |
| `AddTikiRetryPolicy()` for outbound HTTP | ✅ | Once a second service needs it identically. Attach it to the `AddTikiExternalHttpClient` builder. |
| A `Tiki:Wallet:Something` config section | ❌ | Per-service configuration. |

**When in doubt, raise it as an issue before you open a PR.** Removing something from this
package after two services depend on it is a major version bump plus a coordinated migration
across five repos. Adding it late is a minor version bump. The costs are not symmetric.

---

## 2. Repository map

```
tiki-shared-lib/
├── src/
│   ├── Tiki.Shared/                     the one package every service takes
│   │   ├── Auth/                        JWT, HMAC request signing, Redis sessions, permissions
│   │   ├── Caching/                     ITieredCache (L1 memory + L2 Redis)
│   │   ├── Core/                        exceptions, middleware, universal enums, BaseEvent, paging
│   │   ├── Extensions/                  JSON conventions, IServiceCollection wiring
│   │   ├── Gateway/                     TikiHeaderNames — the gateway↔service header contract
│   │   ├── Grpc/                        signing interceptors (client + server), trace propagation
│   │   ├── HealthChecks/                /health/live, /health/ready
│   │   ├── Http/                        AddTikiExternalHttpClient, session-lifecycle logging
│   │   ├── Logging/                     request logging, client IP, [Sensitive] masking
│   │   ├── Messaging/                   Kafka/Redpanda producer, consumer base, retry/DLQ router
│   │   ├── Persistence/                 BaseEntity, tenant+soft-delete filter, audit interceptor
│   │   ├── Querydsl/                    dynamic filter/sort/paginate over IQueryable<T>
│   │   ├── Results/                     Result<T> / Error
│   │   ├── Telemetry/                   AddTikiTelemetry() — OpenTelemetry wiring
│   │   └── Validation/                  FluentValidation base + universal shape rules
│   └── Grpc.Contracts/
│       ├── Tiki.Grpc.Contracts.Identity/       ┐  one package per owning service,
│       ├── Tiki.Grpc.Contracts.Wallet/         │  each versioned and released
│       ├── Tiki.Grpc.Contracts.Transaction/    │  independently of Tiki.Shared
│       ├── Tiki.Grpc.Contracts.Compliance/     │
│       └── Tiki.Grpc.Contracts.Integration/    ┘
├── tests/
│   ├── Tiki.Shared.Tests/               unit tests — no external dependency, ever
│   └── Grpc.Contracts.Tests/            packs each .nupkg and inspects what actually ships
├── tools/
│   ├── pack-local.sh                    pack everything into ../.local-packages
│   ├── pack-grpc-contract.sh            pack one contract package
│   └── hash-grpc-contract.sh            SHA-256 of a contract's shape, for consumer drift tests
├── .github/workflows/
│   └── ci.yml                           the whole pipeline: build, test, layering
│                                         check, pack, publish — one job, in order
├── Directory.Build.props                net10.0, nullable, analyzers, package metadata
├── CHANGELOG.md                         ← you will be editing this
└── Tiki-shared.sln
```

---

## 3. Local setup

### Prerequisites

- **.NET 10 SDK** — `dotnet --version` should report `10.0.x`. CI pins `10.0.x`; a different
  major will not reproduce CI locally.
- **Docker** — only if you want to run a service against your change ([§5](#5-testing-your-change-against-a-real-service)).
- **`python3`** — only for `tools/hash-grpc-contract.sh`.
- **Apple Silicon:** `Grpc.Tools` ships no native arm64 macOS `protoc`. `Directory.Build.props`
  already falls back to Homebrew's binaries when it finds them, so:
  ```bash
  brew install protobuf grpc
  ```
  Without either those or Rosetta 2, proto codegen fails with `Bad CPU type in executable`.

### Build and test

```bash
git clone git@github.com:capitalsagetechnology/tiki-shared-lib.git
cd tiki-shared-lib
dotnet restore Tiki-shared.sln
dotnet build   Tiki-shared.sln --configuration Release
dotnet test    Tiki-shared.sln --configuration Release
```

The whole suite runs with **no Postgres, no Redis, no Redpanda**. If your change makes that
untrue, the change is wrong, not the rule ([§12](#12-testing-standards)).

### Branch off `dev`

```bash
git checkout dev && git pull --ff-only
git checkout -b feat/tiered-cache-stampede-guard
```

`dev` is the integration branch and is published as a prerelease on every push. `main` is the
stable channel. Never branch off `main` for a feature.

---

## 4. The development loop

1. **Write the code**, in the module it belongs to. If it needs a new module, see [§11](#11-adding-a-new-module).
2. **Write tests alongside it.** Not after, and not in a follow-up PR — a module without tests
   cannot be released, because the pipeline gates the pack step on `dotnet test`.
3. **Write the XML doc comments**, including *why* ([§14](#14-code-style-and-documentation)).
4. **Run the full suite** — not just your module's tests. `Tiki.Shared` is one assembly and the
   modules see each other.
   ```bash
   dotnet test Tiki-shared.sln --configuration Release
   ```
5. **Check nothing disallowed crept in** — run the same check CI runs, locally:
   ```bash
   dotnet restore src/Tiki.Shared/Tiki.Shared.csproj
   grep -Eio 'Microsoft\.EntityFrameworkCore\.(SqlServer|Sqlite|Cosmos|Relational)|Npgsql\.EntityFrameworkCore|Pomelo\.EntityFrameworkCore|MySql\.EntityFrameworkCore|Datadog\.Trace|MassTransit' \
     src/Tiki.Shared/obj/project.assets.json | sort -u
   ```
   Any output at all is a CI failure. Empty output is what you want.
6. **Decide the version bump** ([§6](#6-versioning-what-kind-of-change-is-this)) and edit
   `<Version>` in `src/Tiki.Shared/Tiki.Shared.csproj`.
7. **Write the CHANGELOG entry** ([§7](#7-the-changelog-is-not-optional)).
8. **Verify against a real service** ([§5](#5-testing-your-change-against-a-real-service)) — required for
   anything touching auth, persistence, messaging or a public signature.
9. **Open a PR into `dev`** ([§15](#15-pull-request-checklist)).

---

## 5. Testing your change against a real service

This is the step people skip, and it is the step that catches the problems that unit tests
structurally cannot: DI registration order, configuration binding, middleware ordering, and
"this compiles but throws on the first request".

### Never use a project reference

You will be tempted to add
`<ProjectReference Include="../../tiki-shared-lib/src/Tiki.Shared/Tiki.Shared.csproj" />`
to the service you are testing with. Don't. It breaks two things:

- **It hides unreleased API.** A project reference compiles against whatever is on disk, so the
  service never discovers that it depends on something you have not published. It builds on your
  machine and fails in CI, or worse, builds in CI off a stale checkout and fails on the server.
- **It breaks the Docker build.** The service's build context no longer contains everything it
  needs, which is why one service's compose file previously had to set `context: ..` — a
  workaround that leaks this repo's layout into a sibling repo's deployment.

Consume it as a **package**, always. The local feed exists precisely so you can do that while
the version is unreleased.

### Pack into the local feed

```bash
cd tiki-shared-lib
./tools/pack-local.sh
```

This packs `Tiki.Shared` **and every gRPC contract package** into `../.local-packages` — the
repo-root feed, a sibling of all the service checkouts.

It also does something load-bearing before packing: it **deletes the previously-restored copies
of these packages from your global NuGet cache** (`~/.nuget/packages/tiki.*`). NuGet caches a
package by `id + version` and will not look at a feed again once it has one extracted globally.
Without that eviction, re-packing the same version number has *no effect* on a consumer that
already restored it — the build silently keeps using the old assembly and you spend an afternoon
debugging a bug you already fixed. This is the single most common local-development trap in
this repo.

### Point a service at the feed

Do **not** commit a local-feed entry to a service's `nuget.config`. It resolves relative to the
config file, so a path like `../.local-packages` becomes `/.local-packages` inside a container
and fails every Docker build with `NU1301`. Add it to your user-level config instead:

```bash
cd ../tiki-identity-api
dotnet nuget add source ../.local-packages -n tiki-local
dotnet restore
```

`./deploy.sh` stages the same feed into each Docker build context automatically when it finds
`.nupkg` files there, so `./deploy.sh up identity` picks up your local pack without any config
change.

### Then actually exercise it

```bash
cd ../tiki-shared-infra && ./tiki-infra up     # Postgres, Redis, Redpanda, OTLP collector
cd ../tiki-rebuild && ./deploy.sh up identity
./deploy.sh logs identity
```

For an auth change, drive a real request end to end: log in through the gateway, confirm the
session lands in Redis, confirm the downstream service accepts the signed hop, then log out and
confirm the next request is a 401.

### Undo it before you commit

```bash
dotnet nuget remove source tiki-local
```

A leftover local source on a build agent resolves a stale package with a real-looking version
number, which is worse than a clean failure.

---

## 6. Versioning: what kind of change is this?

`Tiki.Shared` follows [Semantic Versioning](https://semver.org/), and **every package this repo
publishes shares one version**, which lives in exactly one place:

```xml
<!-- Directory.Build.props -->
<TikiVersion>0.4.0</TikiVersion>
```

Every packable csproj reads it (`<Version>$(TikiVersion)</Version>`), and the pipeline stamps all
six packages with it on the same run. The pipeline refuses to publish when a release tag
disagrees with this value, so it is the source of truth, not a copy of one.

The contract packages used to version independently. That produced six numbers to track across
five consuming repos, and consumers pinned combinations that had never existed — see the 0.4.0
entry in `CHANGELOG.md`. One number, one bump, one line to review.

### Which digit moves

| Change | Bump | Example from this repo's history |
|---|---|---|
| Removing or renaming a public type, method or DI extension | **major** | `IServiceTokenProvider` removed in 0.2.0 |
| Changing a public method signature or a parameter's meaning | **major** | `HasPermission(...)` gained a `tenantId` parameter in 0.3.0 |
| Changing the shape of a serialized/persisted type | **major** | `TikiSession.TenantId` → `GlobalPermissions` + `TenantAccess` in 0.3.0 |
| Moving a configuration key | **major** | `Tiki:Auth:HmacServiceToken` → `Tiki:Auth:ServiceIdentity` in 0.2.0 |
| Changing default behavior in a way a consumer can observe | **major** | `ClockSkew` cut from 5 min to 30 s |
| New type, new method, new overload, new optional config | **minor** | `AddTikiJwtAuth()` added in 0.2.0 |
| Bug fix with no signature change | **patch** | `RedisNonceStore` made atomic in 0.3.0 |
| Doc comments, tests, internal refactor with no observable change | **patch** | — |

### The non-negotiable

> **A minor or patch release never changes a public method signature or a DI registration name.**

A consumer pinned to `0.3.x` must be able to take `0.3.7` without editing a line. Every breaking
change is a major bump *with a migration note* — see the `### Changed — BREAKING` blocks in
`CHANGELOG.md` for the format, including the `diff`-fenced before/after of the affected
`Program.cs` lines.

While the package is pre-1.0, treat the **minor** digit as the major: `0.2.0 → 0.3.0` carried
breaking changes, and that is the established convention here. Do not sneak a breaking change
into `0.3.1`.

---

## 7. The CHANGELOG is not optional

`CHANGELOG.md` is how a consuming team decides whether to take your version. A PR that changes
`src/` and not `CHANGELOG.md` will be sent back.

Add your entry under `## [Unreleased]` while the change is in flight; promote it to a numbered,
dated heading when you cut the release ([§9](#9-releasing-tikishared)).

Follow the existing house style, which is stricter than Keep-a-Changelog in one way that
matters: **every entry says why, and a breaking one shows the migration.**

```markdown
## [Unreleased]

### Changed — BREAKING: <one line saying what is different now>

<A paragraph explaining what the old mechanism was, and specifically what was wrong with it.
Not "improved X" — name the failure mode. The reader is deciding whether they can defer
this upgrade, and they cannot decide that from "improved".>

**Migrating.** In `Program.cs`:

```diff
- services.AddSingleton<IOldThing, OldThing>();
+ services.AddTikiNewThing(builder.Configuration);
```

<Any configuration keys that move, named on both sides.>

### Added — <what>

<Why it did not exist before, and what it makes possible.>

### Fixed — <what was broken>

<The actual mechanism of the bug. "Execution context is copy-on-write, so an AsyncLocal
written inside an awaited call is not visible to the caller afterwards" is the standard —
enough that a reader can tell whether they were affected.>
```

Write for someone who has to decide, at 4pm on a Friday, whether upgrading is safe.

---

## 8. Branches, CI and how a version actually gets published

Three triggers produce three version shapes. This exists because **NuGet treats a version as
immutable**: pushing `0.3.0` a second time is either rejected or silently ignored, and consumers
keep resolving the first build forever while appearing to be up to date.

| Trigger | Version published | Channel | Who gets it |
|---|---|---|---|
| Pull request | nothing | — | Builds and tests only |
| Push to `dev` | `0.4.0-dev.<run-number>` | prerelease | Only a consumer that explicitly opts into prereleases |
| Push to `main` | `0.4.0` | stable | Everyone on `0.4.x` |
| Tag `tiki-v0.4.0` | `0.4.0` | release | Explicit, verified against `<TikiVersion>` |

The dev suffix sorts *below* the stable release of the same number, so a consumer on `0.3.0` is
never silently upgraded onto an unreviewed dev build.

**The merge that changes `<Version>` is the release.** Merging to `main` with `<Version>` already
at `0.3.0` when `0.3.0` exists publishes nothing new (`--skip-duplicate`), which is the correct
and quiet outcome — not an error.

### One pipeline, one job

Everything lives in **`.github/workflows/ci.yml`**, as a single job whose steps run in order:

```
restore -> build -> test -> layering check -> pack -> push -> summary
```

This replaced three separate workflows — `build-test.yml`, `publish-shared.yml` and
`publish-grpc-contract.yml` — that all triggered on the same push. That was three checkouts,
three restores and three builds of the same commit, finishing in an order nobody controlled, so
"the tests passed" and "the package was published" were separate events with no guaranteed
relationship between them. A publish could complete while the test run for the same commit was
still going.

A single job makes the ordering structural rather than a convention. Every step runs on the same
checkout, and the first failure stops the rest — so nothing is published by a commit whose tests
have not already passed **in that same run**.

The steps, and what each is for:

1. **Restore / Build / Test** — Release configuration, whole solution. The test step uploads its
   `.trx` results as an artifact even when it fails.
2. **Verify no disallowed package references** — the layering check in
   [§13](#13-dependency-rules--what-ci-will-reject), reading the `project.assets.json` the restore
   already produced.
3. **Refuse to publish a tag that is not reachable from `main`** — tags only. No publishing off an
   unmerged branch.
4. **Pack** — every package on a branch push; on a tag, only the package the tag names, and only
   if the tag's version matches that project's `<Version>`.
5. **Push** — one `dotnet nuget push` of everything packed, with `--skip-duplicate`.

A pull request runs steps 1 and 2 and stops: it publishes nothing.

Runs are serialised per ref (`concurrency`), so two pushes in quick succession cannot race to
publish. Pull requests still cancel in progress, because there is nothing to serialise there.

## 9. Releasing `Tiki.Shared`

1. Land every change for the release on `dev` and confirm the `CI` pipeline is green.
2. Confirm `<TikiVersion>` in `Directory.Build.props` is the version you intend to release. It
   must be higher than anything already published — a version is immutable once pushed.
3. Promote the `## [Unreleased]` section in `CHANGELOG.md` to `## [0.4.x] — YYYY-MM-DD`, and
   leave a fresh empty `## [Unreleased]` above it.
4. Open a PR from `dev` to `main`. The PR body should be the CHANGELOG section, so the release
   review and the release notes are the same text.
5. Merge. The pipeline publishes the stable version.
6. Tag it, so the commit is findable from the version forever:
   ```bash
   git checkout main && git pull --ff-only
   git tag tiki-v0.4.0
   git push origin tiki-v0.4.0
   ```
   The workflow re-verifies that the tag's version matches `<Version>` and refuses to run if it
   does not — a tag that disagrees with the csproj would publish a package whose contents do not
   match the version anyone reading the repo would expect.
7. Tell the consuming teams ([§16](#16-after-you-publish-rolling-consumers-forward)). For a
   breaking change, link the migration note directly.

---

## 10. Changing a gRPC contract package

The five `Tiki.Grpc.Contracts.<Service>` packages live in this repo but are **not** part of
`Tiki.Shared`. They version and release on their own cadence, under their own tag namespace.

**A contract package is owned by the service it names.** Changing `Tiki.Grpc.Contracts.Wallet`
is a change to Wallet's public API surface, made in this repo for packaging reasons only. Get
the owning team's review.

### Making the change

1. Edit the `.proto` under `src/Grpc.Contracts/Tiki.Grpc.Contracts.<Service>/Protos/`.
2. **Field numbers are permanent.** Never renumber an existing field and never reuse the number
   of a removed one — a client built against the old numbering will decode the new wire format
   into the wrong fields, silently and without an error. Removing a field is a breaking change;
   record it and mark it `reserved`. The 0.2.0 Identity change is the worked example:
   `default_currencies` (field 4) was replaced by `default_currency` (field 7) and
   `supported_currencies` (field 8), and `country` moved to field 4 — noted explicitly as
   **Breaking** in the CHANGELOG.
3. Bump `<TikiVersion>` in `Directory.Build.props`. Contract packages no longer carry their own
   version: everything this repo publishes moves together.
4. Add a CHANGELOG entry. Contract releases get their own headings (e.g.
   `## [grpc-integration-v0.1.0]`).
5. Keep the dependency surface minimal: `Google.Protobuf` and `Grpc.Core.Api` only, with
   `Grpc.Tools` as `PrivateAssets="all"`. **Never** `Grpc.Net.Client` or `Grpc.AspNetCore` — a
   contract package must not force a transport choice on a consumer.
6. Ship the `.proto` itself as plain package content under `protos/` (`None Include=... Pack="true"
   PackagePath="protos"`), **not** as `contentFiles` — `contentFiles` is fed straight to a
   consumer's C# compiler on restore, which is never what you want for a file that exists to be
   read.
7. Run the contract tests, which pack each project and inspect the resulting `.nupkg` directly —
   proving what actually ships, not what the csproj claims should ship:
   ```bash
   dotnet test tests/Grpc.Contracts.Tests/Tiki.Grpc.Contracts.Tests.csproj
   ```
8. Pack it locally and try it in the consuming service:
   ```bash
   ./tools/pack-grpc-contract.sh wallet     # → artifacts/grpc-contracts/
   # or ./tools/pack-local.sh for everything at once, into ../.local-packages
   ```

### One service, one proto file per provider

`Tiki.Grpc.Contracts.Integration` holds `veriff.proto`, `kycaid.proto` and `flutterwave.proto`
side by side. Follow that shape — one proto per external provider inside the owning service's
package — rather than one giant `integration.proto`.

### Releasing a contract

There is no per-contract tag any more. Contracts release with everything else, on a push to
`main` or a `tiki-v<x.y.z>` tag:

```bash
git checkout main && git pull --ff-only
git tag tiki-v0.4.0
git push origin tiki-v0.4.0
```

The pipeline refuses to publish unless **both** hold:

- the tag commit is **reachable from `main`** (no publishing off an unmerged branch)
- the tag's version equals `<TikiVersion>` in `Directory.Build.props`

### Consumer drift detection

`tools/hash-grpc-contract.sh <service>` prints a SHA-256 over that contract's compiled RPC
signatures and every field of every message and enum — sorted, so declaration order and comments
never affect it.

```bash
./tools/hash-grpc-contract.sh identity
```

Paste that digest into a drift test **in the consuming service's repo**. If the published
contract's shape ever changes without a version bump, that test starts failing — which is the
point. The drift test deliberately does not live here: what this repo checks about its own
packages is *what actually ships* (`tests/Grpc.Contracts.Tests/PackageContentsTests.cs`); whether
a contract still matches what a service expects is that service's question.

---

## 11. Adding a new module

A module is a top-level folder under `src/Tiki.Shared/`. Adding one is a design decision, not a
mechanical step — clear it in an issue first.

Every module ships with all of the following. A module missing any of them is incomplete:

- [ ] **One `…Extensions.cs`** exposing the wiring as `AddTikiX(...)` / `UseTikiX(...)`. A
      consumer wires the module in one line and never news up an internal type.
- [ ] **An options class** bound from `IConfiguration` under `Tiki:<Module>:…`, with a
      `public const string SectionName`. Never a hardcoded endpoint, environment or connection
      string.
- [ ] **Interfaces for anything a consumer might substitute or fake**, so a service can unit-test
      against your module without a live dependency.
- [ ] **A test folder** at `tests/Tiki.Shared.Tests/<Module>/` that runs with no external
      dependency.
- [ ] **XML doc comments carrying the reasoning**, not just the mechanics ([§14](#14-code-style-and-documentation)).
- [ ] **A row in the Modules table in `README.md`.**
- [ ] **A CHANGELOG entry** under `### Added`.
- [ ] **No new package reference** unless it is unavoidable — and then check it against
      [§13](#13-dependency-rules--what-ci-will-reject) *before* you write the code.

Prefer `FrameworkReference Include="Microsoft.AspNetCore.App"` over a package where the type
exists in the shared framework. This project is deliberately a **plain class library**, not the
Web SDK, so Application-layer code in any consuming service can reference it without pulling in
a hosting model.

---

## 12. Testing standards

Tests live in `tests/Tiki.Shared.Tests/<Module>/`, mirroring `src/`.

### Every test runs with no external dependency

No live Redis, no Postgres, no Redpanda, no network. This is a hard requirement, not a
preference, because `dotnet test` gates the publish — a suite that needs a broker is a suite that
cannot gate a release, and an ungated release is one that reaches five services untested.

Fake at the seam:

- Redis → an `IDistributedCache` fake, or a test double for `ISessionStore` / `INonceStore`
- Kafka → assert against the `IProducer<TKey, TValue>` interface, as `RetryDlqRouterTests` does
- EF Core → the InMemory provider with the test support types under
  `tests/Tiki.Shared.Tests/Persistence/TestSupport/`
- HTTP → a stub `DelegatingHandler`, as the `Http` tests do

`Tiki.Shared` grants `InternalsVisibleTo("Tiki.Shared.Tests")`, so an internal seam can be
tested directly rather than forcing a type public just to reach it.

### What is worth a test

Prioritize the things whose failure is *silent*:

- **Security primitives**, exhaustively — signature canonicalization (query ordering, method
  casing, empty vs. present body), fixed-time comparison, nonce atomicity, clock-skew boundaries.
  `HmacRequestSignerTests` is the model.
- **Anything the request pipeline depends on for correctness** — the tenant query filter, the
  audit interceptor, header stripping, permission resolution across global + per-tenant grants.
- **Every bug you fix.** A regression test that fails before your fix and passes after is the
  minimum bar. Several of the 0.3.0 fixes — the ambient tenant being null, `RequestLoggingMiddleware`
  trusting `X-Tenant-Id` — were invisible in normal operation and would come straight back
  without one.
- **Expression-building edge cases in `Querydsl`**, per type: null handling, enum parsing, date
  ranges, raw-text comparison.

---

## 13. Dependency rules — what CI will reject

The pipeline's layering step greps the restored `project.assets.json` and **fails the build**
on any of:

```
Microsoft.EntityFrameworkCore.SqlServer | .Sqlite | .Cosmos | .Relational
Npgsql.EntityFrameworkCore | Pomelo.EntityFrameworkCore | MySql.EntityFrameworkCore
Datadog.Trace
MassTransit
```

Direct **or transitive** — a package that drags one in is just as rejected.

### The rules behind that list

1. **No concrete database provider.** `Persistence/` is the one module allowed to reference the
   base, provider-agnostic `Microsoft.EntityFrameworkCore` package — and only because it is
   consumed solely from a service's own Infrastructure-layer `DbContext`, never from Domain or
   Application. A concrete provider (Npgsql, SqlServer, Sqlite, …) stays in the consuming
   service, always. Note that `Microsoft.EntityFrameworkCore.Relational` is on the *rejected*
   list: it is provider-adjacent, not provider-agnostic.
2. **No EF Core anywhere outside `Persistence/`.** `Querydsl` is pure LINQ-expression building
   over whatever `IQueryable<T>` a repository supplies, and has no EF Core reference of its own.
   Keep it that way.
3. **No vendor tracing or logging SDK.** Vendor-neutral OpenTelemetry throughout. A service is
   configured with one OTLP endpoint and never learns a backend's name — that is what makes
   swapping Tempo for a hosted vendor a change to one collector config file in
   `tiki-shared-infra`, with no service redeploy.
4. **No messaging framework.** `Messaging/` talks to Redpanda directly via `Confluent.Kafka`,
   with no framework-level indirection.
5. **Caching is two-tier, always.** `ITieredCache` is L1 in-memory *and* L2 Redis. Never a config
   flag that selects one — that is two behaviors, and two behaviors is exactly what this package
   exists to prevent.

Adding **any** new `PackageReference` to `Tiki.Shared.csproj` needs an explicit note in the PR
description saying what it is for and why the framework or an existing dependency cannot do it.
Every dependency here is a dependency five services inherit whether they use it or not.

---

## 14. Code style and documentation

`Directory.Build.props` sets the baseline for every project: `net10.0`, `LangVersion latest`,
`Nullable enable`, `ImplicitUsings enable`, latest analyzers, `GenerateDocumentationFile`. Build
warnings are to be fixed, not suppressed — the generated contract projects are the one place with
a documented relaxation, and it is scoped to them.

### Document the why, not the what

The doc comments in this repo are its most valuable asset, because a consumer reading IntelliSense
is the only chance to prevent a misuse. The house standard, from the existing code:

```csharp
/// <summary>
/// Verifies a presented signature against a freshly computed one in fixed time.
/// <see cref="CryptographicOperations.FixedTimeEquals"/> is the point: a naive
/// comparison returns faster the earlier it finds a mismatched byte, which leaks the
/// correct signature one byte at a time to a caller willing to measure.
/// </summary>
```

That second sentence is the part that matters. `// compares two signatures` would be worthless.
Aim for that everywhere, and specifically:

- **Name the failure mode you are preventing.** "Sorting matters: the same parameters in a
  different order would otherwise produce a different signature."
- **Make trade-offs explicit where they are configured.** `CachingSessionStore`'s 5-second window
  is documented as a trade-off — revocation propagates within it — at the option that controls it,
  not in a design doc nobody reads.
- **Record what you deliberately did not do, and why.** The absence of a `/api/{**catch-all}` route
  in the gateway, and the absence of a local feed in a service's committed `nuget.config`, are both
  documented as decisions. That is what stops someone "fixing" them.
- **Where ordering is a security property, say so at the call site.** `UseTikiAmbientContext()`
  must be registered directly after `UseAuthentication()`; the reason is in its doc comment,
  because the failure mode without it — a silently null ambient tenant — is invisible.

### Commit messages

Conventional Commits, matching the existing history:

```
feat: implement secure client-side tenant selection with X-Tiki-Select-Tenant header
fix: improve atomic nonce handling in InMemoryNonceStore and RedisNonceStore
ci: update GitHub workflows to enable automated dev and main package publishing
docs: …    test: …    refactor: …    chore: …
```

Mark a breaking change with `!` (`feat!:`) or a `BREAKING CHANGE:` footer.

---

## 15. Pull request checklist

Copy this into your PR description.

```markdown
### What and why
<What changed, and the problem it solves. If you are removing or replacing a mechanism,
name what was wrong with the old one.>

### Does it belong in Tiki.Shared?
- [ ] No domain logic, no domain-decision enum, no concrete domain event
- [ ] Every consuming service gets identical behavior — no per-service branching
- [ ] Not per-service configuration; reads IConfiguration, hardcodes no endpoint
- [ ] At least two services need this, identically (or: it is a fix to existing shared code)

### Versioning
- [ ] `<TikiVersion>` in `Directory.Build.props` bumped: `0.x.y` → `0.x.z`
- [ ] Bump matches the change: breaking → major/minor-as-major, additive → minor, fix → patch
- [ ] No public signature or DI registration name changed in a minor/patch release
- [ ] Breaking changes have a migration note in CHANGELOG.md with a `diff` block

### CHANGELOG
- [ ] Entry added under `## [Unreleased]`, in house style, saying *why*

### Tests
- [ ] New/changed behavior is covered
- [ ] Every fix has a regression test that fails without the fix
- [ ] `dotnet test Tiki-shared.sln -c Release` passes locally
- [ ] No test requires Redis, Postgres, Redpanda or the network

### Dependencies
- [ ] No new PackageReference — or: justified below, and checked against §13
- [ ] No concrete DB provider, vendor tracer, or messaging framework (direct or transitive)
- [ ] The local disallowed-package grep from §4 step 5 returns nothing

### Verified against a real service
- [ ] `./tools/pack-local.sh`, consumed as a package (never a ProjectReference), exercised in:
      <service name> — <what you actually drove through it>
- [ ] Any local NuGet source removed again before committing

### Docs
- [ ] XML doc comments explain the reasoning, not just the mechanics
- [ ] README.md updated if a module was added or its surface changed
```

### Review expectations

- **Two approvals for anything under `Auth/`, `Persistence/`, or `Gateway/`.** These three decide
  who may read what; a mistake there is a cross-tenant data leak, not a bug.
- **The owning service's team reviews any `Grpc.Contracts` change.**
- Reviewers: check the change against [§1](#1-before-you-write-any-code-does-it-belong-here)
  first. The most expensive mistake in this repo is not a bug — it is something correct that
  should have lived in a service.

---

## 16. After you publish: rolling consumers forward

Publishing does not finish the job. Nothing upgrades on its own — every service pins its version.

1. **Announce it** with the CHANGELOG section, not a summary of it.
2. **For a breaking change, open the upgrade PR in each consuming repo yourself**, or pair with
   the owning team. You have the context on what moved; they have the context on their wiring.
   The five consumers are `tiki-api-gateway`, `tiki-identity-api`, `tiki-wallet-api`,
   `tiki-compliance-api`, `tiki-integrations-api`.
3. **Watch for the version-skew failures**, because they do not look like version skew:
   - A JWT signing key or HMAC secret that no longer matches → **401 on every request**, no
     obvious cause. The root `.env` exists so that key is written in exactly one place; a
     consumer upgraded halfway is the same symptom.
   - A session-shape change deployed to some services and not others → a service reading an old
     record shape from Redis authorizes against a field that is no longer there.
   - A gRPC contract renumbered without a version bump → fields decode into the wrong slots,
     with no error at all.
4. **Upgrade the gateway and Identity together** for anything touching sessions or the header
   contract. They are the two halves of one mechanism: the gateway stamps what Identity wrote.

---

## 17. Common failures and what they actually mean

| Symptom | Cause | Fix |
|---|---|---|
| Your change doesn't show up in a consuming service, even after re-packing | The global NuGet cache still holds that `id + version` and never re-reads the feed | Use `./tools/pack-local.sh` — it evicts `~/.nuget/packages/tiki.*` first. Never re-pack by hand without that step. |
| `NU1101: Unable to find package Tiki.Shared` | The GitHub Packages source is not registered, or `TIKI_GITHUB_TOKEN` is unset | Register the `github` source; use a PAT with `read:packages` scoped to `capitalsagetechnology`. |
| `NU1301` on a Docker build only | A relative local-feed path in a committed `nuget.config` resolves to `/.local-packages` inside the container | Remove it. Add the feed to your user-level config instead; `./deploy.sh` stages it into the build context. |
| `NU1507` warnings on every project | More than one package source registered without `packageSourceMapping` | Map `Tiki.*` to the private feed and `*` to nuget.org, as the service repos do. |
| CI: "Disallowed package reference found" | A concrete EF provider, vendor tracer or messaging framework arrived — often transitively | Run the §4 step 5 grep locally; `dotnet list package --include-transitive` to find who pulled it in. |
| CI: "Tag version does not match `<Version>`" | The tag and the csproj disagree | Delete the tag, fix `<Version>`, re-tag. Never force a mismatch through. |
| CI: "Tag is not reachable from main" (contracts) | You tagged an unmerged branch | Merge to `main` first, then tag the merge commit. |
| The publish ran but no new package appeared | `<Version>` was unchanged and `--skip-duplicate` did its job | Correct behavior. Bump `<Version>` if you meant to release. |
| `Bad CPU type in executable` during proto codegen | `Grpc.Tools` ships no arm64 macOS `protoc` | `brew install protobuf grpc`, or install Rosetta 2. |
| A consumer picked up an unreviewed build | It opted into prereleases and resolved `0.3.0-dev.<n>` | Pin to a stable version. Dev builds sort below the stable release of the same number by design. |

---

## Questions

Open an issue on this repo. For "should this live in `Tiki.Shared`?", open the issue **before**
writing the code — that question is much cheaper to answer in a paragraph than in a PR that has
to be unwound.
