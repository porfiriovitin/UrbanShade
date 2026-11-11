# Documentação da equipe — UrbanShade

Esta pasta reúne os fluxos implementados no backend para apoiar a integração e os testes da equipe.

## Guias

- [Instituições, responsáveis, sensores e primeiro acesso](./fluxo-instituicoes-primeiro-acesso.md): cadastro administrativo, vínculo dos sensores, senha temporária, troca obrigatória de senha, JWT e permissões.

## Organização

```text
src/
├── Backend/
├── Docs/
│   ├── README.md
│   └── fluxo-instituicoes-primeiro-acesso.md
└── Shared/
```

Os exemplos usam dados fictícios. Substitua os IDs e tokens pelos valores retornados pela API do seu ambiente.


## Integra??o com sat?lites ? SPEC 04

O m?dulo `/api/geo` consulta o Google Earth Engine e mant?m os resultados no PostgreSQL/PostGIS. As consultas HTTP da aplica??o usam os dados persistidos. O processamento acontece em um worker separado da simula??o de sensores, dentro do mesmo backend.

### Configura??o local

- O Docker Compose usa `postgis/postgis:16-3.7` e preserva o volume `postgres_data`.
- A migration `AddGeospatialAnalyses` habilita a extens?o e cria regi?es, jobs, an?lises e ?reas verdes, com ?ndices GiST. O usu?rio do banco precisa de permiss?o para criar a extens?o quando ela ainda n?o existir.
- As configura??es n?o sens?veis ficam em `appsettings.json`, se??o `GoogleEarthEngine`.
- No desenvolvimento, `appsettings.Development.json` cont?m `GoogleEarthEngine:Enabled`, `ProjectId` e o objeto `Credentials` completo da Service Account. Esse arquivo ? ignorado pelo Git e exclu?do da publica??o. N?o copie seu conte?do para exemplos, logs ou respostas.
- Quando `Credentials` n?o estiver configurado, a integra??o usa Application Default Credentials. Isso permite credenciais gerenciadas na hospedagem ou `GOOGLE_APPLICATION_CREDENTIALS` apontando para um arquivo local.
- O projeto Google deve estar registrado no Earth Engine, com a API habilitada e permiss?es de computa??o para a Service Account.
- Fora do Development, `Enabled` permanece `false` por padr?o. Os dados armazenados podem ser consultados mesmo assim; novas solicita??es de processamento retornam `503`.

### Fluxo pela API

Use o token de `PlatformAdministrator` para cadastrar uma regi?o vinculada a uma cidade existente:

```http
POST /api/geo/regions
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "cityId": "<id da cidade>",
  "name": "?rea de estudo",
  "description": "Pol?gono de teste no Parque Ibirapuera",
  "boundary": {
    "type": "Polygon",
    "coordinates": [[[-46.657, -23.589], [-46.654, -23.589], [-46.654, -23.586], [-46.657, -23.586], [-46.657, -23.589]]]
  }
}
```

As coordenadas seguem longitude, latitude, em SRID 4326. S?o aceitos `Polygon` e `MultiPolygon`, com valida??o de topologia, ?rea e complexidade. O limite inicial ? de 100 km? e 2.000 v?rtices.

Solicite o processamento:

```http
POST /api/geo/regions/<regionId>/refresh
Content-Type: application/json
```

```json
{
  "startAt": "2025-08-01T00:00:00Z",
  "endAt": "2025-10-01T00:00:00Z"
}
```

O fim do per?odo ? exclusivo. Datas devem estar em UTC. Com `{}`, a janela padr?o cobre 60 dias e termina na meia-noite UTC do dia atual. O per?odo m?ximo inicial ? de 366 dias.

A resposta `202 Accepted` cont?m `data.id` e o cabe?alho `Location` para `GET /api/geo/jobs/<jobId>`. O worker consulta a fila a cada cinco segundos. Os estados s?o `PENDING`, `PROCESSING`, `COMPLETED` e `FAILED`; `resultSummary` informa a disponibilidade de cada fonte. Aus?ncia de imagens ? um processamento conclu?do com dados indispon?veis, n?o uma temperatura de zero.

Usu?rios autenticados podem consultar:

| Endpoint | Resultado |
|---|---|
| `GET /api/geo/regions?cityId=<id>&page=1&pageSize=20` | Regi?es ativas |
| `GET /api/geo/regions/<id>` | Regi?o e pol?gono |
| `GET /api/geo/regions/<id>/vegetation` | NDVI, cobertura e ?rea vegetal |
| `GET /api/geo/regions/<id>/surface-temperature` | Temperatura superficial em Celsius |
| `GET /api/geo/regions/<id>/analyses?startAt=<UTC>&endAt=<UTC>&page=1&pageSize=20` | Hist?rico das observa??es |
| `GET /api/geo/green-areas?bbox=-46.66,-23.60,-46.65,-23.58&page=1&pageSize=20` | `FeatureCollection` GeoJSON |

As respostas comuns seguem `PayloadResponse<T>`. ?reas verdes retornam GeoJSON diretamente, com `Content-Type: application/geo+json`. O tamanho m?ximo da p?gina ? 100; o bbox admite at? um grau por eixo.

`PATCH /regions/<id>` atualiza nome, descri??o e/ou pol?gono. Campos omitidos s?o preservados; envie descri??o vazia para limpar o texto. `DELETE /regions/<id>` desativa logicamente a regi?o. Essas opera??es e `refresh` exigem administra??o e compartilham o limite de dez solicita??es por minuto por usu?rio. A consulta de jobs tamb?m ? administrativa.

### Regras dos resultados

- O m?dulo examina at? vinte cenas recentes por fonte no per?odo solicitado, em ordem decrescente de data. Avalia cada cena no Earth Engine e escolhe a primeira com pelo menos 70% de cobertura v?lida da regi?o. Uma imagem mais antiga que as vinte candidatas pode exigir outra janela de consulta.
- Sentinel-2 usa B8/B4 para NDVI, SCL para remover pixels inv?lidos, sombras, nuvens e neve, e o dataset de probabilidade de nuvens com limiar inicial de 40.
- Cobertura vegetal ? a ?rea v?lida com NDVI maior ou igual a 0,4 dividida pela ?rea v?lida observada. ?reas sem observa??o n?o s?o classificadas como aus?ncia de vegeta??o.
- As ?reas s?o somas de `Image.pixelArea()` no Earth Engine, em m?, com escala de an?lise de 10 m para vegeta??o e 30 m para Landsat. O grid de processamento usa WGS84; a ?rea n?o ? calculada em graus quadrados. No banco, a valida??o usa `ST_Area(...::geography)`.
- Landsat 8/9 usa produtos `L2SP`, as m?scaras `QA_PIXEL` e `QA_RADSAT` e a f?rmula `ST_B10 * 0.00341802 + 149 - 273.15`. Temperatura da superf?cie permanece separada da temperatura do ar e do ?ndice de calor IoT.
- Cada resultado registra dataset, imagem, data de observa??o, qualidade, par?metros e vers?o do algoritmo. `dataStatus` informa `AVAILABLE`, `OUTDATED` ou `UNAVAILABLE`; o limite inicial de desatualiza??o ? de 60 dias desde a observa??o.
- A identidade do job inclui regi?o, vers?o do pol?gono, per?odo, algoritmo e par?metros. Solicita??es iguais reutilizam o mesmo job. Jobs falhos podem ser solicitados novamente; fontes j? persistidas n?o s?o duplicadas.
- A fila usa aquisi??o at?mica e uma posse de quinze minutos. Jobs interrompidos ficam dispon?veis novamente ap?s esse prazo. O processamento tem timeout de doze minutos e s? persiste quando ainda det?m a posse.
- Falha de uma fonte preserva resultados v?lidos da outra e os dados anteriores. Alterar o pol?gono exige nova an?lise para os indicadores atuais, mantendo o hist?rico anterior.
- Pol?gonos de vegeta??o t?m ?rea m?nima inicial de 500 m?, limite de 200 por an?lise e complexidade limitada. Representam estimativas de vegeta??o, n?o parques p?blicos confirmados. A lista pode ser limitada; os metadados registram a situa??o da vetoriza??o.
- A atualiza??o autom?tica fica desabilitada inicialmente. Ao ativar `AutomaticRefreshEnabled`, o worker agenda regi?es conforme `RefreshIntervalDays`, inicialmente sete dias. Isso n?o garante novas imagens v?lidas.

### Verifica??o

Os testes de API usam um cont?iner PostGIS isolado. Os testes de c?lculo interpretam as express?es produzidas pela integra??o para verificar NDVI, temperatura e m?scaras sem depender do Google.

```powershell
dotnet test UrbanShade.slnx
```

O teste real ? opcional e exige as credenciais locais de desenvolvimento e acesso ? rede. Ele verifica o cat?logo oficial de algoritmos, vegeta??o, vetoriza??o, temperatura e aus?ncia de imagens para uma regi?o pequena:

```powershell
$env:URBANSHADE_EARTH_ENGINE_TEST = 'true'
dotnet test tests/UseCases.Tests --filter FullyQualifiedName~EarthEngineLiveTests
Remove-Item Env:URBANSHADE_EARTH_ENGINE_TEST
```

## Áreas verdes do OpenStreetMap

O módulo importa polígonos mapeados no OpenStreetMap pela Overpass API. Ele complementa as estimativas do Sentinel-2 com nomes, limites e tags dos lugares, sem alterar os indicadores de NDVI ou temperatura.

### Configuração e atualização

As opções ficam na seção `OpenStreetMap` de `appsettings.json`. No desenvolvimento, `appsettings.Development.json` habilita a integração; não é necessária nenhuma chave. Nos demais ambientes, o padrão é `Enabled: false`.

O endpoint administrativo abaixo cria um job persistido e retorna `202 Accepted`, o identificador e o cabeçalho `Location`. Ele aceita corpo vazio, exige a política `PlatformAdministration` e compartilha o rate limiting administrativo existente:

```http
POST /api/geo/regions/<regionId>/refresh-green-areas
Authorization: Bearer <token>
```

Consulte o andamento em `GET /api/geo/jobs/<jobId>`. O campo `jobType` distingue `OPENSTREETMAP` de `SATELLITE`. Solicitações para a mesma região, versão do polígono, parâmetros e dia UTC reutilizam o job. Jobs falhos podem ser solicitados novamente.

O worker do OpenStreetMap funciona independentemente das credenciais e da configuração do Earth Engine. Ele verifica a fila a cada dez segundos e recupera jobs interrompidos após o vencimento da posse. Falhas externas mantêm os dados anteriormente importados. Uma resposta válida e vazia remove apenas os vínculos daquela região.

Para atualização periódica, configure `OpenStreetMap:AutomaticRefreshEnabled: true`. O intervalo inicial é de sete dias. Essa opção fica desativada por padrão.

### Dados importados

São consultados ways fechados e relações multipolygon com as seguintes tags:

- `leisure=park|garden`;
- `landuse=forest|grass|village_green|recreation_ground`;
- `natural=wood|grassland|scrub`.

Praças ou outros espaços sem essas tags não são classificados automaticamente como áreas verdes. Pontos, vias abertas e elementos com `area=no` não geram polígonos. Relações são montadas a partir dos membros, respeitando contornos externos, fragmentos invertidos e contornos internos. Os membros de uma relação importada não são duplicados como áreas independentes nessa resposta.

A consulta usa o bounding box e procura áreas que contêm seu centro e seus cantos. O PostGIS aplica a interseção com o polígono real da região antes de criar os vínculos. Esse procedimento não garante um inventário completo: dependemos do mapeamento do OSM e dos mecanismos de seleção e atualização do Overpass.

Cada objeto é identificado pelo par tipo/ID do OSM. Regiões sobrepostas compartilham o mesmo registro. Uma alteração do limite da região exige nova importação para seus vínculos atuais. Importações antigas não podem substituir solicitações ou snapshots mais recentes já persistidos.

O limite inicial é de 2.000 elementos, 20.000 vértices por objeto e 16 MiB por resposta. Geometrias válidas podem ser simplificadas para até 2.000 vértices na distribuição pelo mapa. Respostas incompletas, excessivas ou geometrias inválidas causam falha do job e preservam a importação anterior. As áreas em m² são calculadas pelo PostGIS com `ST_Area(...::geography)` sobre a geometria armazenada.

### Consulta pelo aplicativo

Use o endpoint existente:

```http
GET /api/geo/green-areas?bbox=-46.66,-23.60,-46.65,-23.58&page=1&pageSize=20
Authorization: Bearer <token>
```

Ele continua consultando apenas o banco local e retorna um `FeatureCollection`. Para elementos OSM, as propriedades incluem:

- `source: OPENSTREETMAP`, nome e área estimada;
- `referenceDate`: data-base do snapshot informado pelo Overpass;
- `importedAt`: data da importação no backend;
- `osmElementType`, `osmElementId` e `osmUrl`;
- `sourceTags`: tags originais, incluindo informações de acesso quando disponíveis.

Como um objeto OSM pode pertencer a várias regiões, seu `regionId` singular permanece nulo. A presença no mapa não comprova acesso público, sombra ou conforto térmico. Preserve e interprete as tags de acesso; não apresente espaços privados como parques públicos.

Os metadados `sources.openStreetMap` fornecem a atribuição **© OpenStreetMap contributors**, o link de copyright e a licença ODbL. O aplicativo deve exibir a atribuição ao utilizar essa camada. As informações do OSM permanecem identificadas separadamente dos resultados de satélite. Consulte as [orientações oficiais de atribuição](https://www.openstreetmap.org/copyright).

### Testes

Os testes automatizados cobrem parsing de ways e multipolygons, contornos internos, limites, falhas HTTP, autorização, idempotência, fila, persistência espacial e regiões sobrepostas. Os testes comuns usam respostas simuladas e PostGIS isolado.

O teste real consulta uma região pequena no Overpass público e fica desabilitado na suíte comum:

```powershell
$env:URBANSHADE_OSM_TEST = 'true'
dotnet test tests/UseCases.Tests --filter FullyQualifiedName~OpenStreetMapLiveTests
Remove-Item Env:URBANSHADE_OSM_TEST
```

As instâncias públicas têm recursos compartilhados e podem retornar indisponibilidade ou limites de uso. O backend usa timeout e até duas repetições para falhas transitórias. Para volume elevado, será necessário avaliar uma instância própria ou um provedor adequado, conforme as [orientações do Overpass](https://dev.overpass-api.de/overpass-doc/en/preface/commons.html).
