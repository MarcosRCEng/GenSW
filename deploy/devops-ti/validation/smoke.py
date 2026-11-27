#!/usr/bin/env python3
"""Create and verify a fixture through the private GenSW proxy in the #576 trial."""

import argparse
from decimal import Decimal
import getpass
import hashlib
from http.client import HTTPConnection, HTTPException
import json
import os
from pathlib import Path
import struct
import sys
from urllib.parse import urlsplit
from uuid import UUID, uuid4
import zlib


API = '/api/v1'
OPTIMIZATIONS = f'{API}/otimizacoes-formulacao'
PNG_SIGNATURE = b'\x89PNG\r\n\x1a\n'


def require(condition, message):
    if not condition:
        raise ValueError(message)


class Api:
    def __init__(self, port):
        self.port = port
        self.token = None

    def request(self, method, path, status, *, body=None, headers=None, anonymous=False):
        headers = dict(headers or {})
        if self.token and not anonymous:
            headers['Authorization'] = f'Bearer {self.token}'
        connection = HTTPConnection('127.0.0.1', self.port, timeout=35)
        try:
            connection.request(method, path, body=body, headers=headers)
            with connection.getresponse() as response:
                require(response.status == status,
                        f'{method} {path}: HTTP {response.status}, esperado {status}.')
                return response.headers, response.read()
        finally:
            connection.close()

    def json(self, method, path, status=200, body=None, *, headers=None):
        headers = dict(headers or {})
        if body is not None:
            body = json.dumps(body).encode('utf-8')
            headers['Content-Type'] = 'application/json'
        response_headers, data = self.request(method, path, status, body=body, headers=headers)
        require(response_headers.get_content_type() == 'application/json',
                f'{method} {path}: tipo de resposta JSON esperado.')
        return json.loads(data)


def synthetic_png():
    def chunk(kind, data=b''):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data))

    # A 512 x 320 two-color image forces a distinct 256 x 160 thumbnail.
    row = b'\0' + b'\x15\x92\x54' * 256 + b'\xd2\x8e\x29' * 256
    return (PNG_SIGNATURE + chunk(b'IHDR', struct.pack('!IIBBBBB', 512, 320, 8, 2, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(row * 320)) + chunk(b'IEND'))


def create_fixture(api):
    suffix = uuid4().hex[:12]
    code = f'ENSAIO-576-{suffix.upper()}'
    name = f'Animal sintético {code}'
    caption = f'Imagem sintética {code}'
    species = api.json('POST', f'{API}/especies', 201, {'nomeComum': f'Espécie sintética {code}'})
    animal = api.json('POST', f'{API}/animais', 201, {
        'codigoInterno': code, 'nome': name, 'especieId': species['id'], 'sexo': 1, 'escopo': 1,
    })
    animal_id = str(UUID(animal['id']))
    boundary = uuid4().hex
    data = (f'--{boundary}\r\nContent-Disposition: form-data; name="legenda"\r\n\r\n{caption}\r\n'
            f'--{boundary}\r\nContent-Disposition: form-data; name="arquivo"; filename="synthetic.png"\r\n'
            'Content-Type: image/png\r\n\r\n').encode('utf-8')
    data += synthetic_png() + f'\r\n--{boundary}--\r\n'.encode('ascii')
    _, response = api.request('POST', f'{API}/animais/{animal_id}/imagens', 201, body=data,
                              headers={'Content-Type': f'multipart/form-data; boundary={boundary}'})
    image = json.loads(response)
    return {'animalId': animal_id, 'code': code, 'name': name, 'caption': caption,
            'imageId': str(UUID(image['id']))}


def verify_fixture(api, receipt):
    animal_id = str(UUID(receipt['animalId']))
    image_id = str(UUID(receipt['imageId']))
    animal_path = f'{API}/animais/{animal_id}'
    animal = api.json('GET', animal_path)
    require(animal['codigoInterno'] == receipt['code'] and animal['nome'] == receipt['name'],
            'O cadastro retornado difere do cadastro gravado.')

    content_path = f'{animal_path}/imagens/{image_id}/conteudo'
    versions = {'visualizacao': (512, 320), 'miniatura': (256, 160)}
    gallery = api.json('GET', f'{animal_path}/imagens')
    image = next((item for item in gallery['items'] if item['id'] == image_id), None)
    require(image and image['legenda'] == receipt['caption'], 'Imagem sintética ausente da galeria.')
    require(image['conteudoPath'] == f'{content_path}?versao=visualizacao'
            and image['thumbnailPath'] == f'{content_path}?versao=miniatura',
            'URLs da galeria não correspondem às duas versões da imagem.')

    hashes = {}
    for version, dimensions in versions.items():
        path = f'{content_path}?versao={version}'
        api.request('GET', path, 401, anonymous=True)
        headers, data = api.request('GET', path, 200)
        require(headers.get_content_type() == 'image/png' and len(data) >= 24 and data.startswith(PNG_SIGNATURE)
                and struct.unpack_from('!II', data, 16) == dimensions, f'PNG inválido ({version}).')
        hashes[version] = hashlib.sha256(data).hexdigest()
        print(f'{version} HTTP 200 image/png {dimensions[0]}x{dimensions[1]}, sem sessão HTTP 401')
    if 'imageSha256' in receipt:
        require(hashes == receipt['imageSha256'], 'As versões da imagem mudaram após a gravação.')
    return hashes


def solve_fixture(api):
    candidates = []
    suffix = uuid4().hex[:12]
    for letter, protein, mass in (('A', '100', '60'), ('B', '400', '40')):
        item = api.json('POST', f'{API}/itens', 201, {
            'codigo': f'ENSAIO-576-{suffix}-{letter}', 'nome': f'Ingrediente sintético {letter}',
            'classe': 'Alimentar', 'unidade': 'kg', 'podeEntrar': True, 'usoInterno': True,
        })
        profile = api.json('POST', f'{API}/itens/{item["id"]}/perfis-nutricionais', 201, {
            'nome': 'Perfil sintético #576', 'fonte': 'Ensaio de implantação #576',
            'metodo': 'Sintetico', 'preparacao': 'Mistura', 'referenciaAmostra': 'Amostra sintética',
            'contexto': 'Verificação de empacotamento', 'valores': [{
                'componente': 'PB', 'estado': 'Conhecido', 'valor': protein, 'origem': 'Declarado',
                'base': 'BN', 'unidade': 'g/kg', 'metodo': 'Sintetico', 'contexto': '',
            }],
        })
        published = api.json('POST', f'{API}/perfis-nutricionais/{profile["id"]}/publicacao', 200,
                             {'versaoEsperada': profile['revisao']})
        candidates.append({'id': str(uuid4()), 'itemId': item['id'], 'itemRevisao': item['revisao'],
                           'perfilId': published['id'], 'perfilRevisao': published['revisao'], 'referenciaKg': mass})

    result = api.json('POST', OPTIMIZATIONS, 201, {
        'versaoModelo': 'f05-n1/1', 'modo': 'N1', 'disponibilidade': False, 'massaKg': '100',
        'referenciaDescricao': 'Mistura sintética #576', 'preparo': 'Mistura', 'candidatos': candidates,
        'metas': [{'id': str(uuid4()), 'componente': 'PB', 'metodo': 'Sintetico', 'contexto': '',
                   'base': 'BN', 'unidade': 'g/kg', 'minimo': '250', 'origem': 'InformadaUsuario',
                   'justificativa': 'Teste do HiGHS no artefato linux-x64'}],
    }, headers={'Idempotency-Key': uuid4().hex})
    require(result['estado'] == 'Finalizada' and result['resultado']['desfecho'] == 'SolucaoViavel',
            f'Cálculo não comprovou solução viável: {result["resultado"]["codigo"]}.')
    motor = result['resultado']['motor']
    masses = motor['candidate']['quantizedMasses']
    require(all(Decimal(masses[candidate['id']]) == 50 for candidate in candidates),
            'O HiGHS retornou massas diferentes de 50 kg para cada ingrediente.')
    metadata = motor['native']['metadata']
    require(metadata['version'] == '1.15.1' and metadata['runtime'] == 'linux-x64',
            'Versão ou arquitetura do HiGHS diferentes do pacote aprovado.')
    print(f'HiGHS {metadata["version"]} {metadata["runtime"]}, solução 50 kg + 50 kg')
    return {'id': result['id'], 'resultSha256': result['hashResultado'], 'solver': metadata['version'],
            'runtime': metadata['runtime'], 'binarySha256': metadata['binarySha256']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('create', 'verify'))
    parser.add_argument('--port', type=int, default=3086, help='porta HTTP local ou encaminhada por SSH')
    parser.add_argument('--username', required=True, help='usuário dedicado ao ensaio sintético')
    parser.add_argument('--receipt', type=Path, required=True, help='recibo de dados sintéticos, sem credenciais')
    parser.add_argument('--synthetic-db-confirmed', action='store_true',
                        help='confirmar identidade da base de ensaio antes de gravar')
    parser.add_argument('--solver', action='store_true', help='executar também um cálculo nativo ao criar')
    args = parser.parse_args()
    if args.action == 'create' and (not args.synthetic_db_confirmed or args.receipt.exists()):
        parser.error('create exige --synthetic-db-confirmed e um novo caminho --receipt.')
    if args.action == 'verify' and args.solver:
        parser.error('--solver só se aplica a create. verify confere o cálculo já gravado.')

    api = Api(args.port)
    api.request('GET', f'{API}/ready', 200)
    api.request('GET', f'{API}/auth/me', 401)
    password = os.environ.get('GENSW_VALIDATION_PASSWORD') or getpass.getpass('Senha sintética: ')
    login = api.json('POST', f'{API}/auth/login', 200, {'userName': args.username, 'password': password})
    api.token = login['accessToken']
    api.json('GET', f'{API}/auth/me')
    print('Prontidão HTTP 200, login HTTP 200 e sessão autenticada HTTP 200')

    if args.action == 'create':
        receipt = create_fixture(api)
        receipt['imageSha256'] = verify_fixture(api, receipt)
        if args.solver:
            receipt['optimization'] = solve_fixture(api)
        with args.receipt.open('x', encoding='utf-8') as target:
            json.dump(receipt, target, ensure_ascii=False, indent=2)
            target.write('\n')
    else:
        receipt = json.loads(args.receipt.read_text(encoding='utf-8'))
        verify_fixture(api, receipt)
        if 'optimization' in receipt:
            old = receipt['optimization']
            result = api.json('GET', f'{OPTIMIZATIONS}/{UUID(old["id"])}')
            require(result['estado'] == 'Finalizada' and result['hashResultado'] == old['resultSha256'],
                    'O resultado persistido da otimização mudou.')
            print(f'Otimização {old["id"]} preservada')

    print(f'Cadastro /animais/{receipt["animalId"]}, imagem {receipt["imageId"]}')


if __name__ == '__main__':
    try:
        main()
    except (HTTPException, KeyError, OSError, ValueError) as error:
        sys.exit(str(error))
