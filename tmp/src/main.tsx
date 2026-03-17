import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import Sludgeitaire from '../prototype.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Sludgeitaire />
  </StrictMode>,
)
