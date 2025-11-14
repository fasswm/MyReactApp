# React + ASP.NET Core проект

Этот проект объединяет React фронтенд с ASP.NET Core Web API бэкендом.

## Структура проекта

- `Program.cs` - главный файл ASP.NET Core приложения
- `clientapp/` - React приложение (TypeScript)

## Требования

- .NET 8.0 SDK
- Node.js и npm
- Visual Studio 2022 (опционально)

## Запуск проекта

### Способ 1: Раздельный запуск (для разработки)

1. **Запустите ASP.NET Core API:**

```bash
cd C:\ReactAspNetCoreProject
dotnet run
```

API будет доступен по адресам:
- HTTPS: https://localhost:7241
- HTTP: http://localhost:5159
- Swagger UI: https://localhost:7241/swagger

2. **Запустите React приложение (в отдельном терминале):**

```bash
cd C:\ReactAspNetCoreProject\clientapp
npm start
```

React приложение будет доступно по адресу: http://localhost:3000

### Способ 2: Через Visual Studio

1. Откройте `MyReactApp.Server.csproj` в Visual Studio
2. Запустите проект (F5)
3. В отдельном терминале запустите React приложение:

```bash
cd C:\ReactAspNetCoreProject\clientapp
npm start
```

## Настройка CORS

CORS уже настроен в `Program.cs` для разрешения запросов с `http://localhost:3000`.

## Решение проблем

### Проблема: Visual Studio не отвечает при создании проекта

**Решение:** Используйте командную строку для создания проекта (как было сделано в этом проекте):

```bash
dotnet new webapi -n MyProject.Server
npx create-react-app clientapp --template typescript
```

### Проблема: Ошибки CORS

Убедитесь, что:
- ASP.NET Core приложение запущено
- React приложение запущено на порту 3000
- В `Program.cs` правильно настроен CORS

### Проблема: API не отвечает

1. Проверьте, что ASP.NET Core приложение запущено
2. Проверьте URL в `clientapp/src/App.tsx` - он должен соответствовать порту из `launchSettings.json`
3. Для HTTPS может потребоваться принять самоподписанный сертификат

## Следующие шаги

- Добавьте больше API endpoints в `Program.cs`
- Создайте контроллеры для лучшей организации кода
- Настройте аутентификацию и авторизацию
- Добавьте подключение к базе данных
- Настройте production сборку React для интеграции с ASP.NET Core

## Полезные команды

### ASP.NET Core

```bash
dotnet restore          # Восстановить пакеты NuGet
dotnet build            # Собрать проект
dotnet run              # Запустить проект
dotnet watch run        # Запустить с автоперезагрузкой
```

### React

```bash
npm install             # Установить зависимости
npm start               # Запустить dev сервер
npm run build           # Собрать для production
npm test                # Запустить тесты
```

