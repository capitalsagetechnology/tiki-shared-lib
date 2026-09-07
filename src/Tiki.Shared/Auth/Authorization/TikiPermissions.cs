namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// The platform's permission vocabulary, as <c>{resource}:{action}</c> string constants.
///
/// <para>
/// This is the one deliberate exception to "no domain vocabulary in Tiki.Shared", and it
/// earns its place: a permission string is a contract between the service that <em>grants</em>
/// it (Identity, when it builds a session) and every service that <em>enforces</em> it. Left
/// to each repo, the grant side would write <c>"wallet:read"</c> and the enforce side
/// <c>"wallet.read"</c>, and the mismatch would present as a 403 nobody can explain rather
/// than as a build failure.
/// </para>
///
/// <para>Names only — no policy, no role-to-permission mapping. Which role gets what is
/// Identity's decision and lives in Identity.</para>
/// </summary>
public static class TikiPermissions
{
    // --- Tenants (Identity) ------------------------------------------------------------
    public const string TenantRead = "tenant:read";
    public const string TenantWrite = "tenant:write";
    public const string TenantAdmin = "tenant:admin";

    // --- Businesses (Identity) ---------------------------------------------------------
    public const string BusinessRead = "business:read";
    public const string BusinessWrite = "business:write";
    public const string BusinessTeamManage = "business:team:manage";

    // --- Users (Identity) --------------------------------------------------------------
    public const string UserRead = "user:read";
    public const string UserWrite = "user:write";
    public const string UserImpersonate = "user:impersonate";

    // --- Wallets ------------------------------------------------------------------------
    public const string WalletRead = "wallet:read";
    public const string WalletCredit = "wallet:credit";
    public const string WalletDebit = "wallet:debit";
    public const string WalletFreeze = "wallet:freeze";

    // --- Transactions --------------------------------------------------------------------
    public const string TransactionRead = "transaction:read";
    public const string TransactionInitiate = "transaction:initiate";
    public const string TransactionApprove = "transaction:approve";

    // --- Compliance ------------------------------------------------------------------------
    public const string ComplianceRead = "compliance:read";
    public const string ComplianceReview = "compliance:review";
    public const string ComplianceOverride = "compliance:override";

    // --- Platform ---------------------------------------------------------------------------
    public const string PlatformAdmin = "platform:admin";
}
