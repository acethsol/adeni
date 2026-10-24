namespace Adeni.Api.Extensions;

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddAdeniForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
            || configuration.GetValue<bool>("FORWARDEDHEADERS_ENABLED"))
        {
            throw new InvalidOperationException("Disable ASPNETCORE_FORWARDEDHEADERS_ENABLED and configure ReverseProxy explicitly to avoid duplicate automatic forwarding.");
        }

        var settings = configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new();
        var proxies = new List<IPAddress>();
        var networks = new List<System.Net.IPNetwork>();
        if (settings.Enabled)
        {
            if (settings.ForwardLimit < 1 || settings.KnownProxies.Length + settings.KnownNetworks.Length == 0)
            {
                throw new InvalidOperationException("ReverseProxy requires a positive ForwardLimit and explicit KnownProxies or KnownNetworks when enabled.");
            }

            foreach (var value in settings.KnownProxies)
            {
                if (!IPAddress.TryParse(value, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                {
                    throw new InvalidOperationException("ReverseProxy:KnownProxies must contain specific IP addresses.");
                }

                proxies.Add(address);
            }

            foreach (var value in settings.KnownNetworks)
            {
                if (!System.Net.IPNetwork.TryParse(value, out var network) || network.PrefixLength == 0
                    || (network.PrefixLength <= 96 && network.Contains(IPAddress.Parse("::ffff:0:0"))))
                {
                    throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain CIDR networks narrower than /0.");
                }

                networks.Add(network);
            }
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Empty trust lists with forwarding enabled would trust every sender.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.ForwardedHeaders = settings.Enabled
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = settings.ForwardLimit;
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
        return services;
    }
}

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";
    public bool Enabled { get; set; }
    public int ForwardLimit { get; set; } = 1;
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}
