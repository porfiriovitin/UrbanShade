# Acesso e autenticação

Os três perfis e os documentos correspondentes estão no [índice de requisitos](../requisitos-funcionais.md#perfis-de-acesso). As permissões devem respeitar o perfil e o vínculo organizacional; o administrador da plataforma representa a equipe que opera o serviço, e o gestor representa uma organização do cenário acadêmico.

## Requisitos funcionais

| ID | Requisito funcional 
|---|---|---|
| RF-040 | O sistema deve permitir cadastro e autenticação de usuários. 
| RF-041 | O sistema deve aplicar autorização conforme o perfil de acesso.
| RF-045 | O sistema deve restringir o acesso dos gestores aos recursos autorizados das organizações às quais pertencem e impedir a administração da infraestrutura por esse perfil.
| RF-047 | O sistema deve registrar ações administrativas relevantes para auditoria. 

## Regras de negócio

| ID | Regra |
|---|---|
| RN-008 | Gestores só podem acessar recursos autorizados das organizações às quais pertencem e cadastrar propostas conforme suas permissões. A administração da infraestrutura cabe ao administrador da plataforma. |

## Consultas públicas e autenticação
Consultas públicas ao mapa podem ser disponibilizadas sem login. Configurações pessoais e funções administrativas exigem autenticação. Gestores não podem administrar dados de outras organizações. A administração de organizações, áreas, locais, sensores e limites é exclusiva da equipe da plataforma.

**Pendente de definição:** quais consultas serão públicas e quais exigirão login. O documento original apresenta o acesso público ao mapa como possibilidade.

## Segurança técnica prevista
Senhas com hash seguro, autenticação por token, autorização por perfil, validação de entradas, proteção de endpoints e controle administrativo dos cenários de simulação. O gerador interno utiliza os sensores cadastrados e não depende de credenciais de dispositivos externos.

## Telas compartilhadas
Telas de login, cadastro e detalhes poderão ser compartilhadas entre as experiências.

[Voltar ao índice](../requisitos-funcionais.md)
