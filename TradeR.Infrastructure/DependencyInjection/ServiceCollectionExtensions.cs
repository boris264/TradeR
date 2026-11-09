using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeR.Infrastructure.Clients;
using TradeR.Infrastructure.Configurations.Kraken;
using TradeR.Infrastructure.Interfaces;

namespace TradeR.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="IKrakenRESTApiClient"/> and its named <see cref="System.Net.Http.HttpClient"/>
        /// (<see cref="KrakenRESTApiClient.HttpClientName"/>), with retries, a circuit breaker and timeouts.
        /// Settings come from the optional <c>Kraken</c> configuration section.
        /// </summary>
        public static IServiceCollection AddKrakenRESTApi(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddOptions<KrakenRESTApiConfiguration>()
                .Bind(configuration.GetSection(KrakenRESTApiConfiguration.SectionName))
                .Validate(
                    settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                    $"{KrakenRESTApiConfiguration.SectionName}:BaseUrl must be an absolute URL.")
                .ValidateOnStart();

            services
                .AddHttpClient(KrakenRESTApiClient.HttpClientName, (serviceProvider, client) =>
                {
                    KrakenRESTApiConfiguration settings = serviceProvider
                        .GetRequiredService<IOptions<KrakenRESTApiConfiguration>>()
                        .Value;

                    // A trailing slash makes relative paths like "0/public/OHLC" resolve under the base URL.
                    string baseUrl = settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/";
                    client.BaseAddress = new Uri(baseUrl);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("TradeR");
                })
                .AddStandardResilienceHandler();

            services.AddTransient<IKrakenRESTApiClient, KrakenRESTApiClient>();

            return services;
        }
    }
}
