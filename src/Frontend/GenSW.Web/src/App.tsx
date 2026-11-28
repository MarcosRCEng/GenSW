import { AppRoutes } from './routes/AppRoutes'

function App({ readNavigationState }: { readNavigationState: (state: unknown) => unknown }) {
  return <AppRoutes readNavigationState={readNavigationState} />
}

export default App
