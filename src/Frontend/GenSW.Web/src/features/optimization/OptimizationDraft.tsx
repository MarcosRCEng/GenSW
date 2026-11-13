import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { display, message } from '../formulation/api'
import { button, Check, Choice, ErrorNotice, Field, primary, Section, ServerSelect } from '../formulation/FormControls'
import type { Item, Recipe, RecipeHeader } from '../formulation/types'
import { savedRecordState } from '../../shared/details/listNavigation'
import { previewDraft, recoverDraft, submitDraft } from './api'
import { eligibleOutput, itemLabel } from './formData'
import { RecoveryNotice } from './RecoveryNotice'
import { useRecoverableCommand } from './useRecoverableCommand'
import type { DraftCommand, DraftDestination, DraftPreview, DraftReceipt, OptimizationExecution } from './types'

export function OptimizationDraft({ execution }: { execution: OptimizationExecution }) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const action = useRef<HTMLButtonElement>(null)
  const destinationTitle = useRef<HTMLHeadingElement>(null)
  const previewTitle = useRef<HTMLHeadingElement>(null)
  const [open, setOpen] = useState(false)
  const [mode, setMode] = useState('nova')
  const [recipe, setRecipe] = useState<Recipe | null>(null)
  const [header, setHeader] = useState<RecipeHeader>({ codigo: '', nome: '', finalidade: '' })
  const [output, setOutput] = useState<Item | null>(null)
  const [review, setReview] = useState<{ destination: DraftDestination; preview: DraftPreview } | null>(null)
  const [acceptResidual, setAcceptResidual] = useState(false)
  const [acceptWarnings, setAcceptWarnings] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const request = useRecoverableCommand(`${user?.userId ?? 'sessao'}:rascunho:${execution.id}`,
    (body: DraftCommand, key) => submitDraft(execution.id, body, key),
    (key) => recoverDraft(execution.id, key))

  useEffect(() => {
    if (open) destinationTitle.current?.focus()
  }, [open])

  useEffect(() => {
    if (review) previewTitle.current?.focus()
  }, [review])

  function invalidate() {
    setReview(null)
    setAcceptResidual(false)
    setAcceptWarnings(false)
    setError(null)
    request.clearError()
  }

  async function prepare(event: FormEvent) {
    event.preventDefault()
    if (busy || request.key) return
    invalidate()
    if (!output || (mode === 'existente' && !recipe)) {
      setError('Escolha o destino da receita e o item de saída.')
      return
    }
    const destination: DraftDestination = {
      receitaId: mode === 'existente' ? recipe?.id ?? null : null,
      receitaRevisao: mode === 'existente' ? recipe?.revisao ?? null : null,
      novaReceita: mode === 'nova' ? { codigo: header.codigo.trim(), nome: header.nome.trim(), finalidade: header.finalidade.trim() } : null,
      saidaItemId: output.id,
      saidaItemRevisao: output.revisao,
    }
    setBusy(true)
    try {
      setReview({ destination, preview: await previewDraft(execution.id, destination) })
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }

  async function openEditor(promise: Promise<DraftReceipt | undefined> | undefined) {
    const result = await promise
    if (!result) return
    const target = `/producao/receitas/versoes/${result.versaoId}/editar?${new URLSearchParams({ otimizacao: result.otimizacaoId })}`
    navigate(target, { state: savedRecordState(undefined, target) })
  }

  return <Section title="Rascunho de receita">
    <p>Crie um rascunho para revisar as quantidades. A publicação da receita é uma ação posterior.</p>
    <RecoveryNotice keyValue={request.key} busy={request.busy} absent={request.absent} canRetry={request.canRetry} error={request.error}
      label="o rascunho" onConsult={() => void openEditor(request.consult())} onRetry={() => void openEditor(request.retry())} onClear={request.clear} />
    {!open && <button ref={action} type="button" className={button} disabled={Boolean(request.key)} onClick={() => setOpen(true)}>Preparar rascunho desta solução</button>}
    {open && <>
      <form className="space-y-4" onSubmit={(event) => void prepare(event)}>
        <h3 ref={destinationTitle} tabIndex={-1} className="font-semibold">Destino e saída</h3>
        <fieldset className="min-w-0 space-y-4" disabled={busy || Boolean(request.key)}>
          <Choice label="Destino do rascunho" options={['nova', 'existente']} captions={{ nova: 'Nova receita', existente: 'Nova versão de receita existente' }}
            value={mode} onChange={(v) => { setMode(v); invalidate() }} />
          {mode === 'nova' ? <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Código da nova receita" value={header.codigo} onChange={(v) => { setHeader((h) => ({ ...h, codigo: v })); invalidate() }} required />
            <Field label="Nome da nova receita" value={header.nome} onChange={(v) => { setHeader((h) => ({ ...h, nome: v })); invalidate() }} required />
            <Field label="Finalidade da nova receita" value={header.finalidade} onChange={(v) => { setHeader((h) => ({ ...h, finalidade: v })); invalidate() }} required />
          </div> : <ServerSelect<Recipe> label="Receita de destino" path="/receitas" params={{ ativo: true }} value={recipe?.id ?? null}
            caption={(r) => `${r.codigo} · ${r.nome}`} onChange={(_, r) => { setRecipe(r ?? null); invalidate() }} />}
          <ServerSelect<Item> label="Item de saída" path="/itens" params={{ ativo: true, classe: 'Alimentar', capacidade: 'producao' }} value={output?.id ?? null}
            eligible={eligibleOutput} caption={itemLabel} onChange={(_, i) => { setOutput(i ?? null); invalidate() }} />
          <div className="flex flex-wrap gap-2">
            <button type="submit" className={button}>{busy ? 'Preparando prévia…' : 'Visualizar prévia do rascunho'}</button>
            <button type="button" className={button} onClick={() => { setOpen(false); invalidate(); window.requestAnimationFrame(() => action.current?.focus()) }}>Cancelar preparação</button>
          </div>
        </fieldset>
      </form>
      <ErrorNotice error={error} />
      {review && <div className="space-y-4 rounded-xl border p-4">
        <h3 ref={previewTitle} tabIndex={-1} className="font-semibold">Prévia do rascunho</h3>
        <p>{mode === 'nova' ? header.nome : recipe?.nome} · saída {output?.nome} · {display(review.preview.conteudo.tamanhoReferencia)} {review.preview.conteudo.unidadeReferencia} BN</p>
        <ul className="list-disc space-y-1 pl-5">{review.preview.conteudo.entradas.map((line) => <li key={line.id}>
          {execution.entradas.candidatos.find((c) => c.itemId === line.itemId)?.nome ?? line.itemId} · {display(line.quantidade)} {line.unidade}
        </li>)}</ul>
        {review.preview.exigeAceiteResiduos && <div className="space-y-2">
          <ul className="list-disc pl-5 text-sm" aria-label="Resíduos de arredondamento">
            {Object.entries(execution.resultado?.motor?.candidate?.residualsKg ?? {}).filter(([, value]) => !/^[-+]?0(?:\.0+)?$/.test(value)).map(([id, value]) => <li key={id}>
              {execution.entradas.candidatos.find((c) => c.id === id)?.nome ?? id} · {display(value)} kg
            </li>)}
          </ul>
          <Check label="Aceito as quantidades arredondadas e os resíduos apresentados" checked={acceptResidual} onChange={setAcceptResidual} />
        </div>}
        {review.preview.avisos.length > 0 && <>
          <ul className="list-disc space-y-1 rounded-lg bg-amber-50 p-4 pl-8 text-sm">{review.preview.avisos.map((warning, i) => <li key={i}>{warning}</li>)}</ul>
          <Check label="Conferi e aceito os avisos desta prévia" checked={acceptWarnings} onChange={setAcceptWarnings} />
        </>}
        {request.errorStatus === 409 && <p>Confira o destino e gere outra prévia antes de confirmar.</p>}
        <button type="button" className={primary} disabled={busy || request.busy || Boolean(request.key) || request.errorStatus === 409 || (review.preview.exigeAceiteResiduos && !acceptResidual) || (review.preview.avisos.length > 0 && !acceptWarnings)}
          onClick={() => void openEditor(request.submit({ destino: review.destination, hashPrevia: review.preview.hash, aceitaResiduos: acceptResidual, aceitaAvisos: acceptWarnings }))}>
          Confirmar criação do rascunho
        </button>
      </div>}
    </>}
  </Section>
}
