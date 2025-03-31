using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/", async () =>
{
    var credential = new DefaultAzureCredential();
    // The scope is a fixed value for Databricks in Azure
    var tokenRequestContext = new TokenRequestContext(new[] { "2ff814a6-3304-4ab8-85cb-cd0e6f879c1d/.default" });
    var token = await credential.GetTokenAsync(tokenRequestContext);

    // Get environment variables
    var workspaceUrl = Environment.GetEnvironmentVariable("DATABRICKS_WORKSPACE_URL");
    var sqlEndpoint = Environment.GetEnvironmentVariable("DATABRICKS_SQL_ENDPOINT");


    if (string.IsNullOrEmpty(workspaceUrl) || string.IsNullOrEmpty(sqlEndpoint))
    {
        return Results.Problem("Missing Databricks environment variables.");
    }

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

    var requestUrl = $"{workspaceUrl}/api/2.0/sql/statements";

    // Define a simple SQL query request
    var sqlQuery = new
    {
        statement = "SELECT * FROM ctl_shres_t_we_002.wholesale_internal.executed_migrations LIMIT 1", // Dummy query
        warehouse_id = sqlEndpoint,
        wait_timeout = "50s"
    };

    var requestContent = new StringContent(JsonSerializer.Serialize(sqlQuery), Encoding.UTF8, "application/json");
    var response = await client.PostAsync(requestUrl, requestContent);
    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem($"Error from Databricks API: {responseBody}");
    }

    return Results.Json(JsonSerializer.Deserialize<JsonElement>(responseBody));
});


app.Run();
