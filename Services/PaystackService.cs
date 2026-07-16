using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FINAPSA.Services
{
    public class PaystackService
    {
        private readonly HttpClient _http;
        private readonly string _secretKey;

        public PaystackService(IConfiguration config, HttpClient http)
        {
            _http      = http;
            _secretKey = config["Paystack:SecretKey"]
                         ?? throw new InvalidOperationException("Paystack SecretKey not configured.");

            _http.BaseAddress = new Uri("https://api.paystack.co/");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _secretKey);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Initialize a transaction with Paystack.
        /// Returns the authorization_url to redirect the user to,
        /// and the reference to store for verification.
        /// </summary>
        public async Task<PaystackInitResponse?> InitializeAsync(
            string email, decimal amountNaira, string reference, string callbackUrl)
        {
            // Paystack expects amount in KOBO (multiply by 100)
            var body = new
            {
                email,
                amount      = (long)(amountNaira * 100),
                reference,
                callback_url = callbackUrl,
                currency    = "NGN"
            };

            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("transaction/initialize", content);
            var raw      = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<PaystackInitResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        /// <summary>
        /// Verify a transaction after Paystack redirects back.
        /// Returns the verification result with status and amount paid.
        /// </summary>
        public async Task<PaystackVerifyResponse?> VerifyAsync(string reference)
        {
            var response = await _http.GetAsync($"transaction/verify/{reference}");
            var raw      = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<PaystackVerifyResponse>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class PaystackInitResponse
    {
        public bool   Status  { get; set; }
        public string? Message { get; set; }
        public PaystackInitData? Data { get; set; }
    }

    public class PaystackInitData
    {
        public string? Authorization_url { get; set; }
        public string? Access_code       { get; set; }
        public string? Reference         { get; set; }
    }

    public class PaystackVerifyResponse
    {
        public bool   Status  { get; set; }
        public string? Message { get; set; }
        public PaystackVerifyData? Data { get; set; }
    }

    public class PaystackVerifyData
    {
        public string? Status    { get; set; }   // "success", "failed", "abandoned"
        public string? Reference { get; set; }
        public long    Amount    { get; set; }   // in kobo
        public string? Currency  { get; set; }
        public PaystackCustomer? Customer { get; set; }
    }

    public class PaystackCustomer
    {
        public string? Email { get; set; }
    }
}
