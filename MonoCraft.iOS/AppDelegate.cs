using Microsoft.Xna.Framework;
using UIKit;
using Foundation;

namespace MonoCraft;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    private Game1 _game;

    public override bool FinishedLaunching(UIApplication app, NSDictionary options)
    {
        _game = new Game1();
        _game.Run();
        return true;
    }


}
