using Microsoft.AspNetCore.Identity;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Blazor_Server.Data
{
    public class ChatGPTEngine
    {
        private HttpClient httpClient { get; set; }

        private string OPENAI_KEY = "";// Add a valid OpenAI key here.

        private string OPENAI_MODEL = "gpt-3.5-turbo-instruct";

        private string API_Chat_ENDPOINT = "https://api.openai.com/v1/chat/completions";

        private string API_ENDPOINT = "https://api.openai.com/v1/completions";

        public List<Message> myMessage = new List<Message>();

        public ChatGPTEngine()
        {
            // get the key from user secrets
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            OPENAI_KEY = config["openaiapikey"];

            httpClient = new HttpClient();

        }

        internal async Task<string> ProcessTheUserInput(string prompt)
        {
            var val = new AuthenticationHeaderValue("Bearer", OPENAI_KEY);
            httpClient.DefaultRequestHeaders.Authorization = val;
            var openAIPrompt = new
            {
                model = OPENAI_MODEL,
                prompt,
                temperature = 0.5,
                max_tokens = 1500,
                top_p = 1,
                frequency_penalty = 0,
                presence_penalty = 0
            };

            var content = new StringContent(JsonSerializer.Serialize(openAIPrompt), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(API_ENDPOINT, content);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var choices = JsonDocument.Parse(jsonContent).RootElement.GetProperty("choices").GetRawText();
            var result = JsonDocument.Parse(Regex.Replace(choices, @"[\[\]]", string.Empty)).RootElement;
            return result.GetProperty("text").GetString();
        }        
        

        internal async Task<string> ProcessTheGivenInfoWithContext(string aiAnswer, string userQuestion)
        {
            var val = new AuthenticationHeaderValue("Bearer", OPENAI_KEY);
            httpClient.DefaultRequestHeaders.Authorization = val;
            myMessage.Add(new Message { role = "system", content = aiAnswer });
            myMessage.Add(new Message { role = "user", content = userQuestion });
            var openAIPrompt = new
            {
                model = "gpt-3.5-turbo",
                messages = myMessage,
                temperature = 0.5,
                max_tokens = 1500,
                top_p = 1,
                frequency_penalty = 0,
                presence_penalty = 0
            };

            var content = new StringContent(JsonSerializer.Serialize(openAIPrompt), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(API_Chat_ENDPOINT, content);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var choices = JsonDocument.Parse(jsonContent).RootElement.GetProperty("choices")[0].GetProperty("message").GetRawText();
            var result = JsonDocument.Parse(Regex.Replace(choices, @"[\[\]]", string.Empty)).RootElement;
            var result1 = result.ToString().Replace("\n", "<br>");
            return result.GetProperty("content").GetString();
        }

        

        
    }



    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }

}
