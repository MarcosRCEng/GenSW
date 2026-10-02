import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { FinancialPage } from '../features/financial/FinancialPage'
import { AuthenticatedHomePage } from '../features/auth/pages/AuthenticatedHomePage'
import { AnimalFormPage } from '../features/animals/pages/AnimalFormPage'
import { AnimalsListPage } from '../features/animals/pages/AnimalsListPage'
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
import { BreedingListPage } from '../features/breedings/BreedingListPage'
import { BreedingFormPage } from '../features/breedings/BreedingFormPage'
import { BreedingDetailsPage } from '../features/breedings/BreedingDetailsPage'
import { ReproductiveCycleListPage } from '../features/reproductive-cycles/ReproductiveCycleListPage'
import { ReproductiveCycleFormPage } from '../features/reproductive-cycles/ReproductiveCycleFormPage'
import { ReproductiveCycleDetailsPage } from '../features/reproductive-cycles/ReproductiveCycleDetailsPage'
import { OffspringListPage } from '../features/offspring/OffspringListPage'
import { OffspringFormPage } from '../features/offspring/OffspringFormPage'
import { OffspringDetailsPage } from '../features/offspring/OffspringDetailsPage'

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
        <Route element={<FinancialPage />} path="/financeiro" />
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
        <Route element={<AnimalsListPage />} path="/animais" />
        <Route element={<AnimalFormPage />} path="/animais/nova" />
        <Route element={<AnimalFormPage />} path="/animais/:id/editar" />
        <Route element={<BreedingListPage />} path="/cruzamentos" />
        <Route element={<BreedingFormPage />} path="/cruzamentos/novo" />
        <Route element={<BreedingDetailsPage />} path="/cruzamentos/:id" />
        <Route element={<BreedingFormPage />} path="/cruzamentos/:id/editar" />
        <Route element={<ReproductiveCycleListPage />} path="/ciclos-reprodutivos" />
        <Route element={<ReproductiveCycleFormPage />} path="/ciclos-reprodutivos/novo" />
        <Route element={<ReproductiveCycleDetailsPage />} path="/ciclos-reprodutivos/:id" />
        <Route element={<ReproductiveCycleFormPage />} path="/ciclos-reprodutivos/:id/editar" />
        <Route element={<OffspringListPage />} path="/proles" />
        <Route element={<OffspringFormPage />} path="/proles/nova" />
        <Route element={<OffspringDetailsPage />} path="/proles/:id" />
        <Route element={<OffspringFormPage />} path="/proles/:id/editar" />
      </Route>
      <Route element={<Navigate replace to="/" />} path="*" />
    </Routes>
  )
}
