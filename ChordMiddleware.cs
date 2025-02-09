using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace EinarEgilsson.Chords
{
    public class ChordMiddleware
    {
        private readonly RequestDelegate _next;

        public ChordMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.ToString().ToLower().EndsWith(".png"))
            {
                await _next(context);
                return;
            }

            var query = context.Request.Query;

            var chordName = Regex.Match(context.Request.Path.ToString(), @"^/(.*)\.png$").Groups[1].Value;
            // For whatever reason 'context.Request.Path' resolves a '%2B' to a
            // '+', but not '%23' to '#'.
            // Using HttpUtilty.UrlDecode() breaks this, because it will remove
            // the '+'. That is why we require a workaround here.
            var split = chordName.Split('+');
            chordName = string.Join('+', split.Select(s => HttpUtility.UrlDecode(s)));
            var pos = query["pos"].FirstOrDefault() ?? query["p"].FirstOrDefault() ?? "000000";
            var fingers = query["fingers"].FirstOrDefault() ?? query["f"].FirstOrDefault() ?? "------";
            fingers = fingers.Replace(' ', '+');
            var size = query["size"].FirstOrDefault() ?? query["s"].FirstOrDefault() ?? "1";
            var fullBarre = query["full_barre"].FirstOrDefault() ?? query["b"].FirstOrDefault() ?? "false";
            if (!bool.TryParse(fullBarre, out var drawFullBarre))
            {
                drawFullBarre = false;
            }

            context.Response.ContentType = "image/png";
            context.Response.GetTypedHeaders().CacheControl =
                new CacheControlHeaderValue()
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(7)
                };

            using var image = new ChordBoxImage(chordName, pos, fingers, size, drawFullBarre);
            await image.SaveAsync(context.Response.Body);
        }
    }
}
