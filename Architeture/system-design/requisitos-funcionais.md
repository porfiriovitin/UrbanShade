# Requisitos funcionais — UrbanShade

O modelo relacional, o diagrama ER e os scripts PostgreSQL estão em [Banco de dados](banco-de-dados/README.md).

O **UrbanShade** é uma plataforma de inteligência climática urbana que combina dados meteorológicos, sensores IoT simulados e análise geoespacial para identificar locais com maior exposição ao calor, apoiar deslocamentos de cidadãos e orientar intervenções urbanas.

O aplicativo terá **duas experiências**, com acesso conforme o perfil:

- **UrbanShade Cidadão:** mapa térmico, comparação de rotas com menor exposição ao calor, alertas, lembretes de hidratação e orientações preventivas.
- **UrbanShade Gestão:** monitoramento de áreas, análise de indicadores, ranking de prioridades e recomendações de intervenções para prefeituras, condomínios e outras organizações.

Ambas utilizam a mesma API e a mesma base de dados ambientais, com interfaces e permissões diferentes.

## Objetivos
1. Informar cidadãos sobre condições térmicas desfavoráveis.
2. Recomendar trajetos com menor exposição térmica **estimada** dentro da região coberta pelo MVP.
3. Monitorar temperatura e umidade em pontos cadastrados.
4. Gerar alertas a partir de leituras IoT e limites configurados.
5. Priorizar locais para intervenções urbanas por meio de um algoritmo multicritério explicável.
6. Apoiar decisões de organizações com indicadores e histórico ambiental.

## Perfis de acesso

| Perfil | Descrição | Permissões principais |
|---|---|---|
| Cidadão | Pessoa que consulta as condições térmicas da cidade. | Visualizar mapa, comparar rotas, consultar alertas e configurar lembretes. |
| Gestor | Representante de prefeitura, condomínio ou organização. | Consultar indicadores e prioridades das áreas autorizadas e cadastrar propostas conforme os requisitos de gestão. |
| Administrador da plataforma | Equipe responsável pela configuração e operação do UrbanShade. | Gerenciar organizações, áreas, locais, sensores e limites de alerta. |

O UrbanShade é um projeto acadêmico sem uso comercial. Organização representa uma instituição do cenário de demonstração e o contexto dos dados. Seus gestores acessam os locais vinculados conforme suas permissões. A equipe da plataforma administra sensores simulados dentro do próprio backend, sem serviço externo de simulação. As regras compartilhadas estão em [Acesso e autenticação](requisitos/acesso-e-autenticacao.md).

## Documentos de requisitos

| Documento | Conteúdo | Requisitos |
|---|---|---|
| [Usuário](requisitos/usuario.md) | Mapa, rotas, alertas, hidratação, preferências e telas do cidadão. | RF-001 a RF-017 |
| [Gestão](requisitos/gestao.md) | Indicadores, histórico, IPIU, propostas, simulações e relatórios. | RF-018 a RF-028 |
| [Organização](requisitos/organizacao.md) | Instituições do cenário acadêmico e acesso aos dados. | Referências aos requisitos de gestão e administração |
| [Administração da plataforma](requisitos/administracao-plataforma.md) | Operação do serviço e gerenciamento de organizações, áreas, locais, sensores e limites. | RF-029, RF-039, RF-042, RF-043, RF-044 e RF-046 |
| [Acesso e autenticação](requisitos/acesso-e-autenticacao.md) | Cadastro, login, autorização, isolamento e auditoria. | RF-040, RF-041, RF-045 e RF-047 |
| [Processamento ambiental](requisitos/processamento-ambiental.md) | Leituras, validação, indicadores, alertas, integração e fluxo IoT. | RF-030 a RF-038 |
| [Escopo do MVP](requisitos/escopo-mvp.md) | Tecnologias, plano de entrega, prioridades e critérios de aceite. | Transversal |

Os IDs RF e RN originais foram preservados. Cada requisito e regra possui uma única definição; os demais documentos usam referências. A definição das consultas públicas permanece pendente em Acesso e autenticação.

## Integrações e fontes de dados

As fontes propostas e a referência ao AquaGuard estão em [Integrações](integrações/README.md).

## Diferencial do UrbanShade

O projeto oferece duas respostas complementares a um mesmo problema urbano:

> **Para o cidadão:** “Qual caminho me expõe menos ao calor?”  
> **Para o gestor:** “Onde devemos agir primeiro para beneficiar mais pessoas?”

O UrbanShade integra **monitoramento climático + IoT + alertas + rotas com exposição estimada + priorização urbana explicável**, com foco em impacto social e uma demonstração ponta a ponta.
