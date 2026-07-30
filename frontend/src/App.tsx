import { NavLink, Navigate, Route, Routes } from 'react-router'
import './App.css'
import RisksPage from './risks/RisksPage'

/**
 * App shell: the persistent header and nav, plus the route table.
 *
 * A fragment rather than a wrapper element — `#root` is already a flex column
 * (see index.css), so the header and the routed page are its flex children.
 * `<main>` belongs to the page, not here: one per document.
 */
function App() {
  return (
    <>
      <header className="app-header">
        <h1>Risk Register</h1>
        <nav className="app-nav" aria-label="Main">
          {/*
            NavLink sets the `active` class and `aria-current="page"` itself, so
            no render prop is needed — styling hangs off `.app-nav a.active`.
          */}
          <NavLink to="/risks">Register</NavLink>
        </nav>
      </header>

      <Routes>
        <Route path="/risks" element={<RisksPage />} />
        {/* The register is the only screen, so everything else — `/` included
            — lands on it. `replace` keeps Back from bouncing off the redirect. */}
        <Route path="*" element={<Navigate to="/risks" replace />} />
      </Routes>
    </>
  )
}

export default App
