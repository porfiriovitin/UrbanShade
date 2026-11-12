# Segurança e execução

## Contratos de autenticação

`POST /api/auth/register` retorna `202 Accepted` com mensagem genérica, sem dados do usuário nem tokens, para endereço novo ou existente. Dados inválidos continuam retornando `400`. Após cadastrar, o cliente deve chamar `POST /api/auth/login`.

O cadastro acadêmico não verifica posse do e-mail. Por isso, ao adicionar um `Citizen` como responsável institucional, o administrador recebe uma nova senha temporária, mesmo que a conta já exista. A senha anterior e todas as sessões anteriores deixam de funcionar. O responsável precisa concluir o primeiro acesso. O administrador deve confirmar a identidade e entregar a credencial diretamente à pessoa correta por um canal confiável. Um endereço de e-mail isolado não é prova de identidade.

Contas que já são `Manager` ou `PlatformAdministrator` mantêm suas credenciais ao receber outro vínculo. Contas que foram promovidas antes desta correção devem ter sua identidade revisada pelo administrador; não há informação histórica suficiente para distinguir um pré-cadastro malicioso de um legítimo.

Access tokens contêm `iss`, `aud` e `sid`. `sid` identifica a família de refresh tokens, que deve ter um token vigente da mesma conta e versão de segurança. Logout e replay revogam toda a família, inclusive o acesso dos JWTs já emitidos. Rotação normal preserva o acesso da sessão. Logout não encerra sessões independentes.

JWTs antigos sem esses campos serão rejeitados. Solicite novo login após atualizar a API. Use chaves aleatórias diferentes por ambiente e serviço; configure `Jwt__SigningKey` fora do repositório. `Jwt__Issuer` e `Jwt__Audience` devem ser diferentes quando os serviços/ambientes tiverem limites de confiança distintos. A API valida ambos.

## Limites

- Antes da autenticação: 32 requisições simultâneas e 300 requisições por IP/minuto, sem fila.
- Autenticação: 20 requisições por IP/minuto; login e cadastro compartilham 10 tentativas por e-mail normalizado/minuto, mesmo vindo de IPs diferentes. Corpos limitados a 8192 bytes.
- Consultas: 60 requisições por conta/minuto nas famílias de leitura; permanecem os limites específicos de rotas e administração geoespacial/climática.
- `GET /api/sensors`: `page` entre 1 e 10000; `pageSize` entre 1 e 100, padrão 100. O envelope continua contendo uma lista. Percorra as páginas até receber menos que `pageSize` itens.
- Mapas térmicos e últimas leituras: máximo de 1000 sensores. Consultas maiores falham com `400`, sem devolver mapa parcial. Mapas térmicos podem ser reduzidos por cidade ou organização; para explorar todo o catálogo, use a listagem paginada.
- Metadados públicos expõem `sensorObservations` sem identificadores internos de sensores, leituras e análises.
- Cache institucional dedicado: orçamento de 16 MiB em unidades estimadas conservadoramente, máximo de 1 MiB por entrada. Itens grandes continuam sendo calculados, mas não ficam retidos no cache. O cache não substitui limites de memória do processo/contêiner.

Os limites de tráfego são locais ao processo. Para múltiplas réplicas, imponha também quotas globais no gateway ou use contadores compartilhados. Não confie em `X-Forwarded-For` enviado diretamente pelo cliente. Configure proxies confiáveis antes de usar o IP encaminhado como identidade.

As opções `SecurityTraffic__ConcurrentRequests`, `SecurityTraffic__RequestsPerMinute`, `SecurityTraffic__AuthenticationRequestsPerMinute` e `SecurityTraffic__AttemptsPerAccountPerMinute` ajustam os limites. Valores devem ser positivos e até 100000. Aumentos devem considerar memória do Argon2id, pool de conexões e capacidade do servidor.

## Banco e produção

O Compose local publica PostgreSQL somente em `127.0.0.1:5432`. Em produção, mantenha banco em rede privada e configure TLS com validação de certificado na conexão. A API não gerencia firewall nem certificados do banco.

Em Development e Tests, migrations e sensores de demonstração continuam automáticos. Nos demais ambientes, a API não executa DDL nem seed no startup. Aplique migrations previamente com identidade de implantação separada, por exemplo:

```powershell
dotnet ef database update --project src/Backend/UrbanShade.Infraestructure --startup-project src/Backend/UrbanShade.API
```

Execute esse comando com a conexão da identidade de implantação fornecida pelo ambiente seguro. Não coloque senhas na linha de comando nem publique configurações locais.

A conexão da API em produção deve usar uma identidade sem superusuário, criação de roles/bancos, replicação, bypass de RLS, propriedade das tabelas ou permissão de criar objetos em `public`. A API verifica essas condições antes de iniciar e recusa privilégios excessivos.

O arquivo `../Deployment/runtime-role.sql` concede permissões de dados a um grupo sem login. Um administrador do banco deve criar a identidade de login separadamente, atribuí-la ao grupo e configurar a conexão da API. Não conceda a ela a role proprietária/de implantação. Repita os grants de tabelas/sequências após migrations que criem objetos novos. O script não é executado pela API.

## Validação

As suítes HTTP usam bancos descartáveis em Testcontainers, nunca a conexão local da aplicação. Testes de serviços externos reais exigem autorização e ativação explícita. A consulta de dependências pode ser repetida com:

```powershell
dotnet list UrbanShade.slnx package --vulnerable --include-transitive --no-restore
```
