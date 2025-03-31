using Azure.Identity;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Set up Azure App Configuration using Managed Identity (IAM)
builder.Configuration.AddAzureAppConfiguration(options =>
{
    string appConfigEndpoint = builder.Configuration["AppConfigEndpoint"];
    options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
           .UseFeatureFlags(featureFlagOptions =>
           {
               featureFlagOptions.CacheExpirationInterval = TimeSpan.FromSeconds(5);
           });
});

builder.Services.AddAzureAppConfiguration();
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Use Azure App Configuration Middleware
app.UseAzureAppConfiguration();

app.MapHealthChecks("/health");

app.MapGet("/", (IConfiguration config) =>
{
    bool isReleaseEnabled = config.GetValue<bool>("FeatureManagement:PM11");
    string message = isReleaseEnabled
            ? "🔥 <b>WE ARE LIVE!</b> PM11 is ON! The internet is faster, the coffee tastes better, and somewhere, a developer just smiled. 🚀🎉"
            : "🚨 <b>RED ALERT!</b> PM11 is disabled, push the button! 🚧";

    string imageUrl = isReleaseEnabled
        ? "https://media1.giphy.com/media/v1.Y2lkPTc5MGI3NjExeWR5Z2Y5dWoyNnJhbnVrdjRjaG1zOWN4aWo1cWhlaTV0Mmt5bnI1YiZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/WygEr1vMRXmzemxGmG/giphy.gif"
        : "https://media.giphy.com/media/LoUmOTtGzs3A8WggVx/giphy.gif";

    string htmlResponse = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Release Status</title>
            <style>
                body {{
                    font-family: Arial, sans-serif;
                    text-align: center;
                    background-color: {(isReleaseEnabled ? "#0f0f0f" : "#ffdddd")};
                    color: {(isReleaseEnabled ? "#ffffff" : "#ff0000")};
                    padding: 50px;
                }}
                h1 {{ font-size: 2.5em; }}
                img {{ max-width: 100%; height: auto; margin-top: 20px; border-radius: 10px; }}
            </style>
        </head>
        <body>
            <h1>{message}</h1>
            <img src='{imageUrl}' alt='Status GIF'>
        </body>
        </html>";

    return Results.Content(htmlResponse, "text/html");
});

app.Run();
