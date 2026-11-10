# Open-Meteo

A Open-Meteo será usada para consultar temperatura, umidade e previsão nas áreas monitoradas, atendendo ao RF-037. Os valores vêm de modelos meteorológicos e serão identificados como estimados na aplicação.

## Dados utilizados

| Grupo | Campos | Uso |
|---|---|---|
| current | temperature_2m, relative_humidity_2m, apparent_temperature | Condições atuais da região |
| hourly | temperature_2m, relative_humidity_2m, apparent_temperature, shortwave_radiation | Histórico recente de previsão e evolução horária |

A radiação ajuda a contextualizar a exposição ao calor, mas a sombra de cada trecho depende dos atributos cadastrados no grafo.

## Exemplo de consulta

A consulta abaixo usa coordenadas do centro de São Paulo. Ainda precisa ser testada na implementação.

~~~http
GET https://api.open-meteo.com/v1/forecast?latitude=-23.5505&longitude=-46.6333&current=temperature_2m,relative_humidity_2m,apparent_temperature&hourly=temperature_2m,relative_humidity_2m,apparent_temperature,shortwave_radiation&forecast_days=2&timezone=UTC
~~~

## Implementação

O backend terá um cliente HTTP próprio para a Open-Meteo, com DTOs separados dos modelos da aplicação. A consulta será feita por área e compartilhada entre os usuários por cache. O intervalo inicial será de 15 minutos, configurável.

Cada registro deve guardar fonte, unidade, coordenadas solicitadas e retornadas, horário de referência e horário da coleta. Os valores horários serão selecionados pelo timestamp da janela consultada.

Timeouts e respostas 429 terão tentativas limitadas, com intervalo progressivo e respeito ao Retry-After. Se a consulta falhar, a aplicação poderá mostrar o último registro com indicação de desatualização. Campos ausentes continuarão sem valor; zero é um valor válido e não representa ausência de dados.

A simulação dos sensores funciona independentemente dessa integração.

## Acesso

O projeto utilizará a API gratuita para uso acadêmico não comercial, respeitando seus limites e a atribuição CC BY 4.0.

- [Documentação](https://open-meteo.com/en/docs)
- [Termos de uso](https://open-meteo.com/en/terms)

[Demais integrações](mapas-e-dados-urbanos.md)
