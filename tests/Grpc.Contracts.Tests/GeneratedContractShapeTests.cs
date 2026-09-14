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
    public void Integration_Veriff_CreateSession_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.VeriffService.VeriffServiceClient),
            typeof(IntegrationNs.VeriffService.VeriffServiceBase),
            "CreateSession");

    [Fact]
    public void Integration_SmartComply_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.SmartComplyService.SmartComplyServiceClient),
            typeof(IntegrationNs.SmartComplyService.SmartComplyServiceBase));

    [Fact]
    public void Integration_SmartComply_SubmitKycCheck_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.SmartComplyService.SmartComplyServiceClient),
            typeof(IntegrationNs.SmartComplyService.SmartComplyServiceBase),
            "SubmitKycCheck");

    [Fact]
    public void Integration_Volume_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.VolumeService.VolumeServiceClient),
            typeof(IntegrationNs.VolumeService.VolumeServiceBase));

    [Fact]
    public void Integration_Volume_GetPaymentStatus_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.VolumeService.VolumeServiceClient),
            typeof(IntegrationNs.VolumeService.VolumeServiceBase),
            "GetPaymentStatus");

    [Fact]
    public void Integration_Flutterwave_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceClient),
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceBase));

    [Fact]
    public void Integration_Flutterwave_RefundTransaction_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceClient),
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceBase),
            "RefundTransaction");

    [Fact]
    public void Integration_ExchangeRates_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.ExchangeRatesService.ExchangeRatesServiceClient),
            typeof(IntegrationNs.ExchangeRatesService.ExchangeRatesServiceBase));

    [Theory]
    [InlineData("GetLiveRates")]
    [InlineData("GetCacheSettings")]
    [InlineData("UpdateCacheSettings")]
    public void Integration_ExchangeRates_rpc_is_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.ExchangeRatesService.ExchangeRatesServiceClient),
            typeof(IntegrationNs.ExchangeRatesService.ExchangeRatesServiceBase),
            rpcName);

    [Fact]
    public void Integration_Kycaid_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.KycaidService.KycaidServiceClient),
            typeof(IntegrationNs.KycaidService.KycaidServiceBase));

    [Fact]
    public void Integration_Pateno_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.PatenoService.PatenoServiceClient),
            typeof(IntegrationNs.PatenoService.PatenoServiceBase));

    [Theory]
    [InlineData("CreateEtransferRequestMoney")]
    [InlineData("SearchIncomingTransfers")]
    [InlineData("CreateEtransferTransactionWithCustomer")]
    [InlineData("SearchPayee")]
    [InlineData("CreateIndividualBillPayment")]
    public void Integration_Pateno_rpc_is_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.PatenoService.PatenoServiceClient),
            typeof(IntegrationNs.PatenoService.PatenoServiceBase),
            rpcName);

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

    /// <summary>
    /// The rpc Identity calls once onboarding completes. It replaced StartKycVerification, which
    /// made the caller name the verification tracks to run - a policy decision that belongs to
    /// Compliance, and which Identity had no way to get right for a market it does not model.
    /// </summary>
    [Fact]
    public void Compliance_StartOnboardingScreening_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(ComplianceNs.ComplianceService.ComplianceServiceClient),
            typeof(ComplianceNs.ComplianceService.ComplianceServiceBase),
            "StartOnboardingScreening");

    /// <summary>
    /// StartKycVerification is gone, not deprecated. A stub that still carries it lets a caller
    /// compile against an rpc the server no longer implements, and find out at runtime.
    /// </summary>
    [Fact]
    public void Compliance_StartKycVerification_rpc_is_gone()
    {
        Assert.Null(typeof(ComplianceNs.ComplianceService.ComplianceServiceClient)
            .GetMethod("StartKycVerification"));
        Assert.Null(typeof(ComplianceNs.ComplianceService.ComplianceServiceBase)
            .GetMethod("StartKycVerification"));
    }

    [Fact]
    public void Compliance_GetCustomerKycProfile_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(ComplianceNs.ComplianceService.ComplianceServiceClient),
            typeof(ComplianceNs.ComplianceService.ComplianceServiceBase),
            "GetCustomerKycProfile");

    /// <summary>
    /// The generated C# member names, not just the proto values. protoc strips the enum-name
    /// prefix and PascalCases what is left, so <c>KYC_PROVIDER_SMARTCOMPLY</c> becomes
    /// <c>Smartcomply</c> while <c>KYC_PROVIDER_SMART_COMPLY</c> would become
    /// <c>SmartComply</c> — a rename that breaks every consumer's switch arm, from a proto edit
    /// that looks like tidying.
    /// </summary>
    [Theory]
    [MemberData(nameof(ComplianceEnumMembers))]
    public void Compliance_kyc_enum_generates_the_expected_member_names(Type enumType, string[] expected) =>
        Assert.Equal(expected, Enum.GetNames(enumType));

    public static TheoryData<Type, string[]> ComplianceEnumMembers => new()
    {
        {
            typeof(ComplianceNs.KycVerificationType),
            ["Unspecified", "Identity", "Address", "SanctionScreening"]
        },
        {
            typeof(ComplianceNs.KycVerificationStatus),
            ["Unspecified", "NotStarted", "InProgress", "Approved", "Declined",
             "ResubmissionRequested", "Expired", "Abandoned"]
        },
        {
            typeof(ComplianceNs.KycVerificationReason),
            ["Unspecified", "InitialOnboarding", "PeriodicReverification", "TriggeredReview",
             "DocumentExpired"]
        },
        {
            typeof(ComplianceNs.KycProvider),
            ["Unspecified", "Veriff", "Kycaid", "Smartcomply"]
        },
    };

    /// <summary>
    /// A oneof, not two optional fields — so "which did you mean?" cannot arise. The generated
    /// accessor names are part of the contract: consuming code switches on
    /// <c>IdentifierCase</c>, so renaming the oneof is a source-breaking change even though the
    /// wire format is unchanged.
    /// </summary>
    [Fact]
    public void Compliance_GetCustomerKycProfileRequest_identifier_is_a_oneof()
    {
        var request = typeof(ComplianceNs.GetCustomerKycProfileRequest);

        Assert.NotNull(request.GetProperty("IdentifierCase"));
        Assert.Equal(
            new[] { "None", "ProfileId", "SubjectId" },
            Enum.GetNames(request.GetNestedType("IdentifierOneofCase")!).Order().ToArray());
    }

    private static void AssertGeneratesBoth(Type clientType, Type baseType)
    {
        // global:: — this file's own namespace, Tiki.Grpc.Contracts.Tests, shadows the
        // top-level Grpc namespace (Tiki.Grpc is itself a valid prefix here), so an
        // unqualified Grpc.Core.ClientBase would resolve to the wrong place.
        Assert.True(typeof(global::Grpc.Core.ClientBase).IsAssignableFrom(clientType), $"{clientType} should derive from Grpc.Core.ClientBase.");
        Assert.True(baseType.IsAbstract, $"{baseType} should be the abstract service base class.");
    }

    private static void AssertRpcPresentOnBoth(Type clientType, Type baseType, string rpcName)
    {
        var clientHasIt = clientType
            .GetMethods()
            .Any(m => m.Name.StartsWith(rpcName, StringComparison.Ordinal));
        var baseHasIt = baseType
            .GetMethods()
            .Any(m => m.Name == rpcName);

        Assert.True(clientHasIt, $"Client stub is missing {rpcName}.");
        Assert.True(baseHasIt, $"Service base class is missing {rpcName}.");
    }
}
