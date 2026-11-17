using Polly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Clients.Kraken.Json;
using TradeR.Infrastructure.Errors;
using TradeR.Infrastructure.Interfaces;
using TradeR.Infrastructure.Models.Kraken;
using TradeR.SharedKernel.Results;

namespace TradeR.Infrastructure.Clients
{
    public class KrakenRESTApiClient : IKrakenRESTApiClient
    {
        /// <summary>Name of the <see cref="HttpClient"/> registered for Kraken's REST API.</summary>
        public const string HttpClientName = "KrakenRESTApi";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            Converters = { new KrakenOhlcResultConverter() }
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public KrakenRESTApiClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<Result<ApiKrakenOhlcResult>> GetOhlcAsync(
            string pair,
            ApiKrakenOhlcInterval interval,
            long? since = null,
            ApiKrakenAssetVersion assetVersion = ApiKrakenAssetVersion.Version1,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair);

            string requestUri = string.Create(
                CultureInfo.InvariantCulture,
                $"0/public/OHLC?pair={Uri.EscapeDataString(pair)}&interval={(int)interval}");

            if (since.HasValue)
            {
                requestUri += string.Create(CultureInfo.InvariantCulture, $"&since={since.Value}");
            }

            // Legacy naming is Kraken's default and is requested by leaving the parameter out.
            if (assetVersion != ApiKrakenAssetVersion.Legacy)
            {
                requestUri += string.Create(CultureInfo.InvariantCulture, $"&assetVersion={(int)assetVersion}");
            }

            try
            {
                HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

                using HttpResponseMessage response = await client.GetAsync(
                    requestUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return KrakenErrors.Http(response.StatusCode);
                }

                await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
                KrakenResponse<ApiKrakenOhlcResult>? body = await JsonSerializer.DeserializeAsync<KrakenResponse<ApiKrakenOhlcResult>>(
                    content,
                    JsonOptions,
                    cancellationToken);

                if (body is null)
                {
                    return KrakenErrors.InvalidResponse("Kraken returned an empty response.");
                }

                List<string> errors = GetErrors(body.Error);
                if (errors.Count > 0)
                {
                    return KrakenErrors.Api(errors);
                }

                if (body.Result is null)
                {
                    return KrakenErrors.NoData(pair);
                }

                return body.Result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return KrakenErrors.Unavailable("The request to Kraken timed out.");
            }
            catch (ExecutionRejectedException ex)
            {
                // Polly rejected the call: total timeout reached or circuit breaker open after repeated failures.
                return KrakenErrors.Unavailable(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return KrakenErrors.Unavailable(ex.Message);
            }
            catch (JsonException ex)
            {
                return KrakenErrors.InvalidResponse(ex.Message);
            }
            catch (FormatException ex)
            {
                return KrakenErrors.InvalidResponse(ex.Message);
            }
        }

        /// <summary>Kraken prefixes errors with <c>E</c> (e.g. <c>EQuery:...</c>) and warnings with <c>W</c>; only errors fail the call.</summary>
        private static List<string> GetErrors(string[] messages)
        {
            List<string> errors = new List<string>();

            foreach (string message in messages)
            {
                if (message.StartsWith('E'))
                {
                    errors.Add(message);
                }
            }

            return errors;
        }
    }
}
