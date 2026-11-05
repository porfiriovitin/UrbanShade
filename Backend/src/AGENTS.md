## Regras gerais

Antes de alterar qualquer código:

1. Use sempre a **skill `caveman`**.
2. Mapeie o projeto e entenda:

   * estrutura de pastas;
   * arquitetura;
   * padrões utilizados;
   * fluxo relacionado à solicitação;
   * dependências afetadas.
3. Não implemente imediatamente.

## Proposta antes da implementação

Após analisar o projeto, apresente uma proposta contendo:

* o que será feito;
* abordagem escolhida;
* arquivos que serão criados;
* arquivos que serão alterados;
* possíveis impactos.

Exemplo:

```text
Proposta:

Objetivo:
Adicionar validação de reservas.

Arquivos afetados:
- src/services/booking.service.ts
- src/controllers/booking.controller.ts
- src/schemas/booking.schema.ts

Abordagem:
- adicionar validação no schema;
- manter regra de negócio no service;
- ajustar controller apenas para consumir o novo fluxo.
```

Depois da proposta, aguarde minha aprovação antes de implementar.

## Refatorações

Nunca faça refatorações não solicitadas automaticamente.

Se identificar uma oportunidade de refatoração:

1. explique o problema;
2. explique o benefício;
3. liste os arquivos afetados;
4. apresente a proposta;
5. peça minha autorização.

Somente refatore após aprovação explícita.

## Durante a implementação

* Altere somente o necessário.
* Preserve a arquitetura e os padrões existentes.
* Evite abstrações desnecessárias.
* Evite criar arquivos sem necessidade.
* Não altere comportamento fora do escopo solicitado.
* Não faça mudanças adicionais "aproveitando" a tarefa.

Se durante a implementação surgir necessidade de alterar arquivos que não estavam na proposta inicial, informe antes de continuar.

## Fluxo obrigatório

```text
Solicitação
↓
Skill caveman
↓
Mapear projeto
↓
Analisar impacto
↓
Listar arquivos afetados
↓
Apresentar proposta
↓
Aguardar aprovação
↓
Implementar
```