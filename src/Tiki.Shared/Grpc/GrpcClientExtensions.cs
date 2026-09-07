using Microsoft.Extensions.DependencyInjection;

namespace Tiki.Shared.Grpc;

public static class GrpcClientExtensions
{
    /// <summary>
    /// Registers a typed gRPC client with <see cref="ServiceSigningClientInterceptor"/>
    /// already attached, so every outbound call is signed with zero per-call code.
    /// Requires <c>AddTikiServiceAuth()</c> to have registered
    /// <see cref="Auth.IServiceRequestSigner"/>.
    /// </summary>
    public static IHttpClientBuilder AddTikiGrpcClient<TClient>(this IServiceCollection services, Uri address)
        where TClient : class
    {
        services.AddSingleton<ServiceSigningClientInterceptor>();

        return services.AddGrpcClient<TClient>(options => options.Address = address)
            .AddInterceptor<ServiceSigningClientInterceptor>();
    }
}
