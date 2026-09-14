namespace Tiki.Shared.Auth.Authorization;

/// <summary>
/// The modules a business's own team operates within — distinct from <see cref="TikiModule"/>,
/// which is the platform's internal admin vocabulary. A business's team member never holds a
/// <see cref="TikiModule"/> permission; a Tiki staff member never holds a
/// <see cref="BusinessModule"/> one. The two are deliberately separate enums rather than one
/// merged list, so neither vocabulary can leak a value that means nothing in the other's
/// context — "Global Admin", for instance, has no sensible answer for "write access to a
/// business's Store module".
/// </summary>
public enum BusinessModule
{
    /// <summary>A business's own payments and transfers history.</summary>
    Transactions,

    /// <summary>The business's own customers.</summary>
    Customers,

    /// <summary>Balance, settlements, and topping up wallets.</summary>
    Balances,

    /// <summary>Creating, approving, and managing transfers and beneficiaries.</summary>
    Transfers,

    Subaccounts,

    Cards,

    Chargebacks,

    /// <summary>Airtime and DStv transactions.</summary>
    AirtimeAndDstv,

    /// <summary>The business's store, orders, and products.</summary>
    Store,

    PaymentLinks,

    Invoices,

    /// <summary>Payment plans and subscriptions.</summary>
    PaymentPlans,

    Referrals,

    /// <summary>Custom roles and permissions for the business's own team.</summary>
    Roles,

    /// <summary>Business profile and preferences, API keys, webhooks.</summary>
    Settings,

    FixedVirtualAccounts,
}
