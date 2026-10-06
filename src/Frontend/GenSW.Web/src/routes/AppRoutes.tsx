import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { FinancialPage } from '../features/financial/FinancialPage'
import { CategoryDetailsPage, EntryDetailsPage } from '../features/financial/FinancialDetailsPage'
import { PeopleDetailsPage } from '../features/people/pages/PeopleDetailsPage'
import { SpeciesDetailsPage } from '../features/species/pages/SpeciesDetailsPage'
import { BreedDetailsPage } from '../features/breeds/pages/BreedDetailsPage'
import { VarietyDetailsPage } from '../features/varieties/pages/VarietyDetailsPage'
import { AnimalDetailsPage } from '../features/animals/pages/AnimalDetailsPage'
import { AnimalRelatedDetailsPage } from '../features/animals/pages/AnimalRelatedDetailsPage'
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
import { PropertiesListPage } from '../features/properties/PropertiesListPage'
import { PropertyFormPage } from '../features/properties/PropertyFormPage'
import { PropertyDetailsPage } from '../features/properties/PropertyDetailsPage'
import { ItemsPage, ItemFormPage, ItemDetailsPage, CategoriesPage, ConversionFormPage } from '../features/formulation/CatalogPages'
import { ProfileFormPage, ProfileDetailsPage } from '../features/formulation/ProfilePages'
import { RecipesPage, RecipeHeaderFormPage, RecipeDetailsPage, RecipeVersionFormPage, RecipeVersionDetailsPage } from '../features/formulation/RecipePages'
import { FormulationPage, SimulationFormPage, SimulationDetailsPage, ComparisonDetailsPage } from '../features/formulation/SimulationPages'

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
        <Route element={<ItemsPage />} path="/itens" />
        <Route element={<ItemFormPage />} path="/itens/novo" />
        <Route element={<CategoriesPage />} path="/itens/categorias" />
        <Route element={<ItemDetailsPage />} path="/itens/:id" />
        <Route element={<ItemFormPage />} path="/itens/:id/editar" />
        <Route element={<ConversionFormPage />} path="/itens/:id/conversoes/nova" />
        <Route element={<ProfileFormPage />} path="/itens/:id/perfis/novo" />
        <Route element={<ProfileDetailsPage />} path="/producao/perfis/:profileId" />
        <Route element={<ProfileFormPage />} path="/producao/perfis/:profileId/editar" />
        <Route element={<RecipesPage />} path="/producao/receitas" />
        <Route element={<RecipeHeaderFormPage />} path="/producao/receitas/nova" />
        <Route element={<RecipeDetailsPage />} path="/producao/receitas/:id" />
        <Route element={<RecipeHeaderFormPage />} path="/producao/receitas/:id/editar" />
        <Route element={<RecipeVersionFormPage />} path="/producao/receitas/:id/versoes/nova" />
        <Route element={<RecipeVersionDetailsPage />} path="/producao/receitas/versoes/:versionId" />
        <Route element={<RecipeVersionFormPage />} path="/producao/receitas/versoes/:versionId/editar" />
        <Route element={<FormulationPage />} path="/producao/formulacao" />
        <Route element={<SimulationFormPage />} path="/producao/formulacao/nova" />
        <Route element={<SimulationDetailsPage />} path="/producao/formulacao/:simulationId" />
        <Route element={<ComparisonDetailsPage />} path="/producao/formulacao/comparacoes/:comparisonId" />
        <Route element={<PropertiesListPage />} path="/propriedades" />
        <Route element={<PropertyFormPage />} path="/propriedades/nova" />
        <Route element={<PropertyFormPage />} path="/propriedades/:id/editar" />
        <Route element={<PropertyDetailsPage />} path="/propriedades/:id" />
        <Route element={<FinancialPage />} path="/financeiro" />
        <Route element={<CategoryDetailsPage />} path="/financeiro/categorias/:id" />
        <Route element={<EntryDetailsPage />} path="/financeiro/lancamentos/:id" />
        <Route element={<PeopleDetailsPage />} path="/pessoas/:id" />
        <Route element={<SpeciesDetailsPage />} path="/especies/:id" />
        <Route element={<BreedDetailsPage />} path="/racas/:id" />
        <Route element={<VarietyDetailsPage />} path="/variedades/:id" />
        <Route element={<AnimalDetailsPage />} path="/animais/:id" />
        <Route element={<AnimalRelatedDetailsPage section="identificacoes" />} path="/animais/:animalId/identificacoes/:recordId" />
        <Route element={<AnimalRelatedDetailsPage section="registros" />} path="/animais/:animalId/registros/:recordId" />
        <Route element={<AnimalRelatedDetailsPage section="pesagens" />} path="/animais/:animalId/pesagens/:recordId" />
        <Route element={<AnimalRelatedDetailsPage section="producoes-ovos" />} path="/animais/:animalId/producoes-ovos/:recordId" />
        <Route element={<AnimalRelatedDetailsPage section="imagens" />} path="/animais/:animalId/imagens/:recordId" />
        <Route element={<AnimalRelatedDetailsPage section="imagens" kind="variedades" />} path="/variedades/:varietyId/imagens/:recordId" />
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
