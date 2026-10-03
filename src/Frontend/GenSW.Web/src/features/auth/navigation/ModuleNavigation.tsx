import { Link } from 'react-router-dom'
import { functionalAreas, navigationModules, type NavigationModule } from './modules'

function ModuleCard({ module }: { module: NavigationModule }) {
  const available = module.state === 'available'
  const features = available
    ? module.route !== undefined
      ? [{ id: module.id, name: module.name, route: module.route }]
      : module.features
    : []

  return (
    <article
      aria-labelledby={`module-${module.id}`}
      className={`flex min-w-0 flex-col rounded-xl border bg-white p-5 ${available ? 'border-emerald-200 shadow-sm' : 'border-slate-200'}`}
    >
      <p className={`text-xs font-semibold ${available ? 'text-emerald-700' : 'text-slate-600'}`}>
        {available ? 'Disponível' : 'Planejado · indisponível'}
      </p>
      <h3 className="mt-2 text-lg font-semibold text-slate-900" id={`module-${module.id}`}>
        {module.name}
      </h3>
      <p className="mt-2 text-sm leading-6 text-slate-600">{module.description}</p>
      {available && (
        <ul className="mt-4 flex flex-wrap gap-2">
          {features.map((feature) => (
            <li key={feature.id}>
              <Link
                className="inline-flex min-h-11 items-center rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white transition hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
                to={feature.route}
              >
                {feature.name}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </article>
  )
}

export function ModuleNavigation() {
  return (
    <div className="mt-8 space-y-10">
      {functionalAreas.map((area) => (
        <section aria-labelledby={`area-${area.id}`} id={area.id} key={area.id}>
          <h2 className="text-2xl font-semibold tracking-tight text-slate-900" id={`area-${area.id}`}>
            {area.name}
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600">{area.description}</p>
          <nav aria-label={`Módulos de ${area.name}`} className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {navigationModules.filter((module) => module.area === area.id).map((module) => (
              <ModuleCard key={module.id} module={module} />
            ))}
          </nav>
        </section>
      ))}
    </div>
  )
}
