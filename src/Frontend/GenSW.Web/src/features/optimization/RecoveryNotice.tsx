import { button, ErrorNotice } from '../formulation/FormControls'

export function RecoveryNotice({ keyValue, busy, absent, error, canRetry, onConsult, onRetry, onClear, label }: {
  keyValue: string | null
  busy: boolean
  absent: boolean
  error: string | null
  canRetry: boolean
  onConsult: () => void
  onRetry: () => void
  onClear: () => void
  label: string
}) {
  return <>
    <ErrorNotice error={error} />
    {keyValue && <div className="space-y-3 rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm">
      <p>{busy ? 'Aguardando a API…' : `Consulte ${label} para saber se o servidor recebeu o comando.`}</p>
      <p className="break-all">Chave do comando {keyValue}</p>
      {absent && <p role="status">O servidor ainda não encontrou o comando com esta chave.</p>}
      <div className="flex flex-wrap gap-2">
        <button type="button" className={button} disabled={busy} onClick={onConsult}>Consultar {label}</button>
        {canRetry && <button type="button" className={button} disabled={busy} onClick={onRetry}>Reenviar o mesmo comando</button>}
        {absent && <button type="button" className={button} disabled={busy} onClick={onClear}>Liberar formulário para novo comando</button>}
      </div>
    </div>}
  </>
}
