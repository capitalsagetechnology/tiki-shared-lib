using Xunit;
using ComplianceNs = Tiki.Grpc.Contracts.Compliance;
using IdentityNs = Tiki.Grpc.Contracts.Identity;
using IntegrationNs = Tiki.Grpc.Contracts.Integration;
using NotificationNs = Tiki.Grpc.Contracts.Notification;
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
    public void Wallet_PayoutRouting_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(WalletNs.PayoutRoutingService.PayoutRoutingServiceClient),
            typeof(WalletNs.PayoutRoutingService.PayoutRoutingServiceBase));

    [Theory]
    [InlineData("SetPreferredProvider")]
    [InlineData("GetPreferredProvider")]
    public void Wallet_PayoutRouting_rpc_is_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(WalletNs.PayoutRoutingService.PayoutRoutingServiceClient),
            typeof(WalletNs.PayoutRoutingService.PayoutRoutingServiceBase),
            rpcName);

    [Fact]
    public void Wallet_PayoutRouting_messages_have_the_expected_generated_fields()
    {
        AssertFields<WalletNs.SetPreferredProviderRequest>(
            ("SourceCountry", typeof(string)), ("DestinationCountry", typeof(string)),
            ("CurrencyCode", typeof(string)), ("PreferredProviderName", typeof(string)));
        AssertFields<WalletNs.GetPreferredProviderRequest>(
            ("SourceCountry", typeof(string)), ("DestinationCountry", typeof(string)),
            ("CurrencyCode", typeof(string)));
        AssertFields<WalletNs.GetPreferredProviderResponse>(
            ("PreferredProviderName", typeof(string)));
    }

    [Fact]
    public void Transaction_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(TransactionNs.TransactionService.TransactionServiceClient),
            typeof(TransactionNs.TransactionService.TransactionServiceBase));

    [Fact]
    public void Notification_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(NotificationNs.NotificationProvidersService.NotificationProvidersServiceClient),
            typeof(NotificationNs.NotificationProvidersService.NotificationProvidersServiceBase));

    [Theory]
    [InlineData("GetProviders")]
    [InlineData("UpdatePrimaryProvider")]
    public void Notification_rpc_is_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(NotificationNs.NotificationProvidersService.NotificationProvidersServiceClient),
            typeof(NotificationNs.NotificationProvidersService.NotificationProvidersServiceBase),
            rpcName);

    [Fact]
    public void Notification_channel_enum_has_the_specified_members() =>
        Assert.Equal(
            new[] { "Unspecified", "Email", "Sms" },
            Enum.GetNames<NotificationNs.NotificationChannel>());

    [Fact]
    public void Notification_messages_have_the_expected_generated_fields()
    {
        AssertFields<NotificationNs.ChannelProviders>(
            ("Channel", typeof(NotificationNs.NotificationChannel)), ("Primary", typeof(string)),
            ("Chain", typeof(Google.Protobuf.Collections.RepeatedField<string>)));
        AssertFields<NotificationNs.ProvidersReply>(
            ("Channels", typeof(Google.Protobuf.Collections.RepeatedField<NotificationNs.ChannelProviders>)));
        AssertFields<NotificationNs.UpdatePrimaryProviderRequest>(
            ("Channel", typeof(NotificationNs.NotificationChannel)), ("Provider", typeof(string)));
    }

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

    [Theory]
    [InlineData("ListSessionImages")]
    [InlineData("DownloadMedia")]
    public void Integration_Veriff_media_retrieval_rpcs_are_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.VeriffService.VeriffServiceClient),
            typeof(IntegrationNs.VeriffService.VeriffServiceBase),
            rpcName);

    [Fact]
    public void Integration_Veriff_media_retrieval_messages_have_the_expected_fields()
    {
        AssertFields<IntegrationNs.ListSessionImagesRequest>(
            ("SessionId", typeof(string)), ("BusinessId", typeof(string)),
            ("TenantId", typeof(string)), ("Type", typeof(IntegrationNs.VeriffVerificationType)));
        AssertFields<IntegrationNs.ListSessionImagesReply>(
            ("Images", typeof(Google.Protobuf.Collections.RepeatedField<IntegrationNs.SessionImage>)));
        AssertFields<IntegrationNs.SessionImage>(
            ("Id", typeof(string)), ("Name", typeof(string)), ("Context", typeof(string)),
            ("Size", typeof(long)), ("Mimetype", typeof(string)), ("SessionId", typeof(string)));
        AssertFields<IntegrationNs.DownloadMediaRequest>(
            ("MediaId", typeof(string)), ("SessionId", typeof(string)), ("BusinessId", typeof(string)),
            ("TenantId", typeof(string)), ("Type", typeof(IntegrationNs.VeriffVerificationType)));
        AssertFields<IntegrationNs.DownloadMediaChunk>(
            ("Content", typeof(Google.Protobuf.ByteString)), ("ContentType", typeof(string)));
        AssertFields<IntegrationNs.DecisionReply>(
            ("DocumentType", typeof(string)), ("DocumentCountry", typeof(string)),
            ("DocumentNumber", typeof(string)), ("DocumentValidFrom", typeof(string)),
            ("DocumentValidUntil", typeof(string)));
    }

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

    /// <summary>Standard Checkout (POST /v3/payments) - a collection, the counterpart to InitiateTransfer's payout.</summary>
    [Fact]
    public void Integration_Flutterwave_InitiatePayment_rpc_is_present_on_both_the_stub_and_the_base() =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceClient),
            typeof(IntegrationNs.FlutterwaveService.FlutterwaveServiceBase),
            "InitiatePayment");

    [Fact]
    public void Integration_Flutterwave_payment_messages_have_the_expected_generated_fields()
    {
        AssertFields<IntegrationNs.InitiatePaymentRequest>(
            ("BusinessId", typeof(string)), ("TenantId", typeof(string)),
            ("Payment", typeof(IntegrationNs.PaymentDetails)));
        AssertFields<IntegrationNs.PaymentDetails>(
            ("Amount", typeof(string)), ("Currency", typeof(string)), ("TxRef", typeof(string)),
            ("RedirectUrl", typeof(string)), ("Customer", typeof(IntegrationNs.PaymentCustomer)),
            ("Customizations", typeof(IntegrationNs.PaymentCustomizations)), ("MetaJson", typeof(string)),
            ("PaymentMethods", typeof(Google.Protobuf.Collections.RepeatedField<IntegrationNs.FlutterwavePaymentMethod>)));
        AssertFields<IntegrationNs.PaymentCustomer>(
            ("Email", typeof(string)), ("PhoneNumber", typeof(string)), ("Name", typeof(string)));
        AssertFields<IntegrationNs.PaymentCustomizations>(
            ("Title", typeof(string)), ("Logo", typeof(string)));
        AssertFields<IntegrationNs.PaymentLinkReply>(
            ("Link", typeof(string)), ("TxRef", typeof(string)));
    }

    [Fact]
    public void Integration_Flutterwave_transfer_payout_has_type_field()
    {
        AssertFields<IntegrationNs.InitiateTransferPayout>(
            ("Amount", typeof(string)), ("Currency", typeof(string)), ("AccountBank", typeof(string)),
            ("AccountNumber", typeof(string)), ("DebitSubAccount", typeof(string)), ("Narration", typeof(string)),
            ("Reference", typeof(string)), ("DebitCurrency", typeof(string)),
            ("DestinationBranchCode", typeof(string)), ("BeneficiaryName", typeof(string)),
            ("CallBackUrl", typeof(string)), ("MetaJson", typeof(string)),
            ("Type", typeof(IntegrationNs.FlutterwaveTransferType)));
    }

    [Fact]
    public void Integration_Flutterwave_transfer_type_enum_has_the_specified_members() =>
        Assert.Equal(
            new[] { "Unspecified", "BankTransfer", "MobileMoney" },
            Enum.GetNames<IntegrationNs.FlutterwaveTransferType>());

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
    public void Integration_Reliant_contract_generates_both_client_stub_and_service_base() =>
        AssertGeneratesBoth(
            typeof(IntegrationNs.ReliantService.ReliantServiceClient),
            typeof(IntegrationNs.ReliantService.ReliantServiceBase));

    [Theory]
    [InlineData("GetClient")]
    [InlineData("GetClientBalance")]
    [InlineData("GetBankAccount")]
    [InlineData("GetClientLedger")]
    [InlineData("GetReturns")]
    [InlineData("GetHeldClients")]
    [InlineData("GetTransactions")]
    [InlineData("GetDepositInfo")]
    [InlineData("GetDocumentStatus")]
    [InlineData("NewClient")]
    [InlineData("AddDocument")]
    [InlineData("UpdateDefaultBankAccount")]
    [InlineData("AddClientFunds")]
    [InlineData("AddPendingFunds")]
    [InlineData("AddFundsFromBankAccount")]
    [InlineData("PayRecurringPayee")]
    [InlineData("CloseClient")]
    [InlineData("CancelBankDraft")]
    [InlineData("WithdrawClientFunds")]
    [InlineData("TransferClientFunds")]
    [InlineData("AddWalletAddress")]
    [InlineData("AddFundsFromAddress")]
    public void Integration_Reliant_rpc_is_present_on_both_the_stub_and_the_base(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(IntegrationNs.ReliantService.ReliantServiceClient),
            typeof(IntegrationNs.ReliantService.ReliantServiceBase),
            rpcName);

    /// <summary>
    /// <see cref="IntegrationNs.NewClientRequest"/> carries the KYC/identity extensions as their
    /// own message types (<see cref="IntegrationNs.BeneficialOwner"/>,
    /// <see cref="IntegrationNs.InternationalInfo"/>, <see cref="IntegrationNs.ExistingKycResult"/>,
    /// <see cref="IntegrationNs.BankingIdentifier"/>, <see cref="IntegrationNs.GovernmentId"/>)
    /// rather than inlined fields, so they stay reusable and their field numbers stay independently
    /// stable if another Reliant message ever needs the same shape.
    /// </summary>
    [Fact]
    public void Integration_Reliant_NewClientRequest_carries_the_expected_nested_message_types()
    {
        var request = typeof(IntegrationNs.NewClientRequest);

        Assert.Equal(typeof(Google.Protobuf.Collections.RepeatedField<IntegrationNs.BeneficialOwner>),
            request.GetProperty("BeneficialOwners")!.PropertyType);
        Assert.Equal(typeof(IntegrationNs.InternationalInfo), request.GetProperty("International")!.PropertyType);
        Assert.Equal(typeof(IntegrationNs.ExistingKycResult), request.GetProperty("ExistingKycResult")!.PropertyType);
        Assert.Equal(typeof(Google.Protobuf.Collections.RepeatedField<IntegrationNs.BankingIdentifier>),
            request.GetProperty("BankingIdentifiers")!.PropertyType);
        Assert.Equal(typeof(Google.Protobuf.Collections.RepeatedField<IntegrationNs.GovernmentId>),
            request.GetProperty("GovernmentIds")!.PropertyType);
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

    [Theory]
    [InlineData("StartIdentityVerification")]
    [InlineData("UploadIdentityVerificationDocument")]
    [InlineData("SubmitIdentityVerification")]
    [InlineData("PollIdentityVerificationDecision")]
    [InlineData("GetIdentityVerificationSession")]
    public void Compliance_identity_verification_rpcs_generate_client_and_service_methods(string rpcName) =>
        AssertRpcPresentOnBoth(
            typeof(ComplianceNs.ComplianceService.ComplianceServiceClient),
            typeof(ComplianceNs.ComplianceService.ComplianceServiceBase), rpcName);

    [Fact]
    public void Compliance_identity_verification_messages_have_the_expected_generated_fields()
    {
        AssertFields<ComplianceNs.StartIdentityVerificationRequest>(
            ("SubjectId", typeof(string)), ("CountryCode", typeof(string)),
            ("Subject", typeof(ComplianceNs.KycSubject)), ("CallbackUrl", typeof(string)));
        AssertFields<ComplianceNs.IdentityVerificationSessionReply>(
            ("ProfileId", typeof(string)), ("RoundId", typeof(string)),
            ("SessionId", typeof(string)), ("Status", typeof(ComplianceNs.KycVerificationStatus)),
            ("Url", typeof(string)),
            ("SubmittedSides", typeof(Google.Protobuf.Collections.RepeatedField<ComplianceNs.IdentityDocumentSide>)));
        AssertFields<ComplianceNs.UploadIdentityVerificationDocumentRequest>(
            ("SubjectId", typeof(string)), ("SessionId", typeof(string)),
            ("Side", typeof(ComplianceNs.IdentityDocumentSide)), ("DocumentType", typeof(string)),
            ("Content", typeof(Google.Protobuf.ByteString)), ("ContentType", typeof(string)));
        AssertFields<ComplianceNs.IdentityDocumentReceipt>(
            ("SessionId", typeof(string)), ("Side", typeof(ComplianceNs.IdentityDocumentSide)),
            ("SubmittedSides", typeof(Google.Protobuf.Collections.RepeatedField<ComplianceNs.IdentityDocumentSide>)));
        AssertFields<ComplianceNs.SubmitIdentityVerificationRequest>(
            ("SubjectId", typeof(string)), ("SessionId", typeof(string)));
        AssertFields<ComplianceNs.SubmitIdentityVerificationReply>(
            ("SessionId", typeof(string)), ("Status", typeof(ComplianceNs.KycVerificationStatus)),
            ("Submitted", typeof(bool)), ("SubmittedAt", typeof(string)));
        AssertFields<ComplianceNs.PollIdentityVerificationDecisionRequest>(
            ("SubjectId", typeof(string)), ("SessionId", typeof(string)));
        AssertFields<ComplianceNs.PollIdentityVerificationDecisionReply>(
            ("SessionId", typeof(string)), ("Status", typeof(ComplianceNs.KycVerificationStatus)),
            ("DecisionAvailable", typeof(bool)), ("Applied", typeof(bool)),
            ("DecidedAt", typeof(string)), ("DecisionCode", typeof(int)),
            ("DecisionReason", typeof(string)));
        AssertFields<ComplianceNs.GetIdentityVerificationSessionRequest>(
            ("SubjectId", typeof(string)), ("SessionId", typeof(string)));
        AssertFields<ComplianceNs.IdentityVerificationSessionDetailsReply>(
            ("SessionId", typeof(string)), ("Status", typeof(ComplianceNs.KycVerificationStatus)),
            ("DocumentType", typeof(string)), ("Submitted", typeof(bool)),
            ("SubmittedAt", typeof(string)),
            ("SubmittedSides", typeof(Google.Protobuf.Collections.RepeatedField<ComplianceNs.IdentityDocumentSide>)),
            ("Media", typeof(Google.Protobuf.Collections.RepeatedField<ComplianceNs.IdentityVerificationMediaReply>)));
        AssertFields<ComplianceNs.IdentityVerificationMediaReply>(
            ("MediaId", typeof(string)), ("Side", typeof(ComplianceNs.IdentityDocumentSide)),
            ("DocumentType", typeof(string)), ("Submitted", typeof(bool)),
            ("SubmittedAt", typeof(string)), ("ContentType", typeof(string)),
            ("SizeBytes", typeof(long)), ("ReviewStatus", typeof(string)),
            ("DecisionSource", typeof(string)), ("DecisionReason", typeof(string)),
            ("DecidedAt", typeof(string)), ("DocumentCountryCode", typeof(string)));
        Assert.Equal(new[] { "Unspecified", "Front", "Back", "Face" },
            Enum.GetNames<ComplianceNs.IdentityDocumentSide>());
    }

    private static void AssertFields<T>(params (string Name, Type Type)[] fields)
    {
        foreach (var (name, type) in fields)
        {
            Assert.Equal(type, typeof(T).GetProperty(name)?.PropertyType);
        }
    }

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
