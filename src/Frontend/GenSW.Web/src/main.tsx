import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './features/auth/providers/AuthProvider'
import { UnsavedChangesProvider } from './shared/navigation/UnsavedChangesProvider'
import './styles/index.css'

// Keep the existing route tree and URLs. A data router supplies the supported
// navigation blocker, including browser Back, without patching browser history.
const router = createBrowserRouter([{ path: '*', element: <AuthProvider><UnsavedChangesProvider><App /></UnsavedChangesProvider></AuthProvider> }])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
)
