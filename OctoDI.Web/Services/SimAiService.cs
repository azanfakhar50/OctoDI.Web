using Newtonsoft.Json;
using OctoDI.Web.Services;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace OctoDI.Web.Services
{
    public class SimAiService : ISimAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public SimAiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["SimAI:ApiKey"];
        }

        public async Task<string> GetChatResponseAsync(string message)
        {
            try
            {
                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Authorization header is already set in Program.cs
                var response = await _httpClient.PostAsync("chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return $"Error: {response.StatusCode} - {errorContent}";
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<SimAiResponse>(responseString);

                return result?.Choices?[0]?.Message?.Content ?? "No response received";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    // Response models for SimAI
    public class SimAiResponse
    {
        public List<Choice> Choices { get; set; }
    }

    public class Choice
    {
        public Message Message { get; set; }
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}