import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './styles/tokens.css';
import './styles/reducedMotion.css';
import './styles/justPlaced.css';
import App from './App';
import RunOutputWindowApp from './RunOutputWindowApp';
import { panelFromSearch } from './panelMode';

const isRunOutputWindow = panelFromSearch(window.location.search) === 'runoutput';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {isRunOutputWindow ? <RunOutputWindowApp /> : <App />}
  </StrictMode>,
);
