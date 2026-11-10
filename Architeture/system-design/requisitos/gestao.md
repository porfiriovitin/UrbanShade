# Gestão

## Dashboard e monitoramento

| ID | Requisito funcional | 
|---|---|
| RF-018 | O gestor deve acessar um painel de indicadores climáticos das áreas autorizadas. | 
| RF-019 | O painel deve apresentar indicadores de temperatura, umidade, sensores e alertas. | 
| RF-020 | O gestor deve visualizar um mapa térmico das áreas monitoradas. | 
| RF-021 | O gestor deve consultar o histórico de medições por local e período. | 
| RF-022 | O sistema deve destacar locais com maior exposição térmica estimada. | 
| RF-023 | O sistema deve apresentar um ranking de áreas prioritárias para intervenção. | 
| RF-024 | O gestor deve consultar a pontuação, os critérios e a justificativa da prioridade atribuída a cada área. | 
| RF-025 | O gestor deve visualizar alertas ambientais e seus estados. | 
| RF-026 | O gestor deve cadastrar propostas de intervenção, como arborização e instalação de sombreamento. | 
| RF-027 | O gestor deve poder comparar cenários simulados de intervenção urbana. | 
| RF-028 | O gestor deve poder exportar relatórios de monitoramento e priorização. | 

## Índice de Prioridade de Intervenção Urbana (IPIU)

O UrbanShade terá um **índice de 0 a 100** para auxiliar na identificação dos locais onde intervenções poderão gerar maior benefício social.

Critérios propostos:
- Exposição térmica e condições ambientais.
- Falta de sombra ou arborização.
- Circulação estimada de pedestres.
- Presença de escolas, hospitais e pontos de ônibus.
- Vulnerabilidade da população potencialmente afetada.

Cada critério será normalizado, ponderado e registrado para permitir a explicação da pontuação final. Os pesos devem ser documentados e poderão ser inicialmente fixos no MVP.

**Exemplo ilustrativo:**
- **Local:** Avenida Central.
- **IPIU:** 89/100 — prioridade crítica.
- **Justificativa:** baixa cobertura vegetal, exposição elevada ao sol e proximidade de uma escola.
- **Recomendação:** estudar arborização da via e instalação de cobertura em ponto de ônibus.

> O algoritmo entrega apoio à decisão; não afirma que uma intervenção reduzirá a temperatura em valor específico sem medições posteriores.

## Regras de negócio

| ID | Regra |
|---|---|
| RN-004 | O IPIU deve variar entre 0 e 100 e possuir critérios e justificativa consultáveis. |

## Telas previstas

1. **Dashboard:** visão geral e indicadores.
2. **Mapa térmico:** locais monitorados e condições ambientais.
3. **Prioridades:** ranking IPIU, critérios e recomendações.
4. **Sensores:** dispositivos, medições e histórico.
5. **Administração:** áreas, organizações, usuários e parâmetros, com acesso exclusivo do administrador da plataforma.

Telas de login, cadastro e detalhes poderão ser compartilhadas entre as experiências.

As funções administrativas das telas dependem do perfil: consulte [Organização](organizacao.md), [Administração da plataforma](administracao-plataforma.md) e [Acesso e autenticação](acesso-e-autenticacao.md). O isolamento entre organizações segue RN-008; a identificação dos dados segue RF-038 e RN-009 em [Processamento ambiental](processamento-ambiental.md).

[Voltar ao índice](../requisitos-funcionais.md)
