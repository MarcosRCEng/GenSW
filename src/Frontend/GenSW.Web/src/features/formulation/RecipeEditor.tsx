import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { read } from './api'
import {
  button,
  Check,
  Choice,
  Field,
  Section,
  ServerSelect,
} from './FormControls'
import type {
  Component,
  Conversion,
  Item,
  Profile,
  RecipeData,
  RecipeInput,
  RecipeOutput,
  RecipeVersion,
} from './types'

import { newInput, newOutput } from './recipeData'
function SelectedData({
  itemId,
  profileId,
  conversionId,
  change,
}: {
  itemId: string
  profileId: string | null
  conversionId: string | null
  change: (patch: {
    perfilId?: string | null
    conversaoId?: string | null
  }) => void
}) {
  return itemId ? (
    <div className="grid gap-3 lg:grid-cols-2">
      <ServerSelect<Profile>
        label="Perfil publicado (escolha explícita)"
        path={`/itens/${itemId}/perfis-nutricionais`}
        value={profileId}
        onChange={(v) => change({ perfilId: v })}
        caption={(p) =>
          `${p.conteudo.nome} · v${p.numero} · ${p.conteudo.referenciaAmostra}`
        }
        params={{ estado: 'Publicado' }}
      />
      <ServerSelect<Conversion>
        label="Conversão explícita (quando necessária)"
        path={`/itens/${itemId}/conversoes`}
        value={conversionId}
        onChange={(v) => change({ conversaoId: v })}
        caption={(c) =>
          `v${c.numero}: ${c.origem} → ${c.destino} · ${c.contexto}`
        }
      />
    </div>
  ) : null
}
function InputEditor({
  input,
  onChange,
  onRemove,
  index,
  mode,
}: {
  input: RecipeInput
  onChange: (patch: Partial<RecipeInput>) => void
  onRemove: () => void
  index: number
  mode: string
}) {
  const [sub, setSub] = useState<RecipeVersion | null>(null)
  useEffect(() => {
    let live = true
    setSub(null)
    if (input.subReceitaVersaoId)
      void read<RecipeVersion>(`/receitas/versoes/${input.subReceitaVersaoId}`)
        .then((s) => {
          if (live) setSub(s)
        })
        .catch(() => {
          /* Submit revalidates stale references and details remain accessible. */
        })
    return () => {
      live = false
    }
  }, [input.subReceitaVersaoId])
  return (
    <fieldset className="space-y-4 rounded-xl border p-4">
      <legend className="px-2 font-semibold">Entrada {index + 1}</legend>
      <ServerSelect<Item>
        label={`Item da entrada ${index + 1}`}
        path="/itens"
        value={input.itemId || null}
        onChange={(v, i) =>
          onChange({
            itemId: v ?? '',
            papel: i?.classe ?? input.papel,
            unidade: i?.unidade ?? input.unidade,
            perfilId: null,
            conversaoId: null,
          })
        }
        caption={(i) => `${i.codigo} — ${i.nome}`}
        params={{ ativo: true, capacidade: 'entrada' }}
      />
      <div className="grid gap-3 sm:grid-cols-3">
        <Choice
          label="Papel da entrada"
          value={input.papel}
          options={[
            'Alimentar',
            'OutroMaterialIncorporado',
            'Embalagem',
            'Consumivel',
          ]}
          onChange={(v) => onChange({ papel: v })}
        />
        <Field
          label={
            mode === 'Percentual' &&
            ['Alimentar', 'OutroMaterialIncorporado'].includes(input.papel)
              ? 'Percentual da massa BN'
              : 'Quantidade da entrada'
          }
          value={input.quantidade}
          onChange={(v) => onChange({ quantidade: v })}
          required
        />
        <Choice
          label="Unidade da entrada"
          value={input.unidade}
          options={['kg', 'g', 'L', 'mL', 'un']}
          onChange={(v) => onChange({ unidade: v })}
        />
        <Choice
          label="Escala da entrada"
          value={input.escala}
          options={['Variavel', 'Fixa']}
          onChange={(v) => onChange({ escala: v })}
        />
        <Field
          label="Inclusão mínima % (opcional)"
          value={input.inclusaoMinima ?? ''}
          onChange={(v) => onChange({ inclusaoMinima: v || null })}
        />
        <Field
          label="Inclusão máxima % (opcional)"
          value={input.inclusaoMaxima ?? ''}
          onChange={(v) => onChange({ inclusaoMaxima: v || null })}
        />
      </div>
      <SelectedData
        itemId={input.itemId}
        profileId={input.perfilId}
        conversionId={input.conversaoId}
        change={onChange}
      />
      <details className="space-y-3">
        <summary className="cursor-pointer font-medium">
          Sub-receita: simular fabricação do intermediário
        </summary>
        <p className="text-sm">
          Escolha versão e saída específicas. Com perfil próprio selecionado,
          usa-se a composição do intermediário; sem perfil, a mistura é
          expandida uma única vez.
        </p>
        <ServerSelect<RecipeVersion>
          label="Versão publicada da sub-receita"
          path="/receitas/versoes"
          value={input.subReceitaVersaoId}
          onChange={(v, version) => {
            setSub(version ?? null)
            onChange({ subReceitaVersaoId: v, subSaidaId: null })
          }}
          caption={(v) =>
            `v${v.numero} · ${v.conteudo.observacao ?? v.receitaId}`
          }
          params={{ estado: 'Publicado', ativo: true }}
        />
        {sub && (
          <Choice
            label="Saída da sub-receita"
            value={input.subSaidaId ?? ''}
            options={['', ...sub.conteudo.saidas.map((x) => x.id)]}
            captions={Object.fromEntries(
              sub.conteudo.saidas.map((x) => [
                x.id,
                `${x.nome} · ${x.quantidade} ${x.unidade}`,
              ]),
            )}
            onChange={(v) => {
              const o = sub.conteudo.saidas.find((x) => x.id === v)
              onChange({
                subSaidaId: v || null,
                itemId: o?.itemId ?? input.itemId,
                unidade: o?.unidade ?? input.unidade,
              })
            }}
          />
        )}
        {sub && (
          <ul className="text-sm">
            {sub.conteudo.saidas.map((o) => (
              <li key={o.id}>
                {o.id} — {o.nome}, {o.quantidade} {o.unidade}
              </li>
            ))}
          </ul>
        )}
      </details>
      <button className={button} type="button" onClick={onRemove}>
        Remover entrada {index + 1}
      </button>
    </fieldset>
  )
}
function OutputEditor({
  output,
  onChange,
  onRemove,
  index,
}: {
  output: RecipeOutput
  onChange: (patch: Partial<RecipeOutput>) => void
  onRemove: () => void
  index: number
}) {
  return (
    <fieldset className="space-y-4 rounded-xl border p-4">
      <legend className="px-2 font-semibold">Saída {index + 1}</legend>
      <ServerSelect<Item>
        label={`Item da saída ${index + 1}`}
        path="/itens"
        value={output.itemId || null}
        onChange={(v, i) =>
          onChange({
            itemId: v ?? '',
            nome: i?.nome ?? output.nome,
            unidade: i?.unidade ?? output.unidade,
            perfilId: null,
            conversaoId: null,
          })
        }
        caption={(i) => `${i.codigo} — ${i.nome}`}
        params={{ ativo: true, capacidade: 'producao' }}
      />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field
          label="Nome da saída"
          value={output.nome}
          onChange={(v) => onChange({ nome: v })}
          required
        />
        <Field
          label="Quantidade esperada"
          value={output.quantidade}
          onChange={(v) => onChange({ quantidade: v })}
          required
        />
        <Choice
          label="Unidade da saída"
          value={output.unidade}
          options={['kg', 'g', 'L', 'mL', 'un']}
          onChange={(v) => onChange({ unidade: v })}
        />
        <Choice
          label="Escala da saída"
          value={output.escala}
          options={['Variavel', 'Fixa']}
          onChange={(v) => onChange({ escala: v })}
        />
      </div>
      <Check
        label="Saída principal"
        checked={output.principal}
        onChange={(v) => onChange({ principal: v })}
      />
      <SelectedData
        itemId={output.itemId}
        profileId={output.perfilId}
        conversionId={output.conversaoId}
        change={onChange}
      />
      <details className="space-y-3">
        <summary className="cursor-pointer font-medium">
          Medições esperadas do conteúdo (documentais)
        </summary>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field
            label="Massa líquida kg (opcional)"
            value={output.massaLiquidaKg ?? ''}
            onChange={(v) => onChange({ massaLiquidaKg: v || null })}
          />
          <Field
            label="Massa drenada kg (opcional)"
            value={output.massaDrenadaKg ?? ''}
            onChange={(v) => onChange({ massaDrenadaKg: v || null })}
          />
          <Field
            label="Massa bruta kg (opcional)"
            value={output.massaBrutaKg ?? ''}
            onChange={(v) => onChange({ massaBrutaKg: v || null })}
          />
        </div>
        <Field
          label="Método e escopo das medições"
          value={output.metodoMedicao ?? ''}
          onChange={(v) => onChange({ metodoMedicao: v || null })}
          multiline
        />
        <p className="text-sm">
          As medições não substituem conversão de unidade nem um perfil da
          fração drenada.
        </p>
      </details>
      <button className={button} type="button" onClick={onRemove}>
        Remover saída {index + 1}
      </button>
    </fieldset>
  )
}
export function RecipeEditor({
  data,
  onChange,
}: {
  data: RecipeData
  onChange: (data: RecipeData) => void
}) {
  const [components, setComponents] = useState<Component[]>([])
  useEffect(() => {
    let live = true
    void read<Component[]>('/componentes-nutricionais')
      .then((c) => {
        if (live) setComponents(c)
      })
      .catch(() => {})
    return () => {
      live = false
    }
  }, [])
  const change = <K extends keyof RecipeData>(key: K, v: RecipeData[K]) =>
    onChange({ ...data, [key]: v })
  return (
    <>
      <Section title="Tipo e referência">
        <div className="grid gap-4 sm:grid-cols-2">
          <Choice
            label="Tipo de receita"
            value={data.tipo}
            options={['MisturaSimples', 'Processamento']}
            onChange={(v) => change('tipo', v)}
          />
          <Choice
            label="Modo das entradas incorporadas"
            value={data.modo}
            options={['Quantidade', 'Percentual']}
            onChange={(v) => change('modo', v)}
          />
          <Field
            label="Tamanho de referência"
            value={data.tamanhoReferencia}
            onChange={(v) => change('tamanhoReferencia', v)}
            required
          />
          <Choice
            label="Unidade de referência"
            value={data.unidadeReferencia}
            options={['kg', 'L', 'un']}
            onChange={(v) => change('unidadeReferencia', v)}
          />
        </div>
        <p className="text-sm text-slate-600">
          Mistura simples conserva massa, sem perdas/alteração de umidade.
          Processamento exige composição explícita da saída ou hipótese por
          componente. Percentuais incorporados totalizam 100; embalagens ficam
          em quantidades absolutas.
        </p>
        <Field
          label="Observações e hipóteses gerais"
          value={data.observacao ?? ''}
          onChange={(v) => change('observacao', v || null)}
          multiline
        />
      </Section>
      <Section title="Entradas">
        {data.entradas.map((input, index) => (
          <InputEditor
            key={input.id}
            input={input}
            index={index}
            mode={data.modo}
            onChange={(patch) =>
              change(
                'entradas',
                data.entradas.map((v, i) =>
                  i === index ? { ...v, ...patch } : v,
                ),
              )
            }
            onRemove={() =>
              change(
                'entradas',
                data.entradas.filter((_, i) => i !== index),
              )
            }
          />
        ))}
        <button
          className={button}
          type="button"
          onClick={() => change('entradas', [...data.entradas, newInput()])}
        >
          Adicionar entrada
        </button>
      </Section>
      <Section title="Saídas esperadas">
        {data.saidas.map((output, index) => (
          <OutputEditor
            key={output.id}
            output={output}
            index={index}
            onChange={(patch) =>
              change(
                'saidas',
                data.saidas.map((v, i) =>
                  i === index ? { ...v, ...patch } : v,
                ),
              )
            }
            onRemove={() =>
              change(
                'saidas',
                data.saidas.filter((_, i) => i !== index),
              )
            }
          />
        ))}
        <button
          className={button}
          type="button"
          onClick={() =>
            change('saidas', [
              ...data.saidas,
              { ...newOutput(), principal: data.saidas.length === 0 },
            ])
          }
        >
          Adicionar saída
        </button>
      </Section>
      <Section title="Perdas esperadas e etapas">
        {data.perdas.map((p, index) => (
          <div className="space-y-3 rounded-lg border p-3" key={index}>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field
                label={`Quantidade da perda ${index + 1}`}
                value={p.quantidade}
                onChange={(v) =>
                  change(
                    'perdas',
                    data.perdas.map((x, i) =>
                      i === index ? { ...x, quantidade: v } : x,
                    ),
                  )
                }
                required
              />
              <Choice
                label="Unidade da perda"
                value={p.unidade}
                options={['kg', 'g', 'L', 'mL', 'un']}
                onChange={(v) =>
                  change(
                    'perdas',
                    data.perdas.map((x, i) =>
                      i === index ? { ...x, unidade: v } : x,
                    ),
                  )
                }
              />
              <Choice
                label="Escala da perda"
                value={p.escala}
                options={['Variavel', 'Fixa']}
                onChange={(v) =>
                  change(
                    'perdas',
                    data.perdas.map((x, i) =>
                      i === index ? { ...x, escala: v } : x,
                    ),
                  )
                }
              />
            </div>
            <Field
              label="Motivo da perda"
              value={p.motivo}
              onChange={(v) =>
                change(
                  'perdas',
                  data.perdas.map((x, i) =>
                    i === index ? { ...x, motivo: v } : x,
                  ),
                )
              }
              required
            />
            <button
              type="button"
              className={button}
              onClick={() =>
                change(
                  'perdas',
                  data.perdas.filter((_, i) => i !== index),
                )
              }
            >
              Remover perda
            </button>
          </div>
        ))}
        <button
          className={button}
          type="button"
          onClick={() =>
            change('perdas', [
              ...data.perdas,
              { quantidade: '', unidade: 'kg', motivo: '', escala: 'Variavel' },
            ])
          }
        >
          Adicionar perda
        </button>
        {data.etapas.map((step, index) => (
          <div className="space-y-2" key={index}>
            <Field
              label={`Etapa ${index + 1}`}
              value={step.descricao}
              onChange={(v) =>
                change(
                  'etapas',
                  data.etapas.map((x, i) =>
                    i === index ? { ...x, descricao: v } : x,
                  ),
                )
              }
              multiline
            />
            <button
              type="button"
              className={button}
              onClick={() =>
                change(
                  'etapas',
                  data.etapas
                    .filter((_, i) => i !== index)
                    .map((x, i) => ({ ...x, ordem: i + 1 })),
                )
              }
            >
              Remover etapa
            </button>
          </div>
        ))}
        <button
          className={button}
          type="button"
          onClick={() =>
            change('etapas', [
              ...data.etapas,
              { ordem: data.etapas.length + 1, descricao: '' },
            ])
          }
        >
          Adicionar etapa
        </button>
      </Section>
      {data.tipo === 'Processamento' && (
        <Section title="Retenções explícitas da saída única">
          <p>
            Hipótese por componente, fator 0–1. MS da saída exige retenção de
            MS; rendimento sozinho não conserva nutrientes.
          </p>
          {data.retencoes.map((r, index) => (
            <div className="space-y-3 rounded-lg border p-3" key={index}>
              <div className="grid gap-3 sm:grid-cols-2">
                <Choice
                  label="Componente retido"
                  value={r.componente}
                  options={components.map((c) => c.codigo)}
                  onChange={(v) =>
                    change(
                      'retencoes',
                      data.retencoes.map((x, i) =>
                        i === index ? { ...x, componente: v } : x,
                      ),
                    )
                  }
                />
                <Field
                  label="Fator de retenção"
                  value={r.fator}
                  onChange={(v) =>
                    change(
                      'retencoes',
                      data.retencoes.map((x, i) =>
                        i === index ? { ...x, fator: v } : x,
                      ),
                    )
                  }
                  required
                />
              </div>
              <Field
                label="Hipótese e destino da fração não retida"
                value={r.hipotese}
                onChange={(v) =>
                  change(
                    'retencoes',
                    data.retencoes.map((x, i) =>
                      i === index ? { ...x, hipotese: v } : x,
                    ),
                  )
                }
                multiline
                required
              />
              <button
                type="button"
                className={button}
                onClick={() =>
                  change(
                    'retencoes',
                    data.retencoes.filter((_, i) => i !== index),
                  )
                }
              >
                Remover retenção
              </button>
            </div>
          ))}
          <button
            className={button}
            type="button"
            onClick={() =>
              change('retencoes', [
                ...data.retencoes,
                { componente: 'PB', fator: '', hipotese: '' },
              ])
            }
          >
            Adicionar retenção
          </button>
        </Section>
      )}
    </>
  )
}
export function RecipeRead({ data }: { data: RecipeData }) {
  return (
    <div className="space-y-4">
      <p>
        {data.tipo} · {data.modo} · Referência {data.tamanhoReferencia}{' '}
        {data.unidadeReferencia}
      </p>
      <p>{data.observacao}</p>
      <h3 className="font-semibold">Entradas</h3>
      <ul className="space-y-3">
        {data.entradas.map((i) => (
          <li className="rounded border p-3" key={i.id}>
            <Link className="underline" to={`/itens/${i.itemId}`}>
              Item {i.itemId}
            </Link>
            <p>
              {i.quantidade}{' '}
              {data.modo === 'Percentual' &&
              ['Alimentar', 'OutroMaterialIncorporado'].includes(i.papel)
                ? '% BN'
                : i.unidade}{' '}
              · {i.papel} · {i.escala}
            </p>
            {i.perfilId && (
              <Link
                className="mr-3 underline"
                to={`/producao/perfis/${i.perfilId}`}
              >
                Perfil selecionado
              </Link>
            )}
            {i.subReceitaVersaoId && (
              <Link
                className="underline"
                to={`/producao/receitas/versoes/${i.subReceitaVersaoId}`}
              >
                Sub-receita / saída {i.subSaidaId}
              </Link>
            )}
            <p>
              Inclusão: {i.inclusaoMinima ?? 'sem mínimo'} –{' '}
              {i.inclusaoMaxima ?? 'sem máximo'} %
            </p>
            {i.conversaoId && <p>Conversão: {i.conversaoId}</p>}
          </li>
        ))}
      </ul>
      <h3 className="font-semibold">Saídas</h3>
      <ul className="space-y-3">
        {data.saidas.map((o) => (
          <li className="rounded border p-3" key={o.id}>
            <Link className="underline" to={`/itens/${o.itemId}`}>
              {o.nome}
            </Link>
            <p>
              {o.quantidade} {o.unidade} ·{' '}
              {o.principal ? 'Principal' : 'Coproduto'} · {o.escala}
            </p>
            {o.perfilId && (
              <Link className="underline" to={`/producao/perfis/${o.perfilId}`}>
                Perfil da saída
              </Link>
            )}
            {(o.massaLiquidaKg || o.massaDrenadaKg || o.massaBrutaKg) && (
              <p>
                Esperado líquido/drenado/bruto: {o.massaLiquidaKg ?? '?'} /{' '}
                {o.massaDrenadaKg ?? '?'} / {o.massaBrutaKg ?? '?'} kg ·{' '}
                {o.metodoMedicao}
              </p>
            )}
          </li>
        ))}
      </ul>
      <h3 className="font-semibold">Perdas</h3>
      <ul>
        {data.perdas.map((p, i) => (
          <li key={i}>
            {p.quantidade} {p.unidade} · {p.motivo} · {p.escala}
          </li>
        ))}
      </ul>
      <h3 className="font-semibold">Etapas</h3>
      <ol className="list-inside list-decimal">
        {data.etapas.map((e) => (
          <li key={e.ordem}>{e.descricao}</li>
        ))}
      </ol>
      {data.retencoes.length > 0 && (
        <>
          <h3 className="font-semibold">Retenções e hipóteses</h3>
          <ul>
            {data.retencoes.map((r) => (
              <li key={r.componente}>
                {r.componente}: {r.fator} · {r.hipotese}
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
