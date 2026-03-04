using System.Text;
using System.Text.Json;

namespace Buffet_Restaurant_Managment_System_API.Services
{
    public class PromptPayService
    {

        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;

        public PromptPayService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("API_KEY_PAYMENT");
        }

        public async Task<string> GeneratePromptPayQr(decimal amount)
        {
            Console.WriteLine($"=== API KEY VALUE: [{_apiKey}] ===");

            var url = "https://api.inwcloud.shop/v1/promptpay/generate";
            var payload = new { amount = amount };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            Console.WriteLine($"Check API Key: {_apiKey}");

            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return $"Error: {response.StatusCode}";
        }
        public async Task<string> CheckPaymentStatus(string transactionId)
        {
            var url = "https://api.inwcloud.shop/v1/promptpay/check";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var payload = new { transactionId = transactionId };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return result;
            }

            return $"Error: {response.StatusCode} - {result}";
        }
    }
}