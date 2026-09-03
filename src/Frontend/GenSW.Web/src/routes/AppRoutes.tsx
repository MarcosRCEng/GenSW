import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AuthenticatedHomePage } from '../features/auth/pages/AuthenticatedHomePage'
import { LoginPage } from '../features/auth/pages/LoginPage'
import { useAuth } from '../features/auth/hooks/useAuth'
import { BreedFormPage } from '../features/breeds/pages/BreedFormPage'
import { BreedsListPage } from '../features/breeds/pages/BreedsListPage'
import { PeopleFormPage } from '../features/people/pages/PeopleFormPage'
import { PeopleListPage } from '../features/people/pages/PeopleListPage'
import { SpeciesFormPage } from '../features/species/pages/SpeciesFormPage'
import { SpeciesListPage } from '../features/species/pages/SpeciesListPage'
import { VarietiesListPage } from '../features/varieties/pages/VarietiesListPage'
import { VarietyFormPage } from '../features/varieties/pages/VarietyFormPage'

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
        <Route element={<PeopleFormPage />} path="/pessoas/nova" />
        <Route element={<PeopleFormPage />} path="/pessoas/:id/editar" />
        <Route element={<SpeciesListPage />} path="/especies" />
        <Route element={<SpeciesFormPage />} path="/especies/nova" />
        <Route element={<SpeciesFormPage />} path="/especies/:id/editar" />
        <Route element={<BreedsListPage />} path="/racas" />
        <Route element={<BreedFormPage />} path="/racas/nova" />
        <Route element={<BreedFormPage />} path="/racas/:id/editar" />
        <Route element={<VarietiesListPage />} path="/variedades" />
        <Route element={<VarietyFormPage />} path="/variedades/nova" />
        <Route element={<VarietyFormPage />} path="/variedades/:id/editar" />
      </Route>
      <Route element={<Navigate replace to="/" />} path="*" />
    </Routes>
  )
}
