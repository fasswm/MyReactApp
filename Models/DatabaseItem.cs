namespace MyReactApp.Server.Models;


public class DatabaseItem
{
    public int Id { get; set; }
    
   
    public Dictionary<string, object?> Data { get; set; } = new();
}

