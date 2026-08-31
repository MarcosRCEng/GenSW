import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AuthenticatedHomePage } from '../features/auth/pages/AuthenticatedHomePage'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { useAuth } from '../features/auth/hooks/useAuth'
import { PeopleListPage } from '../features/people/pages/PeopleListPage'

function ApplicationLoading() {
  return (
    <main className="flex min-h-screen items-center justify-center px-6" role="status">
      <p className="text-sm font-medium text-slate-600">Carregando sessão…</p>
    </main>
  )
}

function ProtectedRoute() {
  const { isAuthenticated } = useAuth()

  return isAuthenticated ? <Outlet /> : <Navigate replace to="/login" />
}

function AnonymousRoute() {
  const { isAuthenticated } = useAuth()

  return isAuthenticated ? <Navigate replace to="/" /> : <Outlet />
}

export function AppRoutes() {
  const { isInitializing } = useAuth()

  if (isInitializing) {
    return <ApplicationLoading />
  }

  return (
    <Routes>
      <Route element={<AnonymousRoute />}>
        <Route element={<LoginPage />} path="/login" />
      </Route>
      <Route element={<ProtectedRoute />}>
        <Route element={<AuthenticatedHomePage />} path="/" />
        <Route element={<PeopleListPage />} path="/pessoas" />
      </Route>
      <Route element={<Navigate replace to="/" />} path="*" />
    </Routes>
  )
}
