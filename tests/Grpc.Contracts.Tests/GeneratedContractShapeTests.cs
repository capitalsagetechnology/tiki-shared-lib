using Xunit;
using ComplianceNs = Tiki.Grpc.Contracts.Compliance;
using IdentityNs = Tiki.Grpc.Contracts.Identity;
using IntegrationNs = Tiki.Grpc.Contracts.Integration;
using TransactionNs = Tiki.Grpc.Contracts.Transaction;
using WalletNs = Tiki.Grpc.Contracts.Wallet;

namespace Tiki.Grpc.Contracts.Tests;

/// <summary>
/// Proves <c>GrpcServices="Both"</c> actually generated both the client stub (for every
/// caller) and the service base class (for the owning service's own implementation) — for
/// every contract, not just Compliance.
/// </summary>
public class GeneratedContractShapeTests
{
    [Fact]
    public void Identity_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IdentityNs.IdentityService.IdentityServiceClient),
            typeof(IdentityNs.IdentityService.IdentityServiceBase));

    [Fact]
    public void Wallet_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(WalletNs.WalletService.WalletServiceClient),
            typeof(WalletNs.WalletService.WalletServiceBase));

    [Fact]
    public void Transaction_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(TransactionNs.TransactionService.TransactionServiceClient),
            typeof(TransactionNs.TransactionService.TransactionServiceBase));

    [Fact]
    public void Compliance_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(ComplianceNs.ComplianceService.ComplianceServiceClient),
            typeof(ComplianceNs.ComplianceService.ComplianceServiceBase));

    [Fact]
    public void Integration_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.VeriffService.VeriffServiceClient),
            typeof(IntegrationNs.VeriffService.VeriffServiceBase));

    [Fact]
    public void Integration_CreateSession_rpc_is_present_on_both_the_stub_and_the_base()
    {
        var clientHasIt = typeof(IntegrationNs.VeriffService.VeriffServiceClient)
            .GetMethods().Any(m => m.Name.StartsWith("CreateSession", StringComparison.Ordinal));
        var baseHasIt = typeof(IntegrationNs.VeriffService.VeriffServiceBase)
            .GetMethods().Any(m => m.Name == "CreateSession");

        Assert.True(clientHasIt, "Client stub is missing CreateSession.");
        Assert.True(baseHasIt, "Service base class is missing CreateSession.");
    }

    [Fact]
    public void Compliance_GetVerificationStatus_rpc_is_present_on_both_the_stub_and_the_base()
    {
        var clientHasIt = typeof(ComplianceNs.ComplianceService.ComplianceServiceClient)
            .GetMethods().Any(m => m.Name.StartsWith("GetVerificationStatus", StringComparison.Ordinal));
        var baseHasIt = typeof(ComplianceNs.ComplianceService.ComplianceServiceBase)
            .GetMethods().Any(m => m.Name == "GetVerificationStatus");

        Assert.True(clientHasIt, "Client stub is missing GetVerificationStatus.");
        Assert.True(baseHasIt, "Service base class is missing GetVerificationStatus.");
    }

    [Fact]
    public void Compliance_VerificationState_enum_has_the_specified_members()
    {
        var names = Enum.GetNames<ComplianceNs.VerificationState>();

        Assert.Equal(
            new[] { "Unspecified", "Pending", "Verified", "Rejected", "Expired" },
            names);
    }

    private static void AssertGeneratesBoth(Type clientType, Type baseType)
    {
        // global:: — this file's own namespace, Tiki.Grpc.Contracts.Tests, shadows the
        // top-level Grpc namespace (Tiki.Grpc is itself a valid prefix here), so an
        // unqualified Grpc.Core.ClientBase would resolve to the wrong place.
        Assert.True(typeof(global::Grpc.Core.ClientBase).IsAssignableFrom(clientType), $"{clientType} should derive from Grpc.Core.ClientBase.");
        Assert.True(baseType.IsAbstract, $"{baseType} should be the abstract service base class.");
    }
}
