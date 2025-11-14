using Microsoft.EntityFrameworkCore;
using MyReactApp.Server.Data;

namespace MyReactApp.Server.Configuration
{
 
    public abstract class ApplicationConfiguration
    {
       
        public virtual void ConfigureServices(WebApplicationBuilder builder)
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
            
            ConfigureCors(builder);
        }

    
        protected virtual void ConfigureCors(WebApplicationBuilder builder)
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    policy =>
                    {
                        policy.WithOrigins("http://localhost:3000")
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
            });
        }

        
        public virtual void ConfigureMiddleware(WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowReactApp");
            app.UseHttpsRedirection();

            if (!app.Environment.IsDevelopment())
            {
                app.UseStaticFiles();
                app.UseRouting();
                app.MapFallbackToFile("index.html");
            }
        }
    }
}

