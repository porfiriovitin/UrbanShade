# Usuário — Cidadão

## Mapa e navegação

| ID | Requisito funcional |
|---|---|---|
| RF-001 | O aplicativo deve exibir um mapa interativo da região monitorada. | 
| RF-002 | O aplicativo deve mostrar os pontos monitorados com temperatura e umidade. | 
| RF-003 | O aplicativo deve identificar áreas com diferentes níveis de exposição térmica estimada. | 
| RF-004 | O cidadão deve poder consultar detalhes de um ponto, incluindo a última leitura e seu horário. | 
| RF-005 | O cidadão deve poder selecionar origem e destino em uma área coberta pelo sistema de rotas. | 
| RF-006 | O aplicativo deve calcular ou consultar alternativas de trajeto entre origem e destino. | 
| RF-007 | O aplicativo deve comparar rotas por distância e exposição térmica estimada. | 
| RF-008 | O aplicativo deve destacar a rota recomendada pelo menor custo térmico estimado. | 
| RF-009 | O cidadão deve poder visualizar praças, áreas sombreadas e pontos de interesse cadastrados. |

**Exemplo:** entre uma rota de 600 m, exposta ao sol, e outra de 800 m, mais sombreada, o aplicativo poderá recomendar a segunda, indicando a diferença de distância e exposição estimada.

## Alertas, saúde e hidratação

| ID | Requisito funcional | Prioridade |
|---|---|---|
| RF-010 | O aplicativo deve apresentar o nível de risco relacionado ao calor da área consultada. | 
| RF-011 | O cidadão deve ser informado sobre alertas de calor elevado nas áreas monitoradas. | 
| RF-012 | O cidadão deve poder ativar lembretes periódicos de hidratação. | 
| RF-013 | O aplicativo deve disponibilizar informações educativas sobre riscos da exposição excessiva ao calor. | 
| RF-014 | O aplicativo deve exibir recomendações preventivas relacionadas às condições ambientais. | 
| RF-015 | O cidadão deve poder configurar a frequência dos lembretes e os tipos de notificações. | 
| RF-016 | O cidadão deve poder consultar o histórico de alertas recebidos. | 
| RF-017 | O aplicativo deve avisar quando uma rota escolhida apresentar exposição térmica elevada. | 

**Exemplos de alertas:**

- **Hidratação:** “Hora de se hidratar! Lembre-se de beber água regularmente.”
- **Calor elevado:** “Condições térmicas desfavoráveis detectadas na região central. Prefira locais sombreados.”
- **Rota alternativa:** “Existe um trajeto com menor exposição térmica estimada até o seu destino.”

> As recomendações de saúde têm caráter educativo e preventivo; não substituem orientação profissional. Os lembretes de hidratação não devem impor uma necessidade universal de consumo de água.

## Regras de negócio

| ID | Regra |
|---|---|
| RN-007 | As rotas devem considerar distância e exposição térmica estimada, não apenas o menor percurso. |

## Telas previstas

1. **Início:** condições climáticas, risco da região e recomendações.
2. **Mapa:** pontos monitorados, temperaturas e regiões classificadas.
3. **Rotas:** definição de origem/destino e comparação de trajetos.
4. **Alertas:** avisos de calor e lembretes de hidratação.
5. **Perfil:** preferências, notificações e dados pessoais.

## Estratégia de rotas térmicas para o MVP

Para preservar o prazo, o roteamento será limitado a **um conjunto de ruas previamente mapeado**, modelado como um grafo:

- **Vértices:** cruzamentos ou pontos de referência.
- **Arestas:** trechos percorríveis entre vértices.
- **Atributos de cada aresta:** distância, nível estimado de sombra e exposição térmica.

Uma possível função de custo é:

```text
custo(trecho) = distancia_metros * (1 + peso_calor * exposicao_normalizada)
```

Em que `exposicao_normalizada` varia de 0 a 1 e `peso_calor` controla a importância do conforto térmico frente à distância.

O algoritmo de **Dijkstra** poderá encontrar a rota de menor distância e a rota de menor custo térmico. A interface exibirá as duas opções para comparação.

**Limitação explícita:** o modelo não mede a sensação térmica exata de cada rua e não garante sombra contínua. O resultado é uma estimativa baseada nos atributos disponíveis.

A identificação da origem dos dados segue RF-038 e RN-009 em [Processamento ambiental](processamento-ambiental.md). Preferências pessoais exigem autenticação, conforme [Acesso e autenticação](acesso-e-autenticacao.md).

[Voltar ao índice](../requisitos-funcionais.md)
