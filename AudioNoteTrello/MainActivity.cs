﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Google.Android.Material.Snackbar;
using Xamarin.Essentials;

namespace AudioNoteTrello
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        MediaRecorder _recorder;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            FindViewById<Button>(Resource.Id.record)!.Touch += RecordButtonTouch;
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
                    await StopRecording(sender);
                    break;

                default:
                    return;
            }
        }

        void StartRecording()
        {
            _recorder = new MediaRecorder();

            _recorder.SetAudioSource(AudioSource.Mic);
            _recorder.SetOutputFormat(OutputFormat.ThreeGpp);
            _recorder.SetOutputFile(GetFileNameForRecording(this));
            _recorder.SetAudioEncoder(AudioEncoder.AmrNb);

            try
            {
                _recorder.Prepare();
            }
            catch (IOException ioe)
            {
                Log.Error(typeof(MainActivity).FullName, ioe.ToString());
            }

            _recorder.Start();
        }

        async Task StopRecording(object sender)
        {
            if (_recorder == null)
                return;

            _recorder.Stop();
            _recorder.Release();
            _recorder = null;

            if (File.Exists(GetFileNameForRecording(this)))
                await AudioNoteProcessor.ProcessAsync(GetFileNameForRecording(this),
                                                      msg => Message(sender, msg));
        }

        static void Message(object sender, string text) =>
            Snackbar.Make((View) sender, text, Snackbar.LengthLong)
                    .SetAction("Action", (View.IOnClickListener) null)
                    .Show();

        public static readonly string[] RequiredPermissions =
            { Manifest.Permission.WriteExternalStorage, Manifest.Permission.RecordAudio };
        public static readonly int RequestAllPermissions = 1200;

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
                                  Snackbar.LengthIndefinite);

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
        
        public static string GetFileNameForRecording(Context context) => 
            Path.Combine(context.GetExternalFilesDir(Environment.DirectoryMusic)!.AbsolutePath,
                         "note.3gpp");
    }
}