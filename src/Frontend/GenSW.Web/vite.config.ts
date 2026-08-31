import { existsSync, readFileSync } from 'node:fs'
import { homedir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'

const certificateFileName = 'GenSW.Web.pem'
const keyFileName = 'GenSW.Web.key'
const projectRoot = fileURLToPath(new URL('.', import.meta.url))

const resolveLocalHttps = (env: Record<string, string>) => {
  const configuredCertificate = env.GENSW_HTTPS_CERT_PATH?.trim()
  const configuredKey = env.GENSW_HTTPS_KEY_PATH?.trim()

  if (Boolean(configuredCertificate) !== Boolean(configuredKey)) {
    throw new Error(
      'Defina GENSW_HTTPS_CERT_PATH e GENSW_HTTPS_KEY_PATH em conjunto no .env.local.',
    )
  }

  const configuredPair =
    configuredCertificate && configuredKey
      ? [
          path.resolve(projectRoot, configuredCertificate),
          path.resolve(projectRoot, configuredKey),
        ]
      : undefined

  const certificateDirectories = [
    env.APPDATA && path.join(env.APPDATA, 'ASP.NET', 'https'),
    env.USERPROFILE && path.join(env.USERPROFILE, '.aspnet', 'https'),
    path.join(homedir(), '.aspnet', 'https'),
  ].filter((directory): directory is string => Boolean(directory))

  const pairs = configuredPair
    ? [configuredPair]
    : [...new Set(certificateDirectories)].map((directory) => [
        path.join(directory, certificateFileName),
        path.join(directory, keyFileName),
      ])

  const pair = pairs.find(([certificatePath, keyPath]) =>
    [certificatePath, keyPath].every(existsSync),
  )

  if (!pair) {
    throw new Error(
      [
        'Certificado HTTPS local do GenSW não encontrado.',
        `Exporte o certificado .NET como ${certificateFileName}/${keyFileName} fora do repositório`,
        'ou defina GENSW_HTTPS_CERT_PATH e GENSW_HTTPS_KEY_PATH no .env.local.',
        'Consulte o README do frontend para o comando seguro e os pre-requisitos.',
      ].join(' '),
    )
  }

  const [certificatePath, keyPath] = pair

  return {
    cert: readFileSync(certificatePath),
    key: readFileSync(keyPath),
  }
}

export default defineConfig(({ command, isPreview, mode }) => {
  const env = loadEnv(mode, projectRoot, [
    'VITE_',
    'GENSW_',
    'APPDATA',
    'USERPROFILE',
    'VITEST',
  ])
  const isDevelopmentServer =
    command === 'serve' && !isPreview && mode !== 'test' && env.VITEST !== 'true'

  return {
    plugins: [react()],
    server: {
      host: 'localhost',
      port: 7441,
      strictPort: true,
      https: isDevelopmentServer ? resolveLocalHttps(env) : undefined,
    },
    test: {
      clearMocks: true,
      environment: 'jsdom',
      globals: true,
      restoreMocks: true,
      setupFiles: './src/test/setup.ts',
    },
  }
})
