// See https://aka.ms/new-console-template for more information

using Launcher.Platform;
using LiteEngine.Core;

var model = new Model();
Window window = new Window();
window.Load += () =>
{
    model.LoadModel(@"./tr_leet/leet.FBX");
    Scene.Current.Root.Childern.Add(model);

};
window.Run();
