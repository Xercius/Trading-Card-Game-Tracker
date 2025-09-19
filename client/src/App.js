import { useEffect, useState } from 'react';

function App() {
  const [cards, setCards] = useState([]);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetch('/api/card')
      .then(res => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
      })
      .then(data => setCards(data))
      .catch(err => setError(err.message));
  }, []);

  return (
    <div style={{ padding: "1rem" }}>
      <h1>Cards from API</h1>

      {error && <p style={{ color: "red" }}>Error: {error}</p>}

      {cards.length === 0 && !error && <p>Loading…</p>}

      <ul>
        {cards.map(c => (
          <li key={c.id}>
            {c.name} ({c.game})
          </li>
        ))}
      </ul>
    </div>
  );
}

export default App;