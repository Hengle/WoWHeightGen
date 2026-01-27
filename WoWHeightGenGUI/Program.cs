namespace WoWHeightGenGUI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var app = new App.Application();
        app.Run();
    }
}
