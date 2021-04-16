using HarMockServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Service.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace HarMockServer
{
    public class Startup
    {
        private readonly IConfiguration _config;

        public Startup(IConfiguration config)
        {
            _config = config;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Mocks>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, errors) => {
                    return true;
                };

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.Map("/{**catch-all}", async httpContext =>
                {
                    var mocks = httpContext.RequestServices.GetRequiredService<Mocks>();
                    var logger = httpContext.RequestServices.GetRequiredService<ILogger<Startup>>();

                    var match = mocks.Files.Values
                        .SelectMany(v => v.Log.Entries)
                        .Where(e => new Uri(e.Request.Url).AbsolutePath.ToLower() == httpContext.Request.Path.Value.ToLower())
                        .FirstOrDefault();

                    // Found match in HAR file mock api use HAR response
                    if (match != null)
                    {
                        logger.LogInformation($"Mocking API {new Uri(match.Request.Url).AbsolutePath}");

                        httpContext.Response.StatusCode = 200;
                        await httpContext.Response.WriteAsync(match.Response.Content.Text);
                        return;
                    }

                    // No match found, forward request to original api
                    httpContext.Response.StatusCode = 200;
                    await httpContext.Response.WriteAsync("No mock response at that endpoint");                   
                });
            });
        }
    }
}
