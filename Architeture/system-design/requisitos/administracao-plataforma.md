# Administração da plataforma

O administrador da plataforma representa a equipe responsável pelo UrbanShade. A equipe administra os dados e os sensores simulados do projeto acadêmico.

No MVP, as leituras são geradas dentro do próprio backend. Não há instalação de sensores físicos nem serviço externo de simulação.

## Requisitos funcionais

| ID | Requisito funcional | Prioridade |
|---|---|---|
| RF-029 | O administrador da plataforma deve cadastrar sensores associados a pontos geográficos e vinculados à organização atendida. | P0 |
| RF-039 | O administrador da plataforma deve consultar a situação e o horário da última comunicação de cada sensor. | P1 |
| RF-043 | O administrador da plataforma deve cadastrar e editar locais e seus atributos ambientais. | P0 |
| RF-044 | O administrador da plataforma deve configurar limites para geração de alertas. | P0 |
| RF-046 | O administrador da plataforma deve poder ativar ou desativar sensores. | P1 |
| RF-042 | O administrador da plataforma deve gerenciar organizações e áreas monitoradas vinculadas a elas. | P0 |

## Responsabilidades operacionais

A equipe da plataforma cadastra organizações, áreas, locais e sensores simulados, configura limites de alerta e cenários de simulação e acompanha a geração das leituras. Também ativa ou desativa os sensores cadastrados.

Os gestores das organizações do cenário acadêmico consultam os dados dos locais vinculados e utilizam as funções de [Gestão](gestao.md). A configuração da infraestrutura permanece sob responsabilidade da plataforma.

O RF-042 abrange organizações e áreas sob o mesmo perfil responsável. Seus IDs e os dos demais requisitos foram preservados.

## Regras e acessos relacionados

- Autenticação, autorização e auditoria seguem [Acesso e autenticação](acesso-e-autenticacao.md).
- Validação de leituras, alertas automáticos e indicação de dados desatualizados seguem [Processamento ambiental](processamento-ambiental.md), incluindo RN-001, RN-005 e RN-010.
- O contexto do cliente e o cenário acadêmico estão em [Organização](organizacao.md).

[Voltar ao índice](../requisitos-funcionais.md)
