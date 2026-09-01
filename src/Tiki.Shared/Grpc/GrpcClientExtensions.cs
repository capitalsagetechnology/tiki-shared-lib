using Microsoft.Extensions.DependencyInjection;

namespace Tiki.Shared.Grpc;

public static class GrpcClientExtensions
{
    /// <summary>
    /// Registers a typed gRPC client with <see cref="ServiceTokenClientInterceptor"/>
    /// already attached, so every outbound call carries this service's token with zero
    /// per-call code. Requires <c>IServiceTokenProvider</c> to already be registered
    /// (typically <c>HmacServiceTokenProvider</c> for now) — swapping that registration to
    /// the real OAuth2 provider later requires no change here.
    /// </summary>
    public static IHttpClientBuilder AddTikiGrpcClient<TClient>(this IServiceCollection services, Uri address)
        where TClient : class
    {
        services.AddSingleton<ServiceTokenClientInterceptor>();

        return services.AddGrpcClient<TClient>(options => options.Address = address)
            .AddInterceptor<ServiceTokenClientInterceptor>();
    }
}
