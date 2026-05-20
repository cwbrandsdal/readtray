using Velopack;

namespace ReadTray.App;

public static class Program
{
    [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();

            var app = new App();
            app.Run();
        }
    }
