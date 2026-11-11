# Municípios, organizações e análise térmica

O município identifica onde o sensor está instalado. A organização identifica quem é responsável pelo sensor. Uma organização pode administrar sensores em vários municípios, e um município pode conter sensores de várias organizações.

O cálculo térmico continua sendo executado por medição, usando temperatura em Celsius e umidade. Os filtros geográficos e institucionais selecionam os sensores usados nas consultas; não alteram a fórmula.

## Cadastro de município

Um administrador da plataforma cadastra o município com `POST /api/cities`:

```json
{
  "name": "São Paulo",
  "state": "SP",
  "ibgeCode": "3550308"
}
```

O código IBGE é único no banco. A validação exige sete algarismos e verifica a correspondência entre o prefixo do código e a UF. O cadastro é manual: não importa o catálogo nem consulta a existência do município em um serviço externo do IBGE.

Usuários autenticados consultam os municípios cadastrados em `GET /api/cities` e `GET /api/cities/{id}`.

Referência do formato: [Códigos dos Municípios — IBGE](https://www.ibge.gov.br/explica/codigos-dos-municipios.php).

## Vínculo do sensor

O administrador informa `cityId` em `POST /api/sensors` ou em `PATCH /api/sensors/{id}`. O identificador deve corresponder a um município cadastrado.

No PATCH, omitir `cityId` preserva o vínculo; enviar `cityId: null` remove o vínculo. O vínculo institucional é administrado separadamente pelo endpoint existente `POST /api/organizations/{id}/sensors/{sensorId}`.

Sensores existentes continuam com `CityId` nulo até serem associados. Eles permanecem nas consultas gerais e da própria organização, mas não aparecem no filtro de um município específico.

O município é informado pelo administrador. A localização por latitude e longitude não determina automaticamente o município. Alterar `cityId` muda o agrupamento atual do sensor e não recalcula ou remove suas medições e análises históricas.

## Consultas térmicas municipais

Todos os endpoints exigem JWT de acesso válido.

- `GET /api/thermal/map?cityId={cityId}`
- `GET /api/thermal/overview?cityId={cityId}`

Essas consultas consideram os sensores ativos e não excluídos do município, independentemente da organização responsável. Omitir `cityId` mantém o comportamento geral da API.

Os pontos do mapa incluem `cityId` e `organizationId`, além das coordenadas e dos dados térmicos. Os campos podem ser nulos quando não houver vínculo.

## Consultas institucionais

- `GET /api/organizations/{id}/thermal/map`
- `GET /api/organizations/{id}/thermal/overview`

As duas consultas aceitam `?cityId={cityId}`. Nesse caso, o resultado considera somente os sensores que pertencem simultaneamente à organização e ao município informado.

O acesso reutiliza `OrganizationAccess`: administradores da plataforma podem consultar a organização; gestores precisam de vínculo ativo com uma organização ativa. A autorização consulta o estado atual no banco, inclusive depois da emissão do JWT. Consultas a organizações sem autorização retornam 404, seguindo o padrão institucional existente.

Os endpoints gerais permanecem disponíveis aos usuários autenticados conforme a SPEC 03; o acesso institucional acrescenta uma consulta restrita aos sensores da organização.

## Indicadores e ausência de dados

O mapa inclui sensores ativos sem análise, com `NO_DATA` e valores térmicos nulos. Sensores com análise antiga aparecem com `STALE`.

O resumo conta todos os sensores ativos selecionados, mas calcula médias, máximo e distribuição de risco somente a partir da análise mais recente da versão atual, com dados `CURRENT`.

Um município cadastrado sem sensores retorna mapa vazio e resumo com contagens zero e médias e máximo nulos. Município não cadastrado retorna 404. Indicadores municipais representam os pontos monitorados, não uma estimativa da temperatura de todo o território.

## Banco de dados

A migration `AddCitiesAndSensorLocation` cria `Cities`, adiciona `Sensors.CityId` como campo opcional e cria a chave estrangeira e os índices. O histórico térmico existente é preservado. A migration segue o fluxo de aplicação automática na inicialização da API.
