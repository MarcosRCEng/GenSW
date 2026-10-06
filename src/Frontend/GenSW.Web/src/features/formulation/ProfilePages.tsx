import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { decimal, display, message, read, send } from './api'
import {
  button,
  Choice,
  ErrorNotice,
  Field,
  Frame,
  primary,
  Section,
  ServerSelect,
} from './FormControls'
import type { Component, NutrientValue, Profile, ProfileData } from './types'

const getProfile = (id: string) => read<Profile>(`/perfis-nutricionais/${id}`)
const empty: ProfileData = {
  nome: '',
  fonte: '',
  metodo: '',
  preparacao: '',
  referenciaAmostra: '',
  contexto: '',
  especieId: null,
  fase: null,
  dataFonte: null,
  dataColeta: null,
  valores: [],
}
export function ProfileFormPage() {
  const { id, profileId } = useParams()
  const [params] = useSearchParams()
  const source = params.get('origem')
  const navigate = useNavigate()
  const [data, setData] = useState<ProfileData>(empty)
  const [revision, setRevision] = useState(0)
  const [components, setComponents] = useState<Component[]>([])
  const [loading, setLoading] = useState(Boolean(profileId || source))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true
    void read<Component[]>('/componentes-nutricionais')
      .then((c) => {
        if (live) setComponents(c)
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [retry])
  useEffect(() => {
    const target = profileId ?? source
    if (!target) return
    let live = true
    setLoading(true)
    void getProfile(target)
      .then((p) => {
        if (live) {
          if (profileId && p.estado !== 'Rascunho') {
            setError('Versão publicada é imutável; crie nova revisão.')
            return
          }
          setData(p.conteudo)
          setRevision(p.revisao)
          setLoading(false)
        }
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [profileId, source, retry])
  const change = <K extends keyof ProfileData>(k: K, v: ProfileData[K]) =>
    setData((d) => ({ ...d, [k]: v }))
  const updateValue = (index: number, patch: Partial<NutrientValue>) =>
    setData((d) => ({
      ...d,
      valores: d.valores.map((v, i) => (i === index ? { ...v, ...patch } : v)),
    }))
  const add = () =>
    change('valores', [
      ...data.valores,
      {
        componente: 'PB',
        estado: 'Desconhecido',
        valor: null,
        origem: null,
        base: 'BN',
        unidade: 'g/kg',
        metodo: '',
        contexto: '',
        qualificador: 'Pontual',
        motivo: '',
        fonte: null,
        hipotese: null,
      },
    ])
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const payload = {
        ...data,
        valores: data.valores.map((v) => ({
          ...v,
          valor:
            v.estado === 'Conhecido' && v.valor !== null
              ? decimal(v.valor)
              : null,
        })),
      }
      const p = await send<Profile>(
        profileId
          ? `/perfis-nutricionais/${profileId}`
          : `/itens/${id}/perfis-nutricionais`,
        profileId ? { conteudo: payload, versaoEsperada: revision } : payload,
        profileId ? 'PUT' : 'POST',
      )
      navigate(`/producao/perfis/${p.id}`)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame
      title={
        profileId
          ? 'Editar perfil nutricional'
          : source
            ? 'Nova revisão do perfil'
            : 'Novo perfil nutricional'
      }
    >
      <Link
        className={button}
        to={profileId ? `/producao/perfis/${profileId}` : `/itens/${id}`}
      >
        Voltar
      </Link>
      <ErrorNotice error={error} />
      {loading ? (
        <>
          <p role="status">Carregando perfil…</p>
          <button className={button} onClick={() => setRetry((v) => v + 1)}>
            Tentar novamente
          </button>
        </>
      ) : (
        <form className="space-y-6" onSubmit={save}>
          <Section title="Fonte e aplicabilidade">
            <p>
              Valores são informados pelo usuário. Rascunhos incompletos podem
              ser salvos; publicação exige fonte, método, preparação e amostra.
            </p>
            <div className="grid gap-4 sm:grid-cols-2">
              {(
                [
                  'nome',
                  'fonte',
                  'metodo',
                  'preparacao',
                  'referenciaAmostra',
                  'contexto',
                ] as const
              ).map((k) => (
                <Field
                  key={k}
                  label={
                    {
                      nome: 'Nome do perfil',
                      fonte: 'Fonte / identificador do laudo',
                      metodo: 'Método geral',
                      preparacao: 'Preparação / parte analisada',
                      referenciaAmostra:
                        'Referência de amostra / lote documental',
                      contexto: 'Aplicabilidade e estado/processo',
                    }[k]
                  }
                  value={data[k]}
                  onChange={(v) => change(k, v)}
                  required={k === 'nome'}
                />
              ))}
              <Field
                label="Data da fonte (AAAA-MM-DD, opcional)"
                value={data.dataFonte ?? ''}
                onChange={(v) => change('dataFonte', v || null)}
              />
              <Field
                label="Data da coleta (AAAA-MM-DD, opcional)"
                value={data.dataColeta ?? ''}
                onChange={(v) => change('dataColeta', v || null)}
              />
              <Field
                label="Fase de uso (opcional)"
                value={data.fase ?? ''}
                onChange={(v) => change('fase', v || null)}
              />
            </div>
            <ServerSelect<{ id: string; nomeComum: string }>
              label="Espécie de aplicabilidade (opcional)"
              path="/especies"
              value={data.especieId}
              onChange={(v) => change('especieId', v)}
              caption={(s) => s.nomeComum}
            />
          </Section>
          <Section title="Observações por componente">
            {data.valores.map((value, index) => (
              <fieldset className="space-y-3 rounded-xl border p-4" key={index}>
                <legend className="px-2 font-semibold">
                  Observação {index + 1}
                </legend>
                <div className="grid gap-3 sm:grid-cols-3">
                  <Choice
                    label="Componente"
                    value={value.componente}
                    options={components.map((c) => c.codigo)}
                    onChange={(v) =>
                      updateValue(index, {
                        componente: v,
                        unidade:
                          components.find((c) => c.codigo === v)?.grandeza ===
                          'energia'
                            ? 'MJ/kg'
                            : 'g/kg',
                      })
                    }
                  />
                  <Choice
                    label="Estado do valor"
                    value={value.estado}
                    options={['Conhecido', 'Desconhecido', 'NaoAplicavel']}
                    onChange={(v) =>
                      updateValue(index, {
                        estado: v,
                        valor: v === 'Conhecido' ? (value.valor ?? '') : null,
                        origem:
                          v === 'Conhecido'
                            ? (value.origem ?? 'Declarado')
                            : null,
                      })
                    }
                  />
                  <Choice
                    label="Base"
                    value={value.base}
                    options={['BN', 'MS']}
                    onChange={(v) => updateValue(index, { base: v })}
                  />
                  <Choice
                    label="Unidade do valor"
                    value={value.unidade}
                    options={
                      components.find((c) => c.codigo === value.componente)
                        ?.grandeza === 'energia'
                        ? ['MJ/kg', 'kcal/kg']
                        : ['g/kg', 'g/100g', '%', 'mg/kg']
                    }
                    onChange={(v) => updateValue(index, { unidade: v })}
                  />
                  {value.estado === 'Conhecido' && (
                    <>
                      <Field
                        label="Valor (zero explícito permitido)"
                        value={value.valor ?? ''}
                        onChange={(v) => updateValue(index, { valor: v })}
                        required
                      />
                      <Choice
                        label="Origem do valor"
                        value={value.origem ?? 'Declarado'}
                        options={['Medido', 'Declarado', 'Estimado']}
                        onChange={(v) => updateValue(index, { origem: v })}
                      />
                    </>
                  )}
                  <Choice
                    label="Qualificador da fonte"
                    value={value.qualificador}
                    options={[
                      'Pontual',
                      'Minimo',
                      'Maximo',
                      'Faixa',
                      'AbaixoDeteccao',
                    ]}
                    onChange={(v) => updateValue(index, { qualificador: v })}
                  />
                  <Field
                    label="Método do componente"
                    value={value.metodo}
                    onChange={(v) => updateValue(index, { metodo: v })}
                  />
                  <Field
                    label="Contexto do componente / energia"
                    value={value.contexto}
                    onChange={(v) => updateValue(index, { contexto: v })}
                  />
                  <Field
                    label="Fonte específica (opcional)"
                    value={value.fonte ?? ''}
                    onChange={(v) => updateValue(index, { fonte: v || null })}
                  />
                </div>
                <p className="text-sm text-slate-600">
                  {
                    components.find((c) => c.codigo === value.componente)
                      ?.semantica
                  }
                </p>
                {(value.estado !== 'Conhecido' ||
                  value.qualificador !== 'Pontual') && (
                  <Field
                    label="Motivo / qualificador e limites da lacuna"
                    value={value.motivo ?? ''}
                    multiline
                    onChange={(v) => updateValue(index, { motivo: v || null })}
                    required={value.estado !== 'Conhecido'}
                  />
                )}
                {value.origem === 'Estimado' && (
                  <Field
                    label="Hipótese, equação e perfis/fontes da estimativa"
                    value={value.hipotese ?? ''}
                    multiline
                    onChange={(v) =>
                      updateValue(index, { hipotese: v || null })
                    }
                    required
                  />
                )}
                <button
                  type="button"
                  className={button}
                  onClick={() =>
                    change(
                      'valores',
                      data.valores.filter((_, i) => i !== index),
                    )
                  }
                >
                  Remover observação {index + 1}
                </button>
              </fieldset>
            ))}
            <button type="button" className={button} onClick={add}>
              Adicionar observação
            </button>
            <p className="text-sm text-slate-600">
              Limites mínimos/máximos, faixas e abaixo de detecção ficam
              indeterminados no cálculo escalar. Porção exige normalização
              explícita na fonte.
            </p>
          </Section>
          <button className={primary} disabled={busy}>
            {busy ? 'Salvando…' : 'Salvar rascunho'}
          </button>
        </form>
      )}
    </Frame>
  )
}
export function ProfileDetailsPage() {
  const { profileId } = useParams()
  const { record: p, state, retry } = useRecordDetails(profileId, getProfile)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const command = async (operation: string) => {
    if (!p) return
    setError(null)
    setBusy(true)
    try {
      await send(`/perfis-nutricionais/${p.id}/${operation}`, {
        versaoEsperada: p.revisao,
      })
      retry()
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <DetailsPage
      title="Visualizar perfil nutricional"
      listPath={p ? `/itens/${p.itemId}` : '/itens'}
      listLabel="Item"
      state={state}
      onRetry={retry}
      editPath={
        p?.estado === 'Rascunho' ? `/producao/perfis/${p.id}/editar` : undefined
      }
    >
      {p && (
        <>
          <Section title={`${p.conteudo.nome} · v${p.numero} · ${p.estado}`}>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailField label="Fonte">{p.conteudo.fonte}</DetailField>
              <DetailField label="Método">{p.conteudo.metodo}</DetailField>
              <DetailField label="Preparação / parte analisada">
                {p.conteudo.preparacao}
              </DetailField>
              <DetailField label="Amostra / lote documental">
                {p.conteudo.referenciaAmostra}
              </DetailField>
              <DetailField label="Aplicabilidade">
                {p.conteudo.contexto}
              </DetailField>
              <DetailField label="Espécie / fase">{`${p.conteudo.especieId ?? 'Não informada'} / ${p.conteudo.fase ?? 'Não informada'}`}</DetailField>
              <DetailField label="Datas da fonte / coleta">{`${p.conteudo.dataFonte ?? 'Não informada'} / ${p.conteudo.dataColeta ?? 'Não informada'}`}</DetailField>
            </dl>
            <ErrorNotice error={error} />
            <div className="flex flex-wrap gap-3">
              {p.estado === 'Rascunho' && (
                <button
                  className={primary}
                  disabled={busy}
                  onClick={() => void command('publicacao')}
                >
                  Publicar perfil
                </button>
              )}
              {p.estado === 'Publicado' && (
                <button
                  className={button}
                  disabled={busy}
                  onClick={() => void command('inativacao')}
                >
                  Inativar versão
                </button>
              )}
              <Link
                className={button}
                to={`/itens/${p.itemId}/perfis/novo?origem=${p.id}`}
              >
                Criar nova revisão
              </Link>
            </div>
          </Section>
          <Section title="Valores e proveniência">
            {p.conteudo.valores.map((v) => (
              <article
                className="space-y-2 rounded-lg border p-4"
                key={v.componente}
              >
                <h3 className="font-semibold">
                  {v.componente} · {v.estado} · {v.qualificador}
                </h3>
                <p>
                  {v.valor === null
                    ? 'Valor desconhecido / não aplicável'
                    : `${display(v.valor)} ${v.unidade} ${v.base}`}{' '}
                  · {v.origem ?? 'Origem não informada'}
                </p>
                <p>
                  Método: {v.metodo} · Contexto: {v.contexto || 'Não informado'}
                </p>
                <p>Fonte: {v.fonte ?? p.conteudo.fonte}</p>
                {v.motivo && <p>Motivo: {v.motivo}</p>}
                {v.hipotese && <p>Hipótese: {v.hipotese}</p>}
              </article>
            ))}
          </Section>
        </>
      )}
    </DetailsPage>
  )
}
