# Regiões e consultas geoespaciais

Este guia reúne o cadastro de regiões, as consultas disponíveis e as permissões do módulo `/api/geo`. As consultas HTTP usam os dados persistidos no PostgreSQL/PostGIS.

Os exemplos usam dados fictícios. Substitua os IDs e tokens pelos valores retornados pela API do seu ambiente.

## Cadastro de regiões

Use o token de `PlatformAdministrator` para cadastrar uma região vinculada a uma cidade existente:

```http
POST /api/geo/regions
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "cityId": "<id da cidade>",
  "name": "Área de estudo",
  "description": "Polígono de teste no Parque Ibirapuera",
  "boundary": {
    "type": "Polygon",
    "coordinates": [[[-46.657, -23.589], [-46.654, -23.589], [-46.654, -23.586], [-46.657, -23.586], [-46.657, -23.589]]]
  }
}
```

As coordenadas seguem longitude, latitude, em SRID 4326. São aceitos `Polygon` e `MultiPolygon`, com validação de topologia, área e complexidade. O limite inicial é de 100 km² e 2.000 vértices.

## Consultas e manutenção

Usuários autenticados podem consultar:

| Endpoint | Resultado |
| --- | --- |
| `GET /api/geo/regions?cityId=<id>&page=1&pageSize=20` | Regiões ativas |
| `GET /api/geo/regions/<id>` | Região e polígono |
| `GET /api/geo/regions/<id>/vegetation` | NDVI, cobertura e área vegetal |
| `GET /api/geo/regions/<id>/surface-temperature` | Temperatura superficial em Celsius |
| `GET /api/geo/regions/<id>/analyses?startAt=<UTC>&endAt=<UTC>&page=1&pageSize=20` | Histórico das observações |
| `GET /api/geo/green-areas?bbox=-46.66,-23.60,-46.65,-23.58&page=1&pageSize=20` | `FeatureCollection` GeoJSON |

As respostas comuns seguem `PayloadResponse<T>`. Áreas verdes retornam GeoJSON diretamente, com `Content-Type: application/geo+json`. O tamanho máximo da página é 100; o bbox admite até um grau por eixo.

`PATCH /regions/<id>` atualiza nome, descrição e/ou polígono. Campos omitidos são preservados; envie descrição vazia para limpar o texto. `DELETE /regions/<id>` desativa logicamente a região. Essas operações e `refresh` exigem administração e compartilham o limite de dez solicitações por minuto por usuário. A consulta de jobs também é administrativa.

## Processamento dos dados

Após cadastrar uma região, consulte o guia da fonte desejada:

- [Integração com satélites](./integracao-satelites.md): configuração do Earth Engine, solicitação de processamento, indicadores e testes.
- [Áreas verdes do OpenStreetMap](./areas-verdes-openstreetmap.md): configuração do Overpass, importação, consulta e atribuição.

Para o fluxo de cidades e indicadores térmicos, consulte [Municípios e análise térmica](./municipios-e-analise-termica.md).
