# Processamento ambiental

Estes requisitos descrevem os comportamentos automáticos e as integrações que atendem às experiências do cidadão e da gestão. O cadastro e a administração de sensores estão em [Administração da plataforma](administracao-plataforma.md).

## Requisitos funcionais

| ID | Requisito funcional | 
|---|---|
| RF-030 | O backend deve gerar leituras de temperatura e umidade de sensores simulados por um componente interno e encaminhá-las ao fluxo de validação e persistência, sem serviço externo de simulação. | 
| RF-031 | A API deve validar o identificador do sensor, o formato e a faixa permitida dos valores. | 
| RF-032 | A API deve armazenar cada leitura com data, hora, sensor e origem do dado. | 
| RF-033 | O sistema deve atualizar os indicadores após uma nova medição válida. | 
| RF-034 | O sistema deve detectar automaticamente condições que ultrapassem os limites configurados. | 
| RF-035 | O sistema deve gerar alertas vinculados aos pontos ou às áreas afetadas. | 
| RF-036 | O sistema deve recalcular classificações térmicas e prioridades das áreas impactadas. | 
| RF-037 | O sistema deve consultar dados meteorológicos reais da Open-Meteo para contextualização. | 
| RF-038 | O aplicativo deve distinguir dados observados, estimados e simulados. | 

## Regras de negócio

| ID | Regra |
|---|---|
| RN-001 | Toda leitura deve estar vinculada a um sensor cadastrado e autorizado. |
| RN-002 | Leituras inválidas não devem atualizar indicadores nem gerar alertas. |
| RN-003 | A classificação térmica deve utilizar apenas dados disponíveis e uma metodologia definida. |
| RN-005 | Alertas devem ser criados quando as regras ou limites configurados forem atendidos. |
| RN-006 | A persistência de uma mesma condição não deve gerar notificações duplicadas continuamente. |
| RN-009 | Dados simulados, estimados e observados devem ser identificados na interface. |
| RN-010 | Dados de sensores sem atualização dentro do período configurado devem ser marcados como desatualizados. |

## Fluxo de demonstração IoT

1. Um componente interno do backend gera leituras para dois sensores simulados cadastrados.
2. O fluxo de processamento valida os dados gerados e registra as medições no banco relacional.
3. O cenário de simulação passa a gerar temperatura acima do limite configurado para um dos sensores.
4. O motor de regras identifica a condição e gera um alerta.
5. A classificação térmica do local é atualizada.
6. O IPIU das áreas afetadas é recalculado.
7. O aplicativo do cidadão exibe o alerta.
8. O painel do gestor apresenta a atualização e a justificativa do novo ranking.

Para o MVP, a atualização das telas pode ocorrer por **SSE** (Server-Sent Events). Lembretes locais de hidratação podem ser implementados com recursos nativos do Android.

O cálculo e a explicação do IPIU estão em [Gestão](gestao.md); a comparação de rotas térmicas está em [Usuário](usuario.md).

[Voltar ao índice](../requisitos-funcionais.md)
