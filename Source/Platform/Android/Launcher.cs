using Android.Content;
using Android.Views;
using Silk.NET.Input.Extensions;
namespace Android;

[Activity(Label = "Lanucher",MainLauncher = true)]
public class Launcher : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.launcher);
        // Create your application here
    }

    [Java.Interop.Export]
    public void StartEngine(View v)
    {
        InputSnapshot a =  null;

        var EditText = FindViewById<EditText>(Resource.Id.ArgsEditText);

        Intent intent = new Intent(this, typeof(MainActivity));
        intent.PutExtra("args", EditText.Text);
        StartActivity(intent);
    }
}