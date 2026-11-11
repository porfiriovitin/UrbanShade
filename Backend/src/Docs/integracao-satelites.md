# Integração com satélites

O módulo `/api/geo` consulta o Google Earth Engine e mantém os resultados no PostgreSQL/PostGIS. As consultas HTTP da aplicação usam os dados persistidos. O processamento acontece em um worker separado da simulação de sensores, dentro do mesmo backend.

Os exemplos usam dados fictícios. Substitua os IDs e tokens pelos valores retornados pela API do seu ambiente.

## Configuração local

- O Docker Compose usa `postgis/postgis:16-3.7` e preserva o volume `postgres_data`.
- A migration `AddGeospatialAnalyses` habilita a extensão e cria regiões, jobs, análises e áreas verdes, com índices GiST. O usuário do banco precisa de permissão para criar a extensão quando ela ainda não existir.
- As configurações não sensíveis ficam em `appsettings.json`, seção `GoogleEarthEngine`.
- No desenvolvimento, `appsettings.Development.json` contém `GoogleEarthEngine:Enabled`, `ProjectId` e o objeto `Credentials` completo da Service Account. Esse arquivo é ignorado pelo Git e excluído da publicação. Não copie seu conteúdo para exemplos, logs ou respostas.
- Quando `Credentials` não estiver configurado, a integração usa Application Default Credentials. Isso permite credenciais gerenciadas na hospedagem ou `GOOGLE_APPLICATION_CREDENTIALS` apontando para um arquivo local.
- O projeto Google deve estar registrado no Earth Engine, com a API habilitada e permissões de computação para a Service Account.
- Fora do Development, `Enabled` permanece `false` por padrão. Os dados armazenados podem ser consultados mesmo assim; novas solicitações de processamento retornam `503`.

## Solicitação de processamento

Cadastre uma região conforme o guia de [regiões e consultas geoespaciais](./regioes-e-consultas-geoespaciais.md). Use o token administrativo nas solicitações de processamento e na consulta de jobs.

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

O fim do período é exclusivo. Datas devem estar em UTC. Com `{}`, a janela padrão cobre 60 dias e termina na meia-noite UTC do dia atual. O período máximo inicial é de 366 dias.

A resposta `202 Accepted` contém `data.id` e o cabeçalho `Location` para `GET /api/geo/jobs/<jobId>`. O worker consulta a fila a cada cinco segundos. Os estados são `PENDING`, `PROCESSING`, `COMPLETED` e `FAILED`; `resultSummary` informa a disponibilidade de cada fonte. Ausência de imagens é um processamento concluído com dados indisponíveis, não uma temperatura de zero.

## Regras dos resultados

- O módulo examina até vinte cenas recentes por fonte no período solicitado, em ordem decrescente de data. Avalia cada cena no Earth Engine e escolhe a primeira com pelo menos 70% de cobertura válida da região. Uma imagem mais antiga que as vinte candidatas pode exigir outra janela de consulta.
- Sentinel-2 usa B8/B4 para NDVI, SCL para remover pixels inválidos, sombras, nuvens e neve, e o dataset de probabilidade de nuvens com limiar inicial de 40.
- Cobertura vegetal é a área válida com NDVI maior ou igual a 0,4 dividida pela área válida observada. Áreas sem observação não são classificadas como ausência de vegetação.
- As áreas são somas de `Image.pixelArea()` no Earth Engine, em m², com escala de análise de 10 m para vegetação e 30 m para Landsat. O grid de processamento usa WGS84; a área não é calculada em graus quadrados. No banco, a validação usa `ST_Area(...::geography)`.
- Landsat 8/9 usa produtos `L2SP`, as máscaras `QA_PIXEL` e `QA_RADSAT` e a fórmula `ST_B10 * 0.00341802 + 149 - 273.15`. Temperatura da superfície permanece separada da temperatura do ar e do índice de calor IoT.
- Cada resultado registra dataset, imagem, data de observação, qualidade, parâmetros e versão do algoritmo. `dataStatus` informa `AVAILABLE`, `OUTDATED` ou `UNAVAILABLE`; o limite inicial de desatualização é de 60 dias desde a observação.
- A identidade do job inclui região, versão do polígono, período, algoritmo e parâmetros. Solicitações iguais reutilizam o mesmo job. Jobs falhos podem ser solicitados novamente; fontes já persistidas não são duplicadas.
- A fila usa aquisição atômica e uma posse de quinze minutos. Jobs interrompidos ficam disponíveis novamente após esse prazo. O processamento tem timeout de doze minutos e só persiste quando ainda detém a posse.
- Falha de uma fonte preserva resultados válidos da outra e os dados anteriores. Alterar o polígono exige nova análise para os indicadores atuais, mantendo o histórico anterior.
- Polígonos de vegetação têm área mínima inicial de 500 m², limite de 200 por análise e complexidade limitada. Representam estimativas de vegetação, não parques públicos confirmados. A lista pode ser limitada; os metadados registram a situação da vetorização.
- A atualização automática fica desabilitada inicialmente. Ao ativar `AutomaticRefreshEnabled`, o worker agenda regiões conforme `RefreshIntervalDays`, inicialmente sete dias. Isso não garante novas imagens válidas.

## Verificação

Os testes de API usam um contêiner PostGIS isolado. Os testes de cálculo interpretam as expressões produzidas pela integração para verificar NDVI, temperatura e máscaras sem depender do Google.

```powershell
dotnet test UrbanShade.slnx
```

O teste real é opcional e exige as credenciais locais de desenvolvimento e acesso à rede. Ele verifica o catálogo oficial de algoritmos, vegetação, vetorização, temperatura e ausência de imagens para uma região pequena:

```powershell
$env:URBANSHADE_EARTH_ENGINE_TEST = 'true'
dotnet test tests/UseCases.Tests --filter FullyQualifiedName~EarthEngineLiveTests
Remove-Item Env:URBANSHADE_EARTH_ENGINE_TEST
```

## Consultas relacionadas

Os endpoints de vegetação, temperatura superficial, histórico e áreas verdes estão no guia de [regiões e consultas geoespaciais](./regioes-e-consultas-geoespaciais.md). Para importar lugares mapeados no OSM, consulte [Áreas verdes do OpenStreetMap](./areas-verdes-openstreetmap.md).
