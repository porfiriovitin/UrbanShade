-- 1. Dashboard por área. 
SELECT a.id, a.name,
       (SELECT count(*) FROM locations l WHERE l.area_id = a.id AND l.active) AS locations,
       (SELECT count(*) FROM sensors s JOIN locations l ON l.id = s.location_id
        WHERE l.area_id = a.id AND l.active AND s.active) AS active_sensors,
       (SELECT count(*) FROM alerts al JOIN locations l ON l.id = al.location_id
        WHERE l.area_id = a.id AND al.status <> 'resolved') AS active_alerts
FROM areas a
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
WHERE u.id = :'manager_id'::uuid AND a.active
ORDER BY a.name;

-- 2. Última leitura por sensor, incluindo sensores sem leitura.
SELECT a.name AS area, l.name AS location, s.code, r.measured_at,
       r.temperature_c, r.relative_humidity_percent, r.nature,
       CASE WHEN r.id IS NULL THEN 'without_reading'
            WHEN r.measured_at < now() - make_interval(secs => s.stale_after_seconds) THEN 'stale'
            ELSE 'updated' END AS reading_status
FROM sensors s
JOIN locations l ON l.id = s.location_id
JOIN areas a ON a.id = l.area_id
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
LEFT JOIN LATERAL (
    SELECT sr.* FROM sensor_readings sr WHERE sr.sensor_id = s.id
    ORDER BY sr.measured_at DESC, sr.id DESC LIMIT 1
) r ON true
WHERE u.id = :'manager_id'::uuid AND a.active AND l.active AND s.active
ORDER BY a.name, s.code;

-- 3. Histórico horário por local, com intervalo semiaberto [início, fim).
SELECT l.id, l.name, date_trunc('hour', r.measured_at) AS hour_utc,
       count(*) AS readings, round(avg(r.temperature_c), 2) AS avg_temperature_c,
       max(r.temperature_c) AS max_temperature_c,
       round(avg(r.relative_humidity_percent), 2) AS avg_humidity_percent
FROM sensor_readings r
JOIN sensors s ON s.id = r.sensor_id
JOIN locations l ON l.id = s.location_id
JOIN areas a ON a.id = l.area_id
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
WHERE u.id = :'manager_id'::uuid
  AND r.measured_at >= :'start_at'::timestamptz
  AND r.measured_at < :'end_at'::timestamptz
GROUP BY l.id, l.name, date_trunc('hour', r.measured_at)
ORDER BY l.name, hour_utc;

-- 4. Ranking pelo cálculo mais recente de cada área, sem misturar versões históricas.
WITH latest AS (
    SELECT DISTINCT ON (p.area_id) p.*
    FROM priority_assessments p
    ORDER BY p.area_id, p.calculated_at DESC, p.id DESC
)
SELECT a.name, p.score, dense_rank() OVER (ORDER BY p.score DESC) AS position,
       p.thermal_exposure, p.calculated_at, p.method_version,
       p.contains_simulated_inputs, p.explanation, p.recommendation
FROM latest p
JOIN areas a ON a.id = p.area_id AND a.active
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
WHERE u.id = :'manager_id'::uuid
ORDER BY position, a.name;

-- 5. Explicação dos critérios do último IPIU da área autorizada.
SELECT c.name, ps.normalized_value, ps.weight, ps.contribution,
       ps.source, ps.nature, ps.reference_at, ps.evidence
FROM areas a
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
JOIN LATERAL (
    SELECT p.id FROM priority_assessments p WHERE p.area_id = a.id
    ORDER BY p.calculated_at DESC, p.id DESC LIMIT 1
) latest ON true
JOIN priority_scores ps ON ps.assessment_id = latest.id
JOIN priority_criteria c ON c.id = ps.criterion_id
WHERE u.id = :'manager_id'::uuid AND a.id = :'area_id'::uuid
ORDER BY ps.contribution DESC, c.name;

-- 6. Alertas e regra aplicada no momento do disparo.
SELECT a.name AS area, l.name AS location, al.status, al.severity,
       al.message, al.opened_at, al.rule_metric, al.rule_comparison, al.rule_threshold,
       r.temperature_c, r.relative_humidity_percent, r.nature
FROM alerts al
JOIN locations l ON l.id = al.location_id
JOIN areas a ON a.id = l.area_id
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
JOIN sensor_readings r ON r.id = al.trigger_reading_id
WHERE u.id = :'manager_id'::uuid AND al.status <> 'resolved'
ORDER BY al.opened_at DESC;

-- 7. Histórico pessoal de alertas, incluindo o estado de leitura.
SELECT al.message, l.name, ua.received_at, ua.read_at, al.status
FROM user_alerts ua
JOIN users u ON u.id = ua.user_id AND u.active
JOIN alerts al ON al.id = ua.alert_id
JOIN locations l ON l.id = al.location_id
WHERE u.id = :'citizen_id'::uuid
ORDER BY ua.received_at DESC;

-- 8. Grafo da área autorizada e custo com peso de calor 2.
SELECT e.id, n1.name AS origin, n2.name AS destination,
       e.from_node_id, e.to_node_id, e.distance_m, e.shade_fraction,
       e.thermal_exposure, round(e.distance_m * (1 + 2 * e.thermal_exposure), 2) AS thermal_cost,
       e.source, e.nature, e.reference_at
FROM route_edges e
JOIN route_nodes n1 ON n1.id = e.from_node_id
JOIN route_nodes n2 ON n2.id = e.to_node_id
JOIN areas a ON a.id = e.area_id
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
WHERE u.id = :'manager_id'::uuid AND a.id = :'area_id'::uuid AND e.active
ORDER BY e.from_node_id, e.to_node_id;

-- 9. Comparação de cenários com a avaliação que serviu de base.
SELECT a.name AS area, sc.name AS scenario, p.score AS baseline_score,
       sc.simulated_score, sc.simulated_score - p.score AS score_difference,
       sc.assumptions, sc.method_version, author.name AS created_by
FROM intervention_scenarios sc
JOIN priority_assessments p ON p.id = sc.baseline_assessment_id
JOIN areas a ON a.id = sc.area_id
JOIN organizations o ON o.id = a.organization_id AND o.active
JOIN organization_users ou ON ou.organization_id = o.id AND ou.active
JOIN users u ON u.id = ou.user_id AND u.active AND u.role = 1
JOIN users author ON author.id = sc.created_by_user_id
WHERE u.id = :'manager_id'::uuid
ORDER BY sc.created_at DESC;

-- 10. Sessões utilizáveis. A aplicação também verifica User.Active.
SELECT rt.id, rt.family_id, rt.expires_at
FROM refresh_tokens rt JOIN users u ON u.id = rt.user_id
WHERE u.id = :'citizen_id'::uuid AND u.active
  AND rt.revoked_at IS NULL AND rt.expires_at > now()
ORDER BY rt.expires_at;

