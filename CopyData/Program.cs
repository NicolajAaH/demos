using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

// Endpoint to copy data from one schema to another
app.MapPost("/copy-data", async () =>
{
    string server = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER_NAME");
    string sourceDb = Environment.GetEnvironmentVariable("AZURE_SQL_SOURCE_DB");
    string targetDb = Environment.GetEnvironmentVariable("AZURE_SQL_TARGET_DB");
    string schemaName = Environment.GetEnvironmentVariable("AZURE_SQL_SCHEMA_NAME");
    // Set your connection strings for both source and target databases
    string sourceConnectionString = $"Server=tcp:{server}.database.windows.net,1433;Database={sourceDb};Authentication=Active Directory Managed Identity; Encrypt=True;MultipleActiveResultSets=True;";
    string targetConnectionString = $"Server=tcp:{server}.database.windows.net,1433;Database={targetDb};Authentication=Active Directory Managed Identity; Encrypt=True;MultipleActiveResultSets=True;";

    try
    {
        var credential = new DefaultAzureCredential();

        await using (var sourceConn = new SqlConnection(sourceConnectionString))
        await using (var targetConn = new SqlConnection(targetConnectionString))
        {

            await sourceConn.OpenAsync();
            await targetConn.OpenAsync();

            // Get the list of tables from the schema
            var getTablesCmd = new SqlCommand($"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @SchemaName", sourceConn);
            getTablesCmd.CommandTimeout = 3600; // Increase command timeout to 60 minutes (in seconds)
            getTablesCmd.Parameters.AddWithValue("@SchemaName", schemaName);

            var tablesReader = await getTablesCmd.ExecuteReaderAsync();
            while (await tablesReader.ReadAsync())
            {
                var tableName = tablesReader.GetString(0);
                await CopyTableDataAsync(schemaName, tableName, sourceConn, targetConn);
            }

            return Results.Ok($"Data copied successfully from schema {schemaName} in {sourceDb} to {targetDb}.");
        }
    }
    catch (Exception ex)
    {
        // Handle exceptions and return a 500 status code
        return Results.Problem($"Error copying data: {ex.Message}");
    }
}).WithRequestTimeout(TimeSpan.FromMinutes(60)); // Set a timeout for the request

// Function to copy data from source table to target table
async Task CopyTableDataAsync(string schemaName, string tableName, SqlConnection sourceConn, SqlConnection targetConn)
{
    // Fetch data from the source table
    var selectCmd = new SqlCommand($"SELECT * FROM {schemaName}.{tableName}", sourceConn);
    selectCmd.CommandTimeout = 3600;  // Increase command timeout to 60 minutes (in seconds)
    using (var reader = await selectCmd.ExecuteReaderAsync())
    {
        // Prepare the bulk copy to insert data into the target table
        using (var bulkCopy = new SqlBulkCopy(targetConn, SqlBulkCopyOptions.KeepIdentity, null))
        {
            bulkCopy.BulkCopyTimeout = 3600; // Set bulk copy timeout to 60 minutes (in seconds)
            bulkCopy.DestinationTableName = $"{schemaName}.{tableName}";
            await bulkCopy.WriteToServerAsync(reader);
        }
    }
}

app.Run();