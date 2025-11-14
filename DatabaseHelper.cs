using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyReactApp.Server.Data;

namespace MyReactApp.Server.Helpers
{
   
    public static class DatabaseHelper
    {
        
        public static object? ConvertJsonElementToObject(System.Text.Json.JsonElement element)
        {
            try
            {
                switch (element.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.String:
                        return element.GetString();
                    case System.Text.Json.JsonValueKind.Number:
                        if (element.TryGetInt32(out var intValue))
                            return intValue;
                        if (element.TryGetInt64(out var longValue))
                            return longValue;
                        if (element.TryGetDouble(out var doubleValue))
                            return doubleValue;
                        if (element.TryGetDecimal(out var decimalValue))
                            return decimalValue;
                        return element.GetRawText();
                    case System.Text.Json.JsonValueKind.True:
                        return true;
                    case System.Text.Json.JsonValueKind.False:
                        return false;
                    case System.Text.Json.JsonValueKind.Null:
                        return null;
                    case System.Text.Json.JsonValueKind.Object:
                    case System.Text.Json.JsonValueKind.Array:
                        return element.GetRawText();
                    default:
                        return element.ToString();
                }
            }
            catch
            {
                return element.ToString();
            }
        }

      
        public static async Task<List<string>> GetTableNames(ApplicationDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            
            try
            {
                await connection.OpenAsync();
                
                var tables = new List<string>();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                
                var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
                
                return tables;
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}

