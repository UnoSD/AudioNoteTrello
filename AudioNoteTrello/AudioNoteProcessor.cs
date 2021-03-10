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

            log(await SecureStorage.GetAsync("CognitiveServiceApi"));

            log("done");
        }
    }
}