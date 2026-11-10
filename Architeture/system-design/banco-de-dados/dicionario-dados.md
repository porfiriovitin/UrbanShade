# Dicionário de dados

Os nomes físicos usam snake_case. UUID corresponde a Guid; datas timestamptz recebem valores UTC. As restrições completas estão em [01-schema.sql](01-schema.sql).

## users

Contas e perfil global de acesso.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| name | varchar(150) | Sim |  |
| email | varchar(254) | Sim | UK |
| hashed_password | text | Sim |  |
| role | smallint | Sim |  |
| active | boolean | Sim |  |

## refresh_tokens

Hashes de refresh tokens, famílias de rotação e revogação.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| token | text | Sim | UK |
| expires_at | timestamptz | Sim |  |
| user_id | uuid | Sim | FK |
| family_id | uuid | Sim |  |
| revoked_at | timestamptz | Não |  |

## organizations

Instituições do cenário acadêmico.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| name | varchar(150) | Sim |  |
| active | boolean | Sim |  |

## organization_users

Vínculos de usuários com organizações; relação muitos para muitos.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| organization_id | uuid | Sim | PK, FK |
| user_id | uuid | Sim | PK, FK |
| active | boolean | Sim |  |
| joined_at | timestamptz | Sim |  |

## areas

Áreas monitoradas de uma organização, com coordenada central.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| organization_id | uuid | Sim | FK |
| name | varchar(150) | Sim |  |
| latitude | numeric(9,6) | Sim |  |
| longitude | numeric(9,6) | Sim |  |
| active | boolean | Sim |  |

## locations

Pontos monitorados e equipamentos urbanos dentro de uma área.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| name | varchar(150) | Sim |  |
| kind | varchar(20) | Sim |  |
| latitude | numeric(9,6) | Sim |  |
| longitude | numeric(9,6) | Sim |  |
| active | boolean | Sim |  |

## sensors

Sensores simulados associados a um local e seus limites de operação.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| location_id | uuid | Sim | FK |
| code | varchar(60) | Sim | UK |
| active | boolean | Sim |  |
| stale_after_seconds | integer | Sim |  |
| temperature_min_c | numeric(5,2) | Sim |  |
| temperature_max_c | numeric(5,2) | Sim |  |

## sensor_readings

Histórico de leituras válidas produzidas pelo gerador interno.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| sensor_id | uuid | Sim | FK |
| event_id | uuid | Sim |  |
| measured_at | timestamptz | Sim |  |
| received_at | timestamptz | Sim |  |
| temperature_c | numeric(5,2) | Sim |  |
| relative_humidity_percent | numeric(5,2) | Sim |  |
| source | varchar(60) | Sim |  |
| nature | varchar(10) | Sim |  |

## weather_snapshots

Condições meteorológicas estimadas da Open-Meteo por área e instante.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| reference_at | timestamptz | Sim |  |
| collected_at | timestamptz | Sim |  |
| latitude | numeric(9,6) | Sim |  |
| longitude | numeric(9,6) | Sim |  |
| temperature_c | numeric(5,2) | Não |  |
| relative_humidity_percent | numeric(5,2) | Não |  |
| apparent_temperature_c | numeric(5,2) | Não |  |
| shortwave_radiation_w_m2 | numeric(8,2) | Não |  |
| source | varchar(30) | Sim |  |
| nature | varchar(10) | Sim |  |

## environmental_snapshots

Atributos ambientais de um local, separados por fonte, data e método.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| location_id | uuid | Sim | FK |
| reference_at | timestamptz | Sim |  |
| collected_at | timestamptz | Sim |  |
| source | varchar(100) | Sim |  |
| nature | varchar(10) | Sim |  |
| method_version | varchar(50) | Sim |  |
| vegetation_fraction | numeric(6,5) | Não |  |
| pedestrian_score | numeric(6,5) | Não |  |
| vulnerability_score | numeric(6,5) | Não |  |
| surface_temperature_c | numeric(6,2) | Não |  |
| valid_pixel_fraction | numeric(6,5) | Não |  |
| notes | text | Não |  |

## alert_rules

Limites configurados por local e variável monitorada.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| location_id | uuid | Sim | FK |
| name | varchar(100) | Sim |  |
| metric | varchar(20) | Sim |  |
| comparison | varchar(2) | Sim |  |
| threshold | numeric(6,2) | Sim |  |
| active | boolean | Sim |  |

## alerts

Ocorrências de alerta, com leitura de origem e cópia da regra aplicada.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| location_id | uuid | Sim | FK |
| rule_id | uuid | Sim | FK |
| trigger_reading_id | uuid | Sim | FK |
| rule_metric | varchar(20) | Sim |  |
| rule_comparison | varchar(2) | Sim |  |
| rule_threshold | numeric(6,2) | Sim |  |
| status | varchar(15) | Sim |  |
| severity | varchar(10) | Sim |  |
| message | text | Sim |  |
| opened_at | timestamptz | Sim |  |
| resolved_at | timestamptz | Não |  |

## user_alerts

Entrega e leitura de alertas por usuário.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| user_id | uuid | Sim | PK, FK |
| alert_id | uuid | Sim | PK, FK |
| received_at | timestamptz | Sim |  |
| read_at | timestamptz | Não |  |

## user_preferences

Configurações pessoais de hidratação e notificações.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| user_id | uuid | Sim | PK, FK |
| hydration_enabled | boolean | Sim |  |
| hydration_interval_minutes | integer | Sim |  |
| heat_notifications_enabled | boolean | Sim |  |
| route_notifications_enabled | boolean | Sim |  |

## priority_criteria

Catálogo dos critérios utilizados no IPIU.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| code | varchar(50) | Sim | UK |
| name | varchar(100) | Sim |  |

## priority_assessments

Histórico dos cálculos de IPIU por área.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| calculated_at | timestamptz | Sim |  |
| method_version | varchar(50) | Sim |  |
| score | numeric(5,2) | Sim |  |
| thermal_exposure | numeric(6,5) | Sim |  |
| explanation | text | Sim |  |
| recommendation | text | Sim |  |
| contains_simulated_inputs | boolean | Sim |  |

## priority_scores

Entradas normalizadas, pesos e contribuições de cada cálculo.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| assessment_id | uuid | Sim | PK, FK |
| criterion_id | uuid | Sim | PK, FK |
| normalized_value | numeric(6,5) | Sim |  |
| weight | numeric(6,5) | Sim |  |
| contribution | numeric(12,7) | Não |  |
| source | varchar(100) | Sim |  |
| nature | varchar(10) | Sim |  |
| reference_at | timestamptz | Sim |  |
| evidence | text | Sim |  |

## interventions

Propostas de intervenção e seu estado.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| created_by_user_id | uuid | Sim | FK |
| title | varchar(150) | Sim |  |
| description | text | Sim |  |
| kind | varchar(20) | Sim |  |
| status | varchar(15) | Sim |  |
| created_at | timestamptz | Sim |  |

## intervention_scenarios

Simulações comparadas a uma avaliação de referência da mesma área.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| baseline_assessment_id | uuid | Sim | FK |
| created_by_user_id | uuid | Sim | FK |
| name | varchar(150) | Sim |  |
| method_version | varchar(50) | Sim |  |
| simulated_score | numeric(5,2) | Sim |  |
| assumptions | text | Sim |  |
| created_at | timestamptz | Sim |  |

## scenario_scores

Valores e pesos hipotéticos de cada cenário.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| scenario_id | uuid | Sim | PK, FK |
| criterion_id | uuid | Sim | PK, FK |
| normalized_value | numeric(6,5) | Sim |  |
| weight | numeric(6,5) | Sim |  |
| contribution | numeric(12,7) | Não |  |

## route_nodes

Vértices do grafo de caminhada.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| name | varchar(100) | Não |  |
| latitude | numeric(9,6) | Sim |  |
| longitude | numeric(9,6) | Sim |  |

## route_edges

Arestas dirigidas, com distância, sombra e exposição estimada.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| area_id | uuid | Sim | FK |
| from_node_id | uuid | Sim | FK |
| to_node_id | uuid | Sim | FK |
| distance_m | numeric(10,2) | Sim |  |
| shade_fraction | numeric(6,5) | Sim |  |
| thermal_exposure | numeric(6,5) | Sim |  |
| source | varchar(100) | Sim |  |
| nature | varchar(10) | Sim |  |
| reference_at | timestamptz | Sim |  |
| method_version | varchar(50) | Sim |  |
| active | boolean | Sim |  |

## audit_logs

Histórico de ações administrativas.

| Campo | Tipo PostgreSQL | Obrigatório | Chave |
|---|---|---|---|
| id | uuid | Sim | PK |
| actor_user_id | uuid | Sim | FK |
| organization_id | uuid | Não | FK |
| action | varchar(60) | Sim |  |
| entity_type | varchar(60) | Sim |  |
| entity_id | uuid | Sim |  |
| occurred_at | timestamptz | Sim |  |
| details | jsonb | Sim |  |

## Convenções

- Temperaturas do ar e da superfície têm campos separados, em graus Celsius.
- Umidade é percentual de 0 a 100. Frações e valores normalizados variam de 0 a 1.
- measured_at/reference_at indicam a referência do dado; received_at/collected_at registram sua entrada.
- nature aceita observed, estimated ou simulated conforme a tabela. Leituras dos sensores do MVP aceitam apenas simulated.
- Contribuições são colunas geradas pelo PostgreSQL; não são enviadas nos INSERTs.
- Unicidades compostas, índices, valores padrão e CHECKs estão no script de criação.
