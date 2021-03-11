using System;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace AudioNoteTrello
{
    class AudioNoteProcessor
    {
        public static async Task ProcessAsync(string filePath, Action<string> log)
        {
            log($"Started processing {filePath}");

            var apiKey = await SecureStorage.GetAsync("CognitiveServiceApiKey");

            if(string.IsNullOrWhiteSpace(apiKey))
            {
                log("Unable to find CognitiveServiceApiKey, set values first");
                return;
            }

            log(apiKey);

            log("Done");
        }
    }
}