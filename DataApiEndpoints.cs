using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyReactApp.Server.Data;
using MyReactApp.Server.Helpers;

namespace MyReactApp.Server.Endpoints
{
    
    public class DataApiEndpoints : ApiEndpointBase
    {
        
        public override void RegisterEndpoints(WebApplication app)
        {
            RegisterPreflightEndpoints(app);
            RegisterTableEndpoints(app);
            RegisterDataEndpoints(app);
        }

        
        private void RegisterTableEndpoints(WebApplication app)
        {
            app.MapGet("/api/tables", async (ApplicationDbContext db) =>
            {
                var tables = await DatabaseHelper.GetTableNames(db);
                return Results.Ok(tables);
            })
            .WithName("GetTables")
            .WithOpenApi();
        }

        
        private void RegisterDataEndpoints(WebApplication app)
        {
            // GET - получение всех данных из таблицы
            app.MapGet("/api/data/{tableName}", async (string tableName, ApplicationDbContext db) =>
            {
                var tables = await DatabaseHelper.GetTableNames(db);
                if (!tables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.NotFound(new { error = $"Таблица '{tableName}' не найдена" });
                }

                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT * FROM [{tableName}]";
                
                var reader = await command.ExecuteReaderAsync();
                var results = new List<Dictionary<string, object?>>();
                
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[columnName] = value;
                    }
                    results.Add(row);
                }
                
                await connection.CloseAsync();
                return Results.Ok(results);
            })
            .WithName("GetTableData")
            .WithOpenApi();

            // POST - добавление новой строки
            app.MapPost("/api/data/{tableName}", async (string tableName, HttpRequest request, ApplicationDbContext db) =>
            {
                return await InsertRowAsync(tableName, request, db);
            })
            .WithName("InsertTableRow")
            .WithOpenApi();

            // PUT - обновление строки по ID
            app.MapPut("/api/data/{tableName}/{rowId}", async (string tableName, string rowId, HttpRequest request, ApplicationDbContext db) =>
            {
                return await UpdateRowByIdAsync(tableName, rowId, request, db);
            })
            .WithName("UpdateTableRow")
            .WithOpenApi();

            // DELETE - удаление строки по ID
            app.MapDelete("/api/data/{tableName}/{rowId}", async (string tableName, string rowId, ApplicationDbContext db) =>
            {
                return await DeleteRowByIdAsync(tableName, rowId, db);
            })
            .WithName("DeleteTableRow")
            .WithOpenApi();

            // POST - обновление по всем полям
            app.MapPost("/api/data/{tableName}/update", async (string tableName, HttpRequest request, ApplicationDbContext db) =>
            {
                return await UpdateRowByFieldsAsync(tableName, request, db);
            })
            .WithName("UpdateByRow")
            .WithOpenApi();

            // POST - удаление по всем полям
            app.MapPost("/api/data/{tableName}/delete", async (string tableName, HttpRequest request, ApplicationDbContext db) =>
            {
                return await DeleteRowByFieldsAsync(tableName, request, db);
            })
            .WithName("DeleteByRow")
            .WithOpenApi();
        }

        private async Task<IResult> InsertRowAsync(string tableName, HttpRequest request, ApplicationDbContext db)
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest("Пустые данные для добавления");
            }

            using var jsonDoc = System.Text.Json.JsonDocument.Parse(body);
            var newData = new Dictionary<string, object?>();
            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            {
                newData[prop.Name] = DatabaseHelper.ConvertJsonElementToObject(prop.Value);
            }

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            // Используем транзакцию для предотвращения race condition при параллельных вставках
            using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            
            try
            {
                var schemaCmd = connection.CreateCommand();
                schemaCmd.Transaction = transaction;
                schemaCmd.CommandText = $"PRAGMA table_info([{tableName}])";
                var columns = new List<(string Name, string Type, bool IsPk)>();
                var schemaReader = await schemaCmd.ExecuteReaderAsync();
                while (await schemaReader.ReadAsync())
                {
                    var name = schemaReader.GetString(1);
                    var type = schemaReader.GetString(2);
                    var isPk = schemaReader.GetValue(5) != DBNull.Value && Convert.ToInt32(schemaReader.GetValue(5)) == 1;
                    columns.Add((name, type, isPk));
                }
                await schemaReader.CloseAsync();

                var primaryKey = GetPrimaryKey(columns);
                if (!string.IsNullOrEmpty(primaryKey) && !newData.ContainsKey(primaryKey))
                {
                    var idCol = columns.FirstOrDefault(c => c.Name.Equals(primaryKey, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(idCol.Name) && idCol.Type.ToUpperInvariant().Contains("INT"))
                    {
                        // Генерация ID в транзакции предотвращает дубликаты при параллельных запросах
                        var maxCmd = connection.CreateCommand();
                        maxCmd.Transaction = transaction;
                        maxCmd.CommandText = $"SELECT IFNULL(MAX([{primaryKey}]), 0) + 1 FROM [{tableName}]";
                        var nextIdObj = await maxCmd.ExecuteScalarAsync();
                        var nextId = Convert.ToInt64(nextIdObj ?? 1);
                        newData[primaryKey] = nextId;
                    }
                }

                var insertColumns = new List<string>();
                var insertParams = new List<string>();
                var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;

                foreach (var col in columns)
                {
                    if (col.IsPk && col.Type.ToUpperInvariant().Contains("INT") && !newData.ContainsKey(col.Name))
                    {
                        continue;
                    }
                    if (newData.ContainsKey(col.Name))
                    {
                        var p = insertCmd.CreateParameter();
                        p.ParameterName = $"@{col.Name}";
                        p.Value = newData[col.Name] ?? DBNull.Value;
                        insertColumns.Add($"[{col.Name}]");
                        insertParams.Add(p.ParameterName);
                        insertCmd.Parameters.Add(p);
                    }
                }

                if (insertColumns.Count == 0)
                {
                    await transaction.RollbackAsync();
                    await connection.CloseAsync();
                    return Results.BadRequest("Нет данных для вставки");
                }

                insertCmd.CommandText = $"INSERT INTO [{tableName}] (" + string.Join(", ", insertColumns) + ") VALUES (" + string.Join(", ", insertParams) + ")";
                await insertCmd.ExecuteNonQueryAsync();
                
                // Коммитим транзакцию только после успешной вставки
                await transaction.CommitAsync();
                await connection.CloseAsync();
            }
            catch
            {
                // В случае ошибки откатываем транзакцию
                await transaction.RollbackAsync();
                await connection.CloseAsync();
                throw;
            }
            
            return Results.Ok(new { message = "Строка добавлена" });
        }

        private async Task<IResult> UpdateRowByIdAsync(string tableName, string rowId, HttpRequest request, ApplicationDbContext db)
        {
            request.EnableBuffering();
            if (request.Body.CanSeek) request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            if (request.Body.CanSeek) request.Body.Position = 0;
            
            if (string.IsNullOrEmpty(body))
            {
                return Results.BadRequest("Пустое тело запроса");
            }
            
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(body);
            var updatedData = new Dictionary<string, object?>();
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                updatedData[property.Name] = DatabaseHelper.ConvertJsonElementToObject(property.Value);
            }

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            
            (string? primaryKeyColumn, string? pkType) = await GetPrimaryKeyInfoAsync(connection, tableName);
            if (string.IsNullOrEmpty(primaryKeyColumn))
            {
                await connection.CloseAsync();
                return Results.Problem("Не удалось определить первичный ключ таблицы");
            }

            var setParts = new List<string>();
            var updateCommand = connection.CreateCommand();
            
            foreach (var kvp in updatedData)
            {
                if (kvp.Key.Equals(primaryKeyColumn, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var paramName = $"@param{setParts.Count}";
                setParts.Add($"[{kvp.Key}] = {paramName}");
                var dbParam = updateCommand.CreateParameter();
                dbParam.ParameterName = paramName;
                dbParam.Value = kvp.Value ?? DBNull.Value;
                updateCommand.Parameters.Add(dbParam);
            }

            if (setParts.Count == 0)
            {
                await connection.CloseAsync();
                return Results.BadRequest("Нет данных для обновления");
            }

            var whereParam = "@whereParam";
            updateCommand.CommandText = $"UPDATE [{tableName}] SET {string.Join(", ", setParts)} WHERE [{primaryKeyColumn}] = {whereParam}";
            
            object whereValue = ParseRowId(rowId, pkType);
            
            var whereDbParam = updateCommand.CreateParameter();
            whereDbParam.ParameterName = whereParam;
            whereDbParam.Value = whereValue;
            updateCommand.Parameters.Add(whereDbParam);
            
            var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            if (rowsAffected == 0)
            {
                return Results.NotFound($"Строка с ID '{rowId}' не найдена в таблице '{tableName}'");
            }

            return Results.Ok(new { message = "Данные успешно обновлены" });
        }

        private async Task<IResult> DeleteRowByIdAsync(string tableName, string rowId, ApplicationDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            (string? primaryKey, string? pkType) = await GetPrimaryKeyInfoAsync(connection, tableName);
            if (string.IsNullOrEmpty(primaryKey))
            {
                await connection.CloseAsync();
                return Results.Problem("Не удалось определить ключевую колонку для удаления");
            }

            var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = $"DELETE FROM [{tableName}] WHERE [{primaryKey}] = @id";
            var idParam = deleteCmd.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = ParseRowId(rowId, pkType);
            deleteCmd.Parameters.Add(idParam);

            var affected = await deleteCmd.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            if (affected == 0)
            {
                return Results.NotFound($"Строка с ID '{rowId}' не найдена");
            }

            return Results.Ok(new { message = "Строка удалена" });
        }

        private async Task<IResult> UpdateRowByFieldsAsync(string tableName, HttpRequest request, ApplicationDbContext db)
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest("Пустые данные для обновления");
            }

            using var jsonDoc = System.Text.Json.JsonDocument.Parse(body);
            if (jsonDoc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !jsonDoc.RootElement.TryGetProperty("original", out var originalEl) ||
                !jsonDoc.RootElement.TryGetProperty("updated", out var updatedEl))
            {
                return Results.BadRequest("Ожидается JSON вида { original: {...}, updated: {...} }");
            }

            var original = new Dictionary<string, object?>();
            foreach (var p in originalEl.EnumerateObject()) original[p.Name] = DatabaseHelper.ConvertJsonElementToObject(p.Value);
            var updated = new Dictionary<string, object?>();
            foreach (var p in updatedEl.EnumerateObject()) updated[p.Name] = DatabaseHelper.ConvertJsonElementToObject(p.Value);

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            var schemaCmd = connection.CreateCommand();
            schemaCmd.CommandText = $"PRAGMA table_info([{tableName}])";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var schemaReader = await schemaCmd.ExecuteReaderAsync();
            while (await schemaReader.ReadAsync()) columns.Add(schemaReader.GetString(1));
            await schemaReader.CloseAsync();

            var setParts = new List<string>();
            var whereParts = new List<string>();
            var cmd = connection.CreateCommand();

            foreach (var kv in updated)
            {
                if (!columns.Contains(kv.Key)) continue;
                var p = cmd.CreateParameter();
                p.ParameterName = "@set_" + kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
                setParts.Add($"[{kv.Key}] = {p.ParameterName}");
            }

            foreach (var kv in original)
            {
                if (!columns.Contains(kv.Key)) continue;
                var p = cmd.CreateParameter();
                p.ParameterName = "@w_" + kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
                whereParts.Add($"[{kv.Key}] = {p.ParameterName}");
            }

            if (setParts.Count == 0 || whereParts.Count == 0)
            {
                await connection.CloseAsync();
                return Results.BadRequest("Недостаточно данных для обновления");
            }

            cmd.CommandText = $"UPDATE [{tableName}] SET " + string.Join(", ", setParts) + " WHERE " + string.Join(" AND ", whereParts);
            await cmd.ExecuteNonQueryAsync();
            await connection.CloseAsync();
            return Results.Ok(new { message = "Обновление выполнено" });
        }

        private async Task<IResult> DeleteRowByFieldsAsync(string tableName, HttpRequest request, ApplicationDbContext db)
        {
            request.EnableBuffering();
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            
            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest("Пустые данные строки для удаления");
            }

            using var jsonDoc = System.Text.Json.JsonDocument.Parse(body);
            var rowData = new Dictionary<string, object?>();
            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
            {
                rowData[prop.Name] = DatabaseHelper.ConvertJsonElementToObject(prop.Value);
            }
            
            if (rowData.Count == 0)
            {
                return Results.BadRequest("Нет полей для сопоставления строки");
            }

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            var schemaCmd = connection.CreateCommand();
            schemaCmd.CommandText = $"PRAGMA table_info([{tableName}])";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var schemaReader = await schemaCmd.ExecuteReaderAsync();
            while (await schemaReader.ReadAsync())
            {
                columns.Add(schemaReader.GetString(1));
            }
            await schemaReader.CloseAsync();

            var whereParts = new List<string>();
            var deleteCmd = connection.CreateCommand();
            foreach (var kv in rowData)
            {
                if (!columns.Contains(kv.Key)) continue;
                var p = deleteCmd.CreateParameter();
                p.ParameterName = "@" + kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                deleteCmd.Parameters.Add(p);
                whereParts.Add($"[{kv.Key}] = {p.ParameterName}");
            }

            if (whereParts.Count == 0)
            {
                await connection.CloseAsync();
                return Results.BadRequest("Не найдено совпадающих колонок для удаления");
            }

            deleteCmd.CommandText = $"DELETE FROM [{tableName}] WHERE " + string.Join(" AND ", whereParts);
            await deleteCmd.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            return Results.Ok(new { message = "Удаление выполнено" });
        }

        private string GetPrimaryKey(List<(string Name, string Type, bool IsPk)> columns)
        {
            var primaryKey = columns.FirstOrDefault(c => c.IsPk).Name;
            if (string.IsNullOrEmpty(primaryKey))
            {
                var exactIdCol = columns.FirstOrDefault(c => string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exactIdCol.Name))
                {
                    primaryKey = exactIdCol.Name;
                }
                else
                {
                    var anyIdCol = columns.FirstOrDefault(c => c.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(anyIdCol.Name))
                    {
                        primaryKey = anyIdCol.Name;
                    }
                }
            }
            return primaryKey ?? string.Empty;
        }

        private async Task<(string? PrimaryKey, string? PkType)> GetPrimaryKeyInfoAsync(System.Data.Common.DbConnection connection, string tableName)
        {
            var getPk = connection.CreateCommand();
            getPk.CommandText = $"PRAGMA table_info([{tableName}])";
            var pkReader = await getPk.ExecuteReaderAsync();
            string? primaryKey = null;
            string? pkType = null;
            var allColumns = new List<(string Name, string Type)>();
            
            while (await pkReader.ReadAsync())
            {
                var name = pkReader.GetString(1);
                var type = pkReader.GetString(2);
                allColumns.Add((name, type));
                var isPk = pkReader.GetValue(5);
                if (isPk != null && isPk != DBNull.Value && Convert.ToInt32(isPk) == 1)
                {
                    primaryKey = name;
                    pkType = type.ToUpperInvariant();
                }
            }
            await pkReader.CloseAsync();

            if (string.IsNullOrEmpty(primaryKey))
            {
                var exactId = allColumns.FirstOrDefault(c => string.Equals(c.Name, "id", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exactId.Name))
                {
                    primaryKey = exactId.Name;
                    pkType = exactId.Type.ToUpperInvariant();
                }
                else
                {
                    var anyId = allColumns.FirstOrDefault(c => c.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(anyId.Name))
                    {
                        primaryKey = anyId.Name;
                        pkType = anyId.Type.ToUpperInvariant();
                    }
                }
            }

            return (primaryKey, pkType);
        }

        private object ParseRowId(string rowId, string? pkType)
        {
            object whereValue = rowId;
            if (pkType == null || pkType.Contains("INT"))
            {
                if (long.TryParse(rowId, out var idNum)) whereValue = idNum;
            }
            else if (pkType.Contains("REAL") || pkType.Contains("NUMERIC"))
            {
                if (double.TryParse(rowId, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var real)) whereValue = real;
            }
            return whereValue;
        }
    }
}

