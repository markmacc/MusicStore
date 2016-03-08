using Microsoft.AspNetCore.Hosting;

namespace MusicStore
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseUrls("http://+:5000")
                .UseDefaultConfiguration(args)
                .UseIISPlatformHandlerUrl()
                .UseStartup("MusicStore")
                .Build();

            host.Run();
        }
    }
}
