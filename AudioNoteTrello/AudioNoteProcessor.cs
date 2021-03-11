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

            var csApiKey = await SecureStorage.GetAsync("CognitiveServiceApiKey");
            var tApiKey = await SecureStorage.GetAsync("TrelloApiKey");

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

            log("Done");
        }
    }
}