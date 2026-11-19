import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App.jsx';
import { initI18n } from './i18n/index.js';
import './index.css';
import './styles/layout.css';
import './styles/components.css';
import './styles/views.css';

// Load the active locale before the first render so there is no flash of the fallback language.
initI18n().then(() => {
  createRoot(document.getElementById('root')).render(
    <StrictMode>
      <App />
    </StrictMode>
  );
});
