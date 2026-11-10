# Escopo do MVP

## Tecnologias e integrações

| Camada | Proposta |
|---|---|
| Mobile | Kotlin para Android, com interfaces nativas. |
| API | ASP.NET Core (C#), endpoints REST e Swagger/OpenAPI. |
| Arquitetura | Controllers, Services e Repositories. |
| Banco | Banco relacional com entidades para usuários, organizações, locais, sensores, leituras, alertas e intervenções. |
| IoT | Componente interno do backend que gera leituras para sensores simulados, sem serviço externo. |
| Clima | Open-Meteo como fonte de contexto meteorológico. |
| Geolocalização | Mapa interativo e pontos georreferenciados. |
| Inteligência | Algoritmo multicritério para IPIU e Dijkstra para rotas. |
| Testes | Testes automatizados das regras de negócio e documentação de casos de teste. |


## Critérios de aceite do MVP

- [ ] O cidadão consegue abrir o mapa e consultar pontos monitorados.
- [ ] O cidadão consegue informar origem/destino na região de teste e comparar duas rotas.
- [ ] O cidadão consegue ativar lembretes locais de hidratação.
- [ ] O gestor consegue visualizar indicadores e ranking IPIU com justificativas.
- [ ] O componente interno gera leituras válidas para dois sensores simulados e as encaminha ao fluxo de processamento.
- [ ] Uma leitura acima do limite configurado gera um alerta automaticamente.
- [ ] O evento altera os indicadores e recalcula a prioridade de pelo menos uma área.
- [ ] Dados simulados e estimados estão identificados corretamente.
- [ ] A autorização diferencia cidadão, gestor e administrador da plataforma.
- [ ] Testes automatizados verificam alertas, cálculo de prioridade e principais regras de acesso.

[Voltar ao índice](../requisitos-funcionais.md)
