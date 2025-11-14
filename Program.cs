using MyReactApp.Server.Configuration;
using MyReactApp.Server.Endpoints;

// Создаем экземпляр конфигурации приложения
var builder = WebApplication.CreateBuilder(args);
var appConfig = new DefaultApplicationConfiguration();

// Настраиваем сервисы через базовый класс
appConfig.ConfigureServices(builder);

// Собираем приложение
var app = builder.Build();


appConfig.ConfigureMiddleware(app);

// Регистрируем эндпоинты через производные классы
var dataEndpoints = new DataApiEndpoints();
var weatherEndpoints = new WeatherApiEndpoints();

dataEndpoints.RegisterEndpoints(app);
weatherEndpoints.RegisterEndpoints(app);

// Запускаем приложение
app.Run();
