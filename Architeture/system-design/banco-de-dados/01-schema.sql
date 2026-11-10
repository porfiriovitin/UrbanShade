BEGIN;
CREATE SCHEMA urbanshade;
SET LOCAL search_path TO urbanshade, public;

CREATE TABLE users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(150) NOT NULL CHECK (btrim(name) <> ''),
    email varchar(254) NOT NULL UNIQUE CHECK (email = lower(btrim(email)) AND email <> ''),
    hashed_password text NOT NULL CHECK (hashed_password <> ''),
    role smallint NOT NULL DEFAULT 0 CHECK (role IN (0, 1, 2)),
    active boolean NOT NULL DEFAULT true
);

CREATE TABLE refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    token text NOT NULL UNIQUE CHECK (token <> ''),
    expires_at timestamptz NOT NULL,
    user_id uuid NOT NULL REFERENCES users(id),
    family_id uuid NOT NULL DEFAULT gen_random_uuid(),
    revoked_at timestamptz
);
COMMENT ON COLUMN refresh_tokens.token IS 'Hash do token, conforme RefreshTokens.Token. Nunca armazenar o token em texto puro.';
CREATE INDEX ix_refresh_tokens_user ON refresh_tokens(user_id);
CREATE INDEX ix_refresh_tokens_family ON refresh_tokens(family_id);
CREATE INDEX ix_refresh_tokens_expiration ON refresh_tokens(expires_at) WHERE revoked_at IS NULL;

CREATE TABLE organizations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(150) NOT NULL CHECK (btrim(name) <> ''),
    active boolean NOT NULL DEFAULT true
);
CREATE TABLE organization_users (
    organization_id uuid NOT NULL REFERENCES organizations(id),
    user_id uuid NOT NULL REFERENCES users(id),
    active boolean NOT NULL DEFAULT true,
    joined_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, user_id)
);
CREATE INDEX ix_organization_users_user ON organization_users(user_id, organization_id);

CREATE TABLE areas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL REFERENCES organizations(id),
    name varchar(150) NOT NULL CHECK (btrim(name) <> ''),
    latitude numeric(9,6) NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude numeric(9,6) NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    active boolean NOT NULL DEFAULT true,
    UNIQUE (organization_id, name)
);
CREATE TABLE locations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    name varchar(150) NOT NULL CHECK (btrim(name) <> ''),
    kind varchar(20) NOT NULL CHECK (kind IN ('monitoring','park','school','hospital','bus_stop','square')),
    latitude numeric(9,6) NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude numeric(9,6) NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    active boolean NOT NULL DEFAULT true,
    UNIQUE (area_id, name),
    UNIQUE (id, area_id)
);
CREATE TABLE sensors (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id uuid NOT NULL REFERENCES locations(id),
    code varchar(60) NOT NULL UNIQUE CHECK (btrim(code) <> ''),
    active boolean NOT NULL DEFAULT true,
    stale_after_seconds integer NOT NULL DEFAULT 300 CHECK (stale_after_seconds > 0),
    temperature_min_c numeric(5,2) NOT NULL DEFAULT -40,
    temperature_max_c numeric(5,2) NOT NULL DEFAULT 85,
    CHECK (temperature_min_c < temperature_max_c)
);
CREATE INDEX ix_sensors_location ON sensors(location_id);

CREATE TABLE sensor_readings (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sensor_id uuid NOT NULL REFERENCES sensors(id),
    event_id uuid NOT NULL,
    measured_at timestamptz NOT NULL,
    received_at timestamptz NOT NULL DEFAULT now(),
    temperature_c numeric(5,2) NOT NULL CHECK (temperature_c BETWEEN -100 AND 100),
    relative_humidity_percent numeric(5,2) NOT NULL CHECK (relative_humidity_percent BETWEEN 0 AND 100),
    source varchar(60) NOT NULL DEFAULT 'simulador-interno' CHECK (source = 'simulador-interno'),
    nature varchar(10) NOT NULL DEFAULT 'simulated' CHECK (nature = 'simulated'),
    UNIQUE (sensor_id, event_id)
);
CREATE INDEX ix_sensor_readings_latest ON sensor_readings(sensor_id, measured_at DESC, id DESC);

CREATE TABLE weather_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    reference_at timestamptz NOT NULL,
    collected_at timestamptz NOT NULL DEFAULT now(),
    latitude numeric(9,6) NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude numeric(9,6) NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    temperature_c numeric(5,2),
    relative_humidity_percent numeric(5,2) CHECK (relative_humidity_percent BETWEEN 0 AND 100),
    apparent_temperature_c numeric(5,2),
    shortwave_radiation_w_m2 numeric(8,2) CHECK (shortwave_radiation_w_m2 >= 0),
    source varchar(30) NOT NULL DEFAULT 'open-meteo' CHECK (source = 'open-meteo'),
    nature varchar(10) NOT NULL DEFAULT 'estimated' CHECK (nature = 'estimated'),
    UNIQUE (area_id, reference_at, source),
    CHECK (temperature_c IS NOT NULL OR relative_humidity_percent IS NOT NULL
        OR apparent_temperature_c IS NOT NULL OR shortwave_radiation_w_m2 IS NOT NULL)
);

CREATE TABLE environmental_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id uuid NOT NULL REFERENCES locations(id),
    reference_at timestamptz NOT NULL,
    collected_at timestamptz NOT NULL DEFAULT now(),
    source varchar(100) NOT NULL CHECK (btrim(source) <> ''),
    nature varchar(10) NOT NULL CHECK (nature IN ('observed','estimated','simulated')),
    method_version varchar(50) NOT NULL,
    vegetation_fraction numeric(6,5) CHECK (vegetation_fraction BETWEEN 0 AND 1),
    pedestrian_score numeric(6,5) CHECK (pedestrian_score BETWEEN 0 AND 1),
    vulnerability_score numeric(6,5) CHECK (vulnerability_score BETWEEN 0 AND 1),
    surface_temperature_c numeric(6,2),
    valid_pixel_fraction numeric(6,5) CHECK (valid_pixel_fraction BETWEEN 0 AND 1),
    notes text,
    CHECK (vegetation_fraction IS NOT NULL OR pedestrian_score IS NOT NULL
        OR vulnerability_score IS NOT NULL OR surface_temperature_c IS NOT NULL)
);
CREATE INDEX ix_environmental_location ON environmental_snapshots(location_id, reference_at DESC);
COMMENT ON TABLE environmental_snapshots IS 'Uma fonte, data e método por registro. Fontes diferentes geram registros distintos. Temperatura da superfície não é temperatura do ar.';

CREATE TABLE alert_rules (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id uuid NOT NULL REFERENCES locations(id),
    name varchar(100) NOT NULL,
    metric varchar(20) NOT NULL CHECK (metric IN ('temperature','humidity')),
    comparison varchar(2) NOT NULL CHECK (comparison IN ('>=','<=')),
    threshold numeric(6,2) NOT NULL,
    active boolean NOT NULL DEFAULT true,
    UNIQUE (id, location_id),
    CHECK (metric <> 'humidity' OR threshold BETWEEN 0 AND 100)
);
CREATE TABLE alerts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id uuid NOT NULL REFERENCES locations(id),
    rule_id uuid NOT NULL,
    trigger_reading_id uuid NOT NULL REFERENCES sensor_readings(id),
    rule_metric varchar(20) NOT NULL CHECK (rule_metric IN ('temperature','humidity')),
    rule_comparison varchar(2) NOT NULL CHECK (rule_comparison IN ('>=','<=')),
    rule_threshold numeric(6,2) NOT NULL,
    status varchar(15) NOT NULL DEFAULT 'open' CHECK (status IN ('open','acknowledged','resolved')),
    severity varchar(10) NOT NULL CHECK (severity IN ('low','moderate','high','critical')),
    message text NOT NULL CHECK (btrim(message) <> ''),
    opened_at timestamptz NOT NULL DEFAULT now(),
    resolved_at timestamptz,
    FOREIGN KEY (rule_id, location_id) REFERENCES alert_rules(id, location_id),
    CHECK ((status = 'resolved' AND resolved_at IS NOT NULL) OR (status <> 'resolved' AND resolved_at IS NULL)),
    CHECK (resolved_at IS NULL OR resolved_at >= opened_at)
);
CREATE UNIQUE INDEX uq_alerts_one_active_rule ON alerts(rule_id) WHERE status <> 'resolved';
CREATE INDEX ix_alerts_location ON alerts(location_id, opened_at DESC);
COMMENT ON TABLE alerts IS 'Snapshot da regra preserva a explicação após alteração dos limites.';
CREATE TABLE user_alerts (
    user_id uuid NOT NULL REFERENCES users(id),
    alert_id uuid NOT NULL REFERENCES alerts(id),
    received_at timestamptz NOT NULL DEFAULT now(),
    read_at timestamptz,
    PRIMARY KEY (user_id, alert_id),
    CHECK (read_at IS NULL OR read_at >= received_at)
);
CREATE INDEX ix_user_alerts_alert ON user_alerts(alert_id);
CREATE TABLE user_preferences (
    user_id uuid PRIMARY KEY REFERENCES users(id),
    hydration_enabled boolean NOT NULL DEFAULT false,
    hydration_interval_minutes integer NOT NULL DEFAULT 60 CHECK (hydration_interval_minutes BETWEEN 5 AND 1440),
    heat_notifications_enabled boolean NOT NULL DEFAULT true,
    route_notifications_enabled boolean NOT NULL DEFAULT true
);

CREATE TABLE priority_criteria (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL UNIQUE,
    name varchar(100) NOT NULL
);
CREATE TABLE priority_assessments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    calculated_at timestamptz NOT NULL DEFAULT now(),
    method_version varchar(50) NOT NULL,
    score numeric(5,2) NOT NULL CHECK (score BETWEEN 0 AND 100),
    thermal_exposure numeric(6,5) NOT NULL CHECK (thermal_exposure BETWEEN 0 AND 1),
    explanation text NOT NULL CHECK (btrim(explanation) <> ''),
    recommendation text NOT NULL,
    contains_simulated_inputs boolean NOT NULL,
    UNIQUE (id, area_id)
);
CREATE INDEX ix_priority_assessments_latest ON priority_assessments(area_id, calculated_at DESC, id DESC);
CREATE TABLE priority_scores (
    assessment_id uuid NOT NULL REFERENCES priority_assessments(id),
    criterion_id uuid NOT NULL REFERENCES priority_criteria(id),
    normalized_value numeric(6,5) NOT NULL CHECK (normalized_value BETWEEN 0 AND 1),
    weight numeric(6,5) NOT NULL CHECK (weight > 0 AND weight <= 1),
    contribution numeric(12,7) GENERATED ALWAYS AS (normalized_value * weight * 100) STORED,
    source varchar(100) NOT NULL,
    nature varchar(10) NOT NULL CHECK (nature IN ('observed','estimated','simulated')),
    reference_at timestamptz NOT NULL,
    evidence text NOT NULL CHECK (btrim(evidence) <> ''),
    PRIMARY KEY (assessment_id, criterion_id)
);

CREATE TABLE interventions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    created_by_user_id uuid NOT NULL REFERENCES users(id),
    title varchar(150) NOT NULL,
    description text NOT NULL,
    kind varchar(20) NOT NULL CHECK (kind IN ('trees','shade','other')),
    status varchar(15) NOT NULL DEFAULT 'proposed' CHECK (status IN ('proposed','approved','completed','rejected')),
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_interventions_area ON interventions(area_id);

CREATE TABLE intervention_scenarios (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    baseline_assessment_id uuid NOT NULL,
    created_by_user_id uuid NOT NULL REFERENCES users(id),
    name varchar(150) NOT NULL,
    method_version varchar(50) NOT NULL,
    simulated_score numeric(5,2) NOT NULL CHECK (simulated_score BETWEEN 0 AND 100),
    assumptions text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (baseline_assessment_id, area_id) REFERENCES priority_assessments(id, area_id)
);
CREATE TABLE scenario_scores (
    scenario_id uuid NOT NULL REFERENCES intervention_scenarios(id),
    criterion_id uuid NOT NULL REFERENCES priority_criteria(id),
    normalized_value numeric(6,5) NOT NULL CHECK (normalized_value BETWEEN 0 AND 1),
    weight numeric(6,5) NOT NULL CHECK (weight > 0 AND weight <= 1),
    contribution numeric(12,7) GENERATED ALWAYS AS (normalized_value * weight * 100) STORED,
    PRIMARY KEY (scenario_id, criterion_id)
);

CREATE TABLE route_nodes (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    name varchar(100),
    latitude numeric(9,6) NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude numeric(9,6) NOT NULL CHECK (longitude BETWEEN -180 AND 180),
    UNIQUE (id, area_id)
);
CREATE TABLE route_edges (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    area_id uuid NOT NULL REFERENCES areas(id),
    from_node_id uuid NOT NULL,
    to_node_id uuid NOT NULL,
    distance_m numeric(10,2) NOT NULL CHECK (distance_m > 0),
    shade_fraction numeric(6,5) NOT NULL CHECK (shade_fraction BETWEEN 0 AND 1),
    thermal_exposure numeric(6,5) NOT NULL CHECK (thermal_exposure BETWEEN 0 AND 1),
    source varchar(100) NOT NULL,
    nature varchar(10) NOT NULL CHECK (nature IN ('observed','estimated','simulated')),
    reference_at timestamptz NOT NULL,
    method_version varchar(50) NOT NULL,
    active boolean NOT NULL DEFAULT true,
    FOREIGN KEY (from_node_id, area_id) REFERENCES route_nodes(id, area_id),
    FOREIGN KEY (to_node_id, area_id) REFERENCES route_nodes(id, area_id),
    CHECK (from_node_id <> to_node_id),
    UNIQUE (from_node_id, to_node_id)
);
CREATE INDEX ix_route_edges_area ON route_edges(area_id);
COMMENT ON TABLE route_edges IS 'Arestas dirigidas: cadastrar a direção inversa quando permitida. area_id garante nós na mesma área por FK composta.';

CREATE TABLE audit_logs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_user_id uuid NOT NULL REFERENCES users(id),
    organization_id uuid REFERENCES organizations(id),
    action varchar(60) NOT NULL,
    entity_type varchar(60) NOT NULL,
    entity_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    details jsonb NOT NULL DEFAULT '{}'::jsonb
);
CREATE INDEX ix_audit_logs_organization ON audit_logs(organization_id, occurred_at DESC);
COMMENT ON COLUMN audit_logs.entity_id IS 'Referência histórica, sem FK polimórfica. Não incluir senhas ou tokens em details.';

-- Validação dos sensores também protege inserções diretas no banco.
CREATE FUNCTION validate_reading() RETURNS trigger LANGUAGE plpgsql SET search_path = urbanshade, pg_temp AS $$
DECLARE s sensors%ROWTYPE;
BEGIN
    SELECT * INTO s FROM sensors WHERE id = NEW.sensor_id;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Sensor não cadastrado' USING ERRCODE = '23503';
    END IF;
    IF NOT s.active OR NEW.temperature_c < s.temperature_min_c OR NEW.temperature_c > s.temperature_max_c THEN
        RAISE EXCEPTION 'Sensor inativo ou temperatura fora da faixa configurada' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER tr_validate_reading BEFORE INSERT OR UPDATE ON sensor_readings
FOR EACH ROW EXECUTE FUNCTION validate_reading();

CREATE FUNCTION validate_alert_reading() RETURNS trigger LANGUAGE plpgsql SET search_path = urbanshade, pg_temp AS $$
DECLARE reading_location uuid;
BEGIN
    SELECT s.location_id INTO reading_location
    FROM sensor_readings r JOIN sensors s ON s.id = r.sensor_id
    WHERE r.id = NEW.trigger_reading_id;
    IF reading_location IS DISTINCT FROM NEW.location_id THEN
        RAISE EXCEPTION 'Leitura e alerta devem pertencer ao mesmo local' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER tr_validate_alert_reading BEFORE INSERT OR UPDATE ON alerts
FOR EACH ROW EXECUTE FUNCTION validate_alert_reading();

-- Um cálculo completo e seus componentes são gravados na mesma transação.
CREATE FUNCTION validate_priority_total() RETURNS trigger LANGUAGE plpgsql SET search_path = urbanshade, pg_temp AS $$
DECLARE target_id uuid; total_weight numeric; total_score numeric; stored_score numeric;
BEGIN
    IF TG_OP = 'DELETE' THEN
        target_id := OLD.assessment_id;
    ELSIF TG_TABLE_NAME = 'priority_assessments' THEN
        target_id := NEW.id;
    ELSE
        IF TG_OP = 'UPDATE' AND OLD.assessment_id <> NEW.assessment_id THEN
            RAISE EXCEPTION 'O componente não pode mudar de avaliação' USING ERRCODE = '23514';
        END IF;
        target_id := NEW.assessment_id;
    END IF;
    SELECT score INTO stored_score FROM priority_assessments WHERE id = target_id;
    IF NOT FOUND THEN RETURN NULL; END IF;
    SELECT sum(weight), sum(contribution) INTO total_weight, total_score
    FROM priority_scores WHERE assessment_id = target_id;
    IF total_weight IS DISTINCT FROM 1::numeric OR round(total_score, 2) IS DISTINCT FROM stored_score THEN
        RAISE EXCEPTION 'IPIU inconsistente: pesos devem somar 1 e contribuições devem compor a pontuação' USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER tr_priority_parent AFTER INSERT OR UPDATE ON priority_assessments
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_priority_total();
CREATE CONSTRAINT TRIGGER tr_priority_components AFTER INSERT OR UPDATE OR DELETE ON priority_scores
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_priority_total();

CREATE FUNCTION validate_scenario_total() RETURNS trigger LANGUAGE plpgsql SET search_path = urbanshade, pg_temp AS $$
DECLARE target_id uuid; total_weight numeric; total_score numeric; stored_score numeric;
BEGIN
    IF TG_OP = 'DELETE' THEN target_id := OLD.scenario_id;
    ELSIF TG_TABLE_NAME = 'intervention_scenarios' THEN target_id := NEW.id;
    ELSE
        IF TG_OP = 'UPDATE' AND OLD.scenario_id <> NEW.scenario_id THEN
            RAISE EXCEPTION 'O componente não pode mudar de cenário' USING ERRCODE = '23514';
        END IF;
        target_id := NEW.scenario_id;
    END IF;
    SELECT simulated_score INTO stored_score FROM intervention_scenarios WHERE id = target_id;
    IF NOT FOUND THEN RETURN NULL; END IF;
    SELECT sum(weight), sum(contribution) INTO total_weight, total_score
    FROM scenario_scores WHERE scenario_id = target_id;
    IF total_weight IS DISTINCT FROM 1::numeric OR round(total_score, 2) IS DISTINCT FROM stored_score THEN
        RAISE EXCEPTION 'Cenário inconsistente: pesos e contribuições inválidos' USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER tr_scenario_parent AFTER INSERT OR UPDATE ON intervention_scenarios
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_scenario_total();
CREATE CONSTRAINT TRIGGER tr_scenario_components AFTER INSERT OR UPDATE OR DELETE ON scenario_scores
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_scenario_total();

COMMIT;
