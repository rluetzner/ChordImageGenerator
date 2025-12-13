using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace EinarEgilsson.Chords
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<ChordMiddleware>();
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }
    }
}
