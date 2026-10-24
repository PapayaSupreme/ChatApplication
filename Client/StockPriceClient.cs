using System.Net.Http.Headers;
using System.Text.Json;

namespace ChatApplication.Client;

internal static class StockPriceClient
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ChatApplication", "1.0"));
        return client;
    }

    public static bool TryParseCommand(string input, out string ticker)
    {
        string[] parts = input.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 2 &&
            parts[0].Equals("stock", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Length <= 15 &&
            parts[1].All(character =>
                char.IsLetterOrDigit(character) ||
                character is '.' or '-' or '^'))
        {
            ticker = parts[1].ToUpperInvariant();
            return true;
        }

        ticker = string.Empty;
        return false;
    }

    public static async Task<decimal> GetCurrentPriceAsync(string ticker)
    {
        string encodedTicker = Uri.EscapeDataString(ticker);
        using HttpResponseMessage httpResponse = await HttpClient.GetAsync(
            $"https://query1.finance.yahoo.com/v8/finance/chart/{encodedTicker}?range=1d&interval=1m");
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Stock service returned HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}).");
        }

        await using Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();
        using JsonDocument response = await JsonDocument.ParseAsync(responseStream);

        if (!response.RootElement.TryGetProperty("chart", out JsonElement chart) ||
            !chart.TryGetProperty("result", out JsonElement results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0 ||
            !results[0].TryGetProperty("meta", out JsonElement meta) ||
            !meta.TryGetProperty("regularMarketPrice", out JsonElement price) ||
            !price.TryGetDecimal(out decimal currentPrice))
        {
            throw new InvalidOperationException(
                $"No current value was found for {ticker}.");
        }

        return currentPrice;
    }
}
