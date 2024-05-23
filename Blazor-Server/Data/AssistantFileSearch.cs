using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Syncfusion.Blazor.Notifications;
using static System.Net.WebRequestMethods;

namespace Blazor_Server.Data
{
    public class AssistantFileSearch
    {
        private static readonly HttpClient httpClient = new HttpClient();

        private string OPENAI_KEY="";

        private const string API_URL = "https://api.openai.com/v1";

        public string threadId = "";

        public string assistantId = "";

        public  async Task Search(SfToast toast)
        {
            // get the key from user secrets
            var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            OPENAI_KEY = config["openaiapikey"];

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {OPENAI_KEY}");
            httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");




            // Create Assistant
            var assistant = await CreateAssistant();
            await toast.ShowAsync(new ToastModel { Title = "Assistant Created", Content = "Created a new Assistant with id"+assistant.Id, CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            assistantId = assistant.Id;

            // Create Vector Store 
            var vectorStore = await CreateVectorStore();
            await toast.ShowAsync(new ToastModel { Title = "Vector Store Created", Content = "Created a new Vector store with id" + vectorStore.Id, CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            // Upload the file
            var fileId = await UploadFile("DataFiles/SyncBlazor.txt", "assistants");
            await toast.ShowAsync(new ToastModel { Title = "File Uploaded", Content = "File uploaded with id" + fileId, CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            // Add the file to the vector store

            await AddFileToVectorStore(vectorStore.Id, fileId);
            await toast.ShowAsync(new ToastModel { Title = "File added to Vector store", Content = "File linked with vector store", CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            //Update Assistant with Vector Store

            var newAssist = await UpdateAssistant(assistant.Id,vectorStore.Id);
            await toast.ShowAsync(new ToastModel { Title = "Link Vector", Content = "Linked Vector store to assistant", CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            // Create a thread
            threadId = await CreateThread(fileId, vectorStore.Id);
            await toast.ShowAsync(new ToastModel { Title = "Create Thread", Content = "Create a new Thread for the assistant" , CssClass = "e-toast-success", Icon = "e-info toast-icons" });


            // Create a Run for the thread
            var runId =await CreateRun(assistantId, threadId,toast);
            await toast.ShowAsync(new ToastModel { Title = "Create a Run", Content = "Run the thread on the assistant", CssClass = "e-toast-success", Icon = "e-info toast-icons" });

        }

        private static async Task<AssistantResponse> CreateAssistant()
        {
            var openAIPrompt = new
            {
                name = "Syncfusion Feature Assistant",
                instructions = "You are an expert in Syncfusion features and can answer anything related to syncfusion, also always keep the conversation about Syncfuison even if user asks any non related questions divert back them to ask syncfuison related questions.",
                model = "gpt-4-turbo",
                tools = new[] { new { type = "file_search" } }
            };

            var content = new StringContent(JsonSerializer.Serialize(openAIPrompt), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/assistants", content);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AssistantResponse>(jsonContent);
        }

        private static async Task<VectorStoreResponse> CreateVectorStore()
        {
            var vectorStoreData = new { name = "Syncfusion Blazor" };
            var content = new StringContent(JsonSerializer.Serialize(vectorStoreData), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/vector_stores", content);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<VectorStoreResponse>(jsonContent);
        }

        private static async Task<string> UploadFile(string filePath, string purpose)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open);
            var formData = new MultipartFormDataContent();
            formData.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));
            formData.Add(new StringContent(purpose), "purpose");

            var response = await httpClient.PostAsync($"{API_URL}/files", formData);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var fileResponse = JsonSerializer.Deserialize<FileResponse>(jsonContent);
            return fileResponse.Id;
        }


        static async Task AddFileToVectorStore(string vectorid,string fileId)
        {
            var VECTOR_STORE_ID = vectorid;
            var requestUrl = $"{API_URL}/vector_stores/{VECTOR_STORE_ID}/files";
            var requestData = new { file_id = fileId };
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUrl, content);
            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync();
        }

        private static async Task<AssistantResponse> UpdateAssistant(string assistantID, string vectoreStoreId)
        {
            var openAIPrompt = new
            {
                tool_resources = new
                {
                    file_search = new
                    {
                        vector_store_ids= new[] { vectoreStoreId }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(openAIPrompt), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/assistants/{assistantID}", content);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AssistantResponse>(jsonContent);
        }

        private static async Task<string> CreateThread(string fileId, string vectorID)
        {

            var thread = new
            {
               
                tool_resources = new
                {
                    file_search = new
                    {
                        vector_store_ids = new[] { vectorID },
                    }
                }
            };


            var content = new StringContent(JsonSerializer.Serialize(thread), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/threads", content);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var threadResponse = JsonSerializer.Deserialize<ThreadResponse>(jsonContent);
            return threadResponse.Id;
        }

        private static async Task<String> CreateRun(string assistantId, string threadId, SfToast toast)
        {

            var runContent = new { assistant_id = assistantId };
            var content = new StringContent(JsonSerializer.Serialize(runContent), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/threads/{threadId}/runs", content);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var runResponse = JsonSerializer.Deserialize<RunResponse>(jsonContent);
            var runID = runResponse.Id;

            while(runResponse.Status!="completed")
            {
                response = await httpClient.GetAsync($"{API_URL}/threads/{threadId}/runs/{runID}");
                response.EnsureSuccessStatusCode();
                jsonContent = await response.Content.ReadAsStringAsync();
                runResponse = JsonSerializer.Deserialize<RunResponse>(jsonContent);
                if(toast!=null)
                {
                await toast.ShowAsync(new ToastModel { Title = "Waiting!", Content = "Waiting for the run to complete", CssClass = "e-toast-info", Icon = "e-info toast-icons" });

                }
                await Task.Delay(1000);
            }


            return runID;
        }



        public async Task<string> GetMessages(SfToast toast)
        {
            var response = await httpClient.GetAsync($"{API_URL}/threads/{threadId}/messages");
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var threadResponse = JsonSerializer.Deserialize<ThreadMessageResponse>(jsonContent);

            var messages = threadResponse.Data;
            var message = messages[0];

            var text1 = "";
            if (message.Content[0].Type == "text")
            {
                text1 = message.Content[0].Text.Value;               
            }

            if (toast != null) { 
                await toast.ShowAsync(new ToastModel { Title = "Success!", Content = "Retrieved message from the thread", CssClass = "e-toast-success", Icon = "e-success toast-icons" });

            }

            return text1;
        }

        public async Task AskQuestionToAssistant(string query,SfToast toast)
        {

            var message = new
            {
                role = "user",
                content = query,
            };
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"{API_URL}/threads/{threadId}/messages",content);
            response.EnsureSuccessStatusCode();
            await toast.ShowAsync(new ToastModel { Title = "Success!", Content = "User Message added to the thread", CssClass = "e-toast-success", Icon = "e-success toast-icons" });


            var runId = await CreateRun(assistantId, threadId,toast);

        }

        private static async Task<FileResponse> RetrieveFile(string fileId)
        {
            var response = await httpClient.GetAsync($"{API_URL}/files/{fileId}");
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FileResponse>(jsonContent);
        }

    }





    // Define response classes
    public class AssistantResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public class VectorStoreResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public class FileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("filename")]
        public string Filename { get; set; }
    }

    public class ThreadResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    public class RunResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("messages")]
        public System.Collections.Generic.List<Message1> Messages { get; set; }
    }

    public class Message1
    {
        [JsonPropertyName("content")]
        public Content[] Content { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("annotations")]
        public Annotation[] Annotations { get; set; }
    }

    public class Annotation
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("filecitation")]
        public FileCitation FileCitation { get; set; }
    }

    public class FileCitation
    {
        [JsonPropertyName("fileid")]
        public string FileId { get; set; }
    }

    public class ThreadMessageResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("data")]
        public List<MessageData> Data { get; set; }

        [JsonPropertyName("firstid")]
        public string FirstId { get; set; }

        [JsonPropertyName("lastid")]
        public string LastId { get; set; }

        [JsonPropertyName("hasmore")]
        public bool HasMore { get; set; }
    }

    public class MessageData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("createdat")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("assistantid")]
        public string AssistantId { get; set; }

        [JsonPropertyName("threadid")]
        public string ThreadId { get; set; }

        [JsonPropertyName("runid")]
        public string RunId { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; }

        [JsonPropertyName("attachments")]
        public List<object> Attachments { get; set; }


        //public Dictionary<string, object> Metadata { get; set; }
    }

    public class ContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("text")]
        public TextItem Text { get; set; }
    }

    public class TextItem
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("annotations")]
        public List<Annotation> Annotations { get; set; }
    }
}
