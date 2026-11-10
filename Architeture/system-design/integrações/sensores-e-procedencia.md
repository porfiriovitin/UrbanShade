# Sensores simulados

Os sensores serão simulados no próprio backend. Um componente interno gera as leituras dos dispositivos cadastrados e as encaminha para validação e persistência. Essa implementação atende a RF-030 a RF-036 e RF-038.

## Geração das leituras

A proposta é usar um BackgroundService com intervalo configurável. Para cada sensor ativo, ele gera sensorId, eventId, measuredAt, temperatureC e relativeHumidityPercent e chama o serviço de processamento.

A geração acontece periodicamente, sem depender da abertura de uma tela. Os cenários usam valores predefinidos ou variação controlada, com relógio e semente configuráveis para repetir a demonstração.

| Cenário | Comportamento |
|---|---|
| Normal | Leituras dentro dos limites cadastrados |
| Calor elevado | Aumento de temperatura em um dos sensores |
| Leitura inválida | Valor fora da faixa aceita |
| Sensor sem atualização | Interrupção da geração para um sensor |

O simulador funciona sem internet. Caso a previsão da Open-Meteo seja usada como base para algum cenário, a leitura gerada continua identificada como simulada.

## Processamento

A organização e o local são obtidos pelo cadastro do sensor. O processamento verifica se ele está ativo, valida o horário e aceita apenas valores finitos dentro das faixas configuradas. A umidade deve ficar entre 0 e 100%.

O instante da leitura e o instante do processamento são armazenados separadamente. A combinação de sensorId e eventId impede que a mesma leitura seja processada novamente. Leituras inválidas não atualizam indicadores nem geram alertas.

O fluxo será:

Geração → validação → persistência → classificação térmica → alertas → recálculo do IPIU → atualização por SSE.

O envio por SSE respeita as permissões do usuário e seu vínculo com a organização.

## Identificação dos dados

Cada leitura guarda fonte, natureza, horário, unidade, localização e situação de atualização. Para o gerador interno, a fonte será simulador-interno e a natureza será simulado.

Os cálculos de exposição e IPIU também guardam a versão do método e as referências das entradas. A interface deve indicar quando um resultado usa dados simulados. Sensores sem novas leituras serão marcados como desatualizados após o prazo configurado.

[Demais integrações](mapas-e-dados-urbanos.md)
