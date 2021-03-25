using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Google.Android.Material.Snackbar;
using Xamarin.Essentials;
using Environment = Android.OS.Environment;

namespace AudioNoteTrello
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        TextView _logView;

        void SetButtonClick(int id, Func<Task> onClick) =>
            FindViewById<Button>(id)!.Click +=
                async (_, __) => await onClick();

        void SetSecretButtonClick(int id, string secretName, TextView apiText) =>
            SetButtonClick(id, () => SecureStorage.SetAsync(secretName, apiText.Text));

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            FindViewById<Button>(Resource.Id.record)!.Touch += RecordButtonTouch;

            var apiText = FindViewById<EditText>(Resource.Id.apiValue)!;

            _logView = FindViewById<TextView>(Resource.Id.logView);

            SetSecretButtonClick(Resource.Id.setCognitiveApiKey, "CognitiveServiceApiKey", apiText);
            SetSecretButtonClick(Resource.Id.setTrelloApiKey, "TrelloApiKey", apiText);
            SetSecretButtonClick(Resource.Id.setTrelloListId, "TrelloListId", apiText);
            SetSecretButtonClick(Resource.Id.setTrelloApiToken, "TrelloApiToken", apiText);
        }

        async void RecordButtonTouch(object sender, View.TouchEventArgs e)
        {
            switch (e.Event?.Action & MotionEventActions.Mask)
            {
                case MotionEventActions.Down:
                    if (HasPermissionToRecord(this))
                    {
                        StartRecording();
                        Message(sender, "Started recording");
                    }
                    else
                        PerformRuntimePermissionsCheckForRecording(this);

                    break;

                case MotionEventActions.Up:
                    await StopRecording();
                    break;

                default:
                    return;
            }
        }

        const string Ext = "wav";

        void StartRecording()
        {
            _cts = new CancellationTokenSource();
            
            _task = Task.Run(async () =>
            {
                var minBufferSize = AudioTrack.GetMinBufferSize(16000, ChannelOut.Mono, Encoding.Pcm16bit); 

                var recorder = new AudioRecord(AudioSource.Mic, 16000, ChannelIn.Mono, Encoding.Pcm16bit, minBufferSize);
      
                recorder.StartRecording();

                var audioBuffer = new byte[minBufferSize];

                await using var fileStream = 
                    new FileStream(GetFileNameForRecording(this, Ext),
                                   FileMode.Create,
                                   FileAccess.Write);

                while (!_cts.IsCancellationRequested)
                {
                    var bytesRead = await recorder.ReadAsync(audioBuffer, 0, minBufferSize);

                    await fileStream.WriteAsync(audioBuffer, 0, bytesRead, CancellationToken.None);
                }

                await using var writer = new BinaryWriter(fileStream, System.Text.Encoding.UTF8);

                writer.Seek(0, SeekOrigin.Begin);
			
                // ChunkID               
                writer.Write('R');
                writer.Write('I');
                writer.Write('F');
                writer.Write('F');

                // ChunkSize               
                writer.Write(BitConverter.GetBytes(fileStream.Length + 36), 0, 4);
			
                // Format               
                writer.Write('W');
                writer.Write('A');
                writer.Write('V');
                writer.Write('E');
			
                //SubChunk               
                writer.Write('f');
                writer.Write('m');
                writer.Write('t');
                writer.Write(' ');

                // SubChunk1Size - 16 for PCM
                writer.Write(BitConverter.GetBytes(16), 0, 4);
			
                // AudioFormat - PCM=1
                writer.Write(BitConverter.GetBytes((short)1), 0, 2);

                // Channels: Mono=1, Stereo=2
                writer.Write(BitConverter.GetBytes(1), 0, 2);
			
                // SampleRate
                writer.Write(16000);
		
                // ByteRate
                var byteRate = 16000 * 1 * 16 / 8;               
                writer.Write(BitConverter.GetBytes(byteRate), 0, 4);

                // BlockAlign
                var blockAlign = 1 * 16 / 8;
                writer.Write(BitConverter.GetBytes((short)blockAlign), 0, 2);

                // BitsPerSample
                writer.Write(BitConverter.GetBytes(16), 0, 2);
			
                // SubChunk2ID
                writer.Write('d');
                writer.Write('a');
                writer.Write('t');
                writer.Write('a');
			
                // Subchunk2Size
                writer.Write(BitConverter.GetBytes(fileStream.Length), 0, 4);

                fileStream.Close();

                recorder.Stop();
                recorder.Release();
            }, CancellationToken.None);
        }

        async Task StopRecording()
        {
            _cts.Cancel();

            await _task;

            if (File.Exists(GetFileNameForRecording(this, Ext)))
                await AudioNoteProcessor.ProcessAsync(GetFileNameForRecording(this, Ext),
                    msg => _logView.Text += "\n" + msg);
        }

        static void Message(object sender, string text) =>
            Snackbar.Make((View) sender, text, BaseTransientBottomBar.LengthLong)
                .SetAction("Action", (View.IOnClickListener) null)
                .Show();

        public static readonly string[] RequiredPermissions =
        {
            Manifest.Permission.WriteExternalStorage,
            Manifest.Permission.ReadExternalStorage,
            Manifest.Permission.RecordAudio,
            Manifest.Permission.Internet
        };

        public static readonly int RequestAllPermissions = 1200;
        CancellationTokenSource _cts;
        Task _task;

        public static bool HasPermissionToRecord(Context context) =>
            !RequiredPermissions.Select(permission => ContextCompat.CheckSelfPermission(context, permission))
                .Contains(Permission.Denied);

        public static void PerformRuntimePermissionsCheckForRecording(Activity activity)
        {
            if (ShouldShowUserPermissionRationle(activity))
            {
                var rationale =
                    Snackbar.Make(GetLayoutForSnackbar(activity),
                        "Resource.String.permissions_rationale pippopeppe",
                        BaseTransientBottomBar.LengthIndefinite);

                rationale.SetAction(
                    "Oki",
                    obj => ActivityCompat.RequestPermissions(activity, RequiredPermissions, RequestAllPermissions));

                rationale.Show();
            }
            else
                ActivityCompat.RequestPermissions(activity, RequiredPermissions, RequestAllPermissions);
        }

        public static bool ShouldShowUserPermissionRationle(Activity activity) =>
            ActivityCompat.ShouldShowRequestPermissionRationale(activity, Manifest.Permission.RecordAudio) &&
            ActivityCompat.ShouldShowRequestPermissionRationale(activity, Manifest.Permission.WriteExternalStorage);

        public static View GetLayoutForSnackbar(Activity activity) =>
            activity.FindViewById(Android.Resource.Id.Content);

        public static string GetFileNameForRecording(Context context, string ext) =>
            Path.Combine(context.GetExternalFilesDir(Environment.DirectoryMusic)!.AbsolutePath!,
                "note." + ext);
    }
}