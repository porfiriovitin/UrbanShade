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
- A importa??o complementar do OpenStreetMap n?o est? habilitada nesta entrega. A origem est? prevista no modelo, e a an?lise por sat?lite funciona independentemente dela.
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
