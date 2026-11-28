import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './features/auth/providers/AuthProvider'
import { createSessionNavigation } from './shared/navigation/sessionNavigation'
import { UnsavedChangesProvider } from './shared/navigation/UnsavedChangesProvider'
import './styles/index.css'

// Keep the existing route tree and URLs. A data router supplies the supported
// navigation blocker, including browser Back, without patching browser history.
const sessionNavigation = createSessionNavigation()
const router = createBrowserRouter([{ path: '*', element: <AuthProvider><UnsavedChangesProvider><App readNavigationState={sessionNavigation.read} /></UnsavedChangesProvider></AuthProvider> }])
sessionNavigation.attach(router)

const root = document.getElementById('root')!
window.addEventListener('pagehide', event => { if (event.persisted) root.hidden = true })
window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload() })

createRoot(root).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
)
