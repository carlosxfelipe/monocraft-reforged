using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace MonoCraft;

[Activity(
    Label = "MonoCraft",
    MainLauncher = true,
    AlwaysRetainTaskState = true,
    LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.Keyboard
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.ScreenSize
)]
public class MainActivity : AndroidGameActivity
{
    private Game1 _game;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);

        AndroidAssetExtractor.Extract(Assets, FilesDir.AbsolutePath);

        _game = new Game1();
        var view = _game.Services.GetService(typeof(View)) as View;
        SetContentView(view);
        _game.Run();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        if (hasFocus)
        {
#pragma warning disable CA1422
#pragma warning disable CS0618
            Window.DecorView.SystemUiFlags =
                SystemUiFlags.LayoutStable
                | SystemUiFlags.LayoutHideNavigation
                | SystemUiFlags.LayoutFullscreen
                | SystemUiFlags.HideNavigation
                | SystemUiFlags.Fullscreen
                | SystemUiFlags.ImmersiveSticky;
#pragma warning restore CS0618
#pragma warning restore CA1422
        }
    }
}
