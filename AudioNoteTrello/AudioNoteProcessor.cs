using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace AudioNoteTrello
{
    class AudioNoteProcessor
    {
        const string SpeechUri = "speech/recognition/conversation/cognitiveservices/v1?language=it-IT&format=detailed";
        const string SpeechRegion = "westus";

        public static async Task ProcessAsync(string filePath, Action<string> log)
        {
            log($"***** Started processing\n{filePath}");

            var csApiKey = await SecureStorage.GetAsync("CognitiveServiceApiKey");
            var tApiKey = await SecureStorage.GetAsync("TrelloApiKey");
            var tListId = await SecureStorage.GetAsync("TrelloListId");
            var tApiToken = await SecureStorage.GetAsync("TrelloApiToken");

            if(string.IsNullOrWhiteSpace(csApiKey))
            {
                log("Unable to find CognitiveServiceApiKey, set values first");
                return;
            }

            if(string.IsNullOrWhiteSpace(tApiKey))
            {
                log("Unable to find TrelloApiKey, set values first");
                return;
            }

            if(string.IsNullOrWhiteSpace(tListId))
            {
                log("Unable to find TrelloListId, set values first");
                return;
            }

            if(string.IsNullOrWhiteSpace(tApiToken))
            {
                log("Unable to find TrelloApiToken, set values first");
                return;
            }

            await ProcessInternalAsync(filePath, log, csApiKey, tApiKey, tListId, tApiToken);
        }

        public static async Task ProcessInternalAsync(string filePath, Action<string> log, string csApiKey, string tApiKey, string tListId, string tApiToken)
        {
            var audioText = await SpeachToText(filePath, csApiKey, log);

            log("***** STT");
            log(audioText);

            var content = await CreateTrelloCard(filePath, tApiKey, tListId, tApiToken, audioText);

            log("***** Trello");
            log(content.Substring(0, 20));

            log("***** Done");
        }

        static async Task<string> CreateTrelloCard(string filePath, string apiKey, string listId, string token, string text)
        {
            using var trelloClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.trello.com"),
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("OAuth",
                        $"oauth_consumer_key=\"{apiKey}\", oauth_token=\"{token}\"")
                }
            };

            var formData = new MultipartFormDataContent
            {
                {new StringContent(listId), "\"idList\""},
                {new StringContent(text), "\"name\""},
                {new StringContent("Note from AudioNoteTrello"), "\"desc\""},
                {new StreamContent(File.OpenRead(filePath)), "\"fileSource\"", $"note.{filePath[^3..]}"}
            };

            var result = await trelloClient.PostAsync("1/cards", formData);

            return await result.Content.ReadAsStringAsync();
        }

        static async Task<string> SpeachToText(string filePath, string apiKey, Action<string> log)
        {
            using var speechClient = new HttpClient
            {
                BaseAddress = new Uri($"https://{SpeechRegion}.stt.speech.microsoft.com"),
                DefaultRequestHeaders =
                {
                    {"Ocp-Apim-Subscription-Key", apiKey}
                }
            };

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            var response = await speechClient.PostAsync(SpeechUri, new StreamContent(fileStream));

            log($"***** STT response");
            log($"{(int)response.StatusCode} {response.StatusCode}");
            log($"{response.ReasonPhrase}");
            log($"Success:{response.IsSuccessStatusCode}");
            log(string.Join('\n', response.Headers.Select(x => $"{x.Key}: {string.Join(", ", x.Value)}\n")));

            var responseContent = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<SttResponse>(responseContent)
                              .NBest
                              .FirstOrDefault()?
                              .Display ?? "Unable to parse";
        }
    }

    public class NBest
    {
        public double Confidence { get; set; }
        public string Lexical { get; set; }
        public string ITN { get; set; }
        public string MaskedITN { get; set; }
        public string Display { get; set; }
    }

    public class SttResponse
    {
        public string RecognitionStatus { get; set; }
        public int Offset { get; set; }
        public int Duration { get; set; }
        public List<NBest> NBest { get; set; }
    }
}