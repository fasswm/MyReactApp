import React, { useState, useEffect } from 'react';
import './App.css';

function App() {
  const [weatherForecasts, setWeatherForecasts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetch('/weatherforecast')
      .then(response => {
        if (!response.ok) {
          throw new Error('Ошибка при загрузке данных');
        }
        return response.json();
      })
      .then(data => {
        setWeatherForecasts(data);
        setLoading(false);
      })
      .catch(err => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  if (loading) {
    return <div className="App"><div className="container">Загрузка...</div></div>;
  }

  if (error) {
    return <div className="App"><div className="container">Ошибка: {error}</div></div>;
  }

  return (
    <div className="App">
      <header className="App-header">
        <h1>React и ASP.NET Core приложение</h1>
        <p>Прогноз погоды из ASP.NET Core API</p>
      </header>
      <main className="container">
        <div className="weather-grid">
          {weatherForecasts.map((forecast, index) => (
            <div key={index} className="weather-card">
              <div className="weather-date">{forecast.date}</div>
              <div className="weather-temp">{forecast.temperatureC}°C</div>
              <div className="weather-temp-f">{forecast.temperatureF}°F</div>
              <div className="weather-summary">{forecast.summary}</div>
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}

export default App;

