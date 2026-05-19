using Android.App;
using Android.Runtime;

namespace BeauOuPas
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();
            // Firebase Android retiré temporairement (package Plugin.Firebase
            // désactivé dans le csproj). Les notifications push Android seront
            // réintégrées plus tard via une stratégie dédiée.
        }
    }
}