# Áreas verdes do OpenStreetMap

O módulo importa polígonos mapeados no OpenStreetMap pela Overpass API. Ele complementa as estimativas do Sentinel-2 com nomes, limites e tags dos lugares, sem alterar os indicadores de NDVI ou temperatura.

## Configuração e atualização

As opções ficam na seção `OpenStreetMap` de `appsettings.json`. No desenvolvimento, `appsettings.Development.json` habilita a integração; não é necessária nenhuma chave. Nos demais ambientes, o padrão é `Enabled: false`.

O endpoint administrativo abaixo cria um job persistido e retorna `202 Accepted`, o identificador e o cabeçalho `Location`. Ele aceita corpo vazio, exige a política `PlatformAdministration` e compartilha o rate limiting administrativo existente:

```http
POST /api/geo/regions/<regionId>/refresh-green-areas
Authorization: Bearer <token>
```

Consulte o andamento em `GET /api/geo/jobs/<jobId>`. O campo `jobType` distingue `OPENSTREETMAP` de `SATELLITE`. Solicitações para a mesma região, versão do polígono, parâmetros e dia UTC reutilizam o job. Jobs falhos podem ser solicitados novamente.

O worker do OpenStreetMap funciona independentemente das credenciais e da configuração do Earth Engine. Ele verifica a fila a cada dez segundos e recupera jobs interrompidos após o vencimento da posse. Falhas externas mantêm os dados anteriormente importados. Uma resposta válida e vazia remove apenas os vínculos daquela região.

Para atualização periódica, configure `OpenStreetMap:AutomaticRefreshEnabled: true`. O intervalo inicial é de sete dias. Essa opção fica desativada por padrão.

## Dados importados

São consultados ways fechados e relações multipolygon com as seguintes tags:

- `leisure=park|garden`;
- `landuse=forest|grass|village_green|recreation_ground`;
- `natural=wood|grassland|scrub`.

Praças ou outros espaços sem essas tags não são classificados automaticamente como áreas verdes. Pontos, vias abertas e elementos com `area=no` não geram polígonos. Relações são montadas a partir dos membros, respeitando contornos externos, fragmentos invertidos e contornos internos. Os membros de uma relação importada não são duplicados como áreas independentes nessa resposta.

A consulta usa o bounding box e procura áreas que contêm seu centro e seus cantos. O PostGIS aplica a interseção com o polígono real da região antes de criar os vínculos. Esse procedimento não garante um inventário completo: dependemos do mapeamento do OSM e dos mecanismos de seleção e atualização do Overpass.

Cada objeto é identificado pelo par tipo/ID do OSM. Regiões sobrepostas compartilham o mesmo registro. Uma alteração do limite da região exige nova importação para seus vínculos atuais. Importações antigas não podem substituir solicitações ou snapshots mais recentes já persistidos.

O limite inicial é de 2.000 elementos, 20.000 vértices por objeto e 16 MiB por resposta. Geometrias válidas podem ser simplificadas para até 2.000 vértices na distribuição pelo mapa. Respostas incompletas, excessivas ou geometrias inválidas causam falha do job e preservam a importação anterior. As áreas em m² são calculadas pelo PostGIS com `ST_Area(...::geography)` sobre a geometria armazenada.

## Consulta pelo aplicativo

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

## Testes

Os testes automatizados cobrem parsing de ways e multipolygons, contornos internos, limites, falhas HTTP, autorização, idempotência, fila, persistência espacial e regiões sobrepostas. Os testes comuns usam respostas simuladas e PostGIS isolado.

O teste real consulta uma região pequena no Overpass público e fica desabilitado na suíte comum:

```powershell
$env:URBANSHADE_OSM_TEST = 'true'
dotnet test tests/UseCases.Tests --filter FullyQualifiedName~OpenStreetMapLiveTests
Remove-Item Env:URBANSHADE_OSM_TEST
```

As instâncias públicas têm recursos compartilhados e podem retornar indisponibilidade ou limites de uso. O backend usa timeout e até duas repetições para falhas transitórias. Para volume elevado, será necessário avaliar uma instância própria ou um provedor adequado, conforme as [orientações do Overpass](https://dev.overpass-api.de/overpass-doc/en/preface/commons.html).
## Guias relacionados

- [Regiões e consultas geoespaciais](./regioes-e-consultas-geoespaciais.md): cadastro da região usada na importação e referência das consultas.
- [Integração com satélites](./integracao-satelites.md): estimativas de vegetação e temperatura obtidas pelo Earth Engine.
