namespace MyReactApp.Server.Endpoints
{
    
    public abstract class ApiEndpointBase
    {
        public abstract void RegisterEndpoints(WebApplication app);

        
        protected virtual void RegisterPreflightEndpoints(WebApplication app)
        {
            app.MapMethods("/api/data/{tableName}", new[] { "OPTIONS" }, () => Results.Ok())
               .WithName("PreflightDataTable")
               .WithOpenApi();

            app.MapMethods("/api/data/{tableName}/{rowId}", new[] { "OPTIONS" }, () => Results.Ok())
               .WithName("PreflightDataTableById")
               .WithOpenApi();
        }
    }
}

