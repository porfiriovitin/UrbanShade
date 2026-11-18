package app.urbanshade.ui.localdetails

import androidx.compose.foundation.BorderStroke
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.urbanshade.R
import app.urbanshade.data.ClimateAlerts
import app.urbanshade.data.ClimateFixtures
import app.urbanshade.data.DailyClimateRecord
import app.urbanshade.data.LocationClimateData
import app.urbanshade.data.ThermalTargetCelsius
import app.urbanshade.ui.components.*
import app.urbanshade.ui.map.MapPlace
import app.urbanshade.ui.map.demoMapPlaces
import app.urbanshade.ui.theme.UrbanHeat
import app.urbanshade.ui.theme.UrbanMotion
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSpacing

@Composable
internal fun LocalDetailsScreen(
    place: MapPlace,
    modifier: Modifier = Modifier,
    onBack: () -> Unit = {},
    onNavigate: (NavigationDestination) -> Unit = {},
    initialAnalysis: Boolean = false
) {
    val location = remember(place.id) { ClimateFixtures.getLocation(place.id) }
    var analysis by rememberSaveable(place.id) { mutableStateOf(initialAnalysis) }
    var selectedDateIso by rememberSaveable(place.id) { mutableStateOf("2026-12-26") }
    val snapshotState = rememberSaveableStateHolder()

    val record = location.dailyRecords.firstOrNull { it.dateIso == selectedDateIso }
        ?: location.dailyRecords.first()

    val historyScroll = rememberScrollState()
    val analysisScroll = rememberScrollState()
    val colors = MaterialTheme.colorScheme

    Column(modifier.fillMaxSize().background(colors.primary)) {
        PageHeader(
            title = place.title,
            onBack = onBack
        )
        Column(
            Modifier
                .weight(1f)
                .fillMaxWidth()
                .clip(RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))
                .background(colors.background, RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))
                .verticalScroll(if (analysis) analysisScroll else historyScroll)
                .padding(UrbanSpacing.Page),
            verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Section)
        ) {
            DetailsViewSelector(analysis, onSelected = { analysis = it })

            AnimatedContent(targetState = analysis, transitionSpec = {
                (fadeIn(tween(UrbanMotion.Transition)) togetherWith fadeOut(tween(UrbanMotion.Transition))).using(null)
            }, label = "Details tab") { showAnalysis ->
                Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Section)) {
                    if (showAnalysis) {
                        // Smart Analysis tab
                        SmartAnalysisCard(location, record)
                        snapshotState.SaveableStateProvider("${place.id}/analysis") {
                            ClimateSnapshot(location, record, proposed = true)
                        }
                        SmartAnalysisThermalComparisonChart(location, record)
                        HistoryHumidityChart(record)
                    } else {
                        // History tab
                        HistoryDateSelector(
                            selectedIso = record.dateIso,
                            records = location.dailyRecords,
                            onSelected = { selectedDateIso = it }
                        )
                        LocalSummaryCard(location, record)
                        HistoryIndicators(location, record)
                        snapshotState.SaveableStateProvider("${place.id}/history") {
                            ClimateSnapshot(location, record, proposed = false)
                        }
                        HistoryTemperatureChart(record)
                        HistoryHumidityChart(record)

                        val dayAlerts = remember(location.id, record.dateIso) {
                            ClimateAlerts.getAlertsFor(location.id, record.dateIso)
                        }
                        if (dayAlerts.isNotEmpty()) {
                            Column(verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
                                Text(
                                    text = stringResource(R.string.local_alerts_emitted_title, record.dayMonth),
                                    style = MaterialTheme.typography.titleMedium,
                                    color = colors.onSurface,
                                    modifier = Modifier.semantics { heading() }
                                )
                                dayAlerts.forEach { alert ->
                                    AlertCard(alert.message)
                                }
                            }
                        }
                    }
                }
            }
        }
        UrbanShadeNavigationBar(selected = NavigationDestination.Map, onNavigate = onNavigate)
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DetailsViewSelector(analysis: Boolean, onSelected: (Boolean) -> Unit) {
    val colors = MaterialTheme.colorScheme
    SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth().heightIn(min = 56.dp), space = 0.dp) {
        listOf(R.string.local_history, R.string.local_smart_analysis).forEachIndexed { index, label ->
            val selected = analysis == (index == 1)
            val container by animateColorAsState(if (selected) colors.primary else colors.background,
                tween(UrbanMotion.Selection), label = "Tab background")
            val content by animateColorAsState(if (selected) colors.onPrimary else colors.onSurfaceVariant,
                tween(UrbanMotion.Selection), label = "Tab content")
            SegmentedButton(
                selected = selected,
                onClick = { onSelected(index == 1) },
                shape = SegmentedButtonDefaults.itemShape(index, 2, RoundedCornerShape(16.dp)),
                border = BorderStroke(0.dp, Color.Transparent),
                icon = {},
                colors = SegmentedButtonDefaults.colors(
                    activeContainerColor = container,
                    activeContentColor = content,
                    inactiveContainerColor = container,
                    inactiveContentColor = content
                ),
                modifier = Modifier.weight(1f).heightIn(min = 56.dp)
            ) {
                Text(stringResource(label), style = MaterialTheme.typography.titleMedium.copy(fontSize = 16.sp, lineHeight = 20.sp),
                    maxLines = 2, textAlign = TextAlign.Center)
            }
        }
    }
}

@Composable
private fun HistoryDateSelector(
    selectedIso: String,
    records: List<DailyClimateRecord>,
    onSelected: (String) -> Unit
) {
    val colors = MaterialTheme.colorScheme
    Row(
        Modifier
            .fillMaxWidth()
            .height(50.dp)
            .padding(bottom = 2.dp)
            .horizontalScroll(rememberScrollState())
            .selectableGroup(),
        horizontalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterHorizontally),
        verticalAlignment = Alignment.CenterVertically
    ) {
        records.forEach { record ->
            val isSelected = record.dateIso == selectedIso
            val container by animateColorAsState(if (isSelected) colors.primary else colors.background,
                tween(UrbanMotion.Selection), label = "Date background")
            val content by animateColorAsState(if (isSelected) colors.onPrimary else colors.onSurfaceVariant,
                tween(UrbanMotion.Selection), label = "Date content")
            Surface(
                selected = isSelected,
                onClick = { onSelected(record.dateIso) },
                modifier = Modifier.width(70.dp).height(40.dp),
                shape = RoundedCornerShape(8.dp),
                color = container,
                contentColor = content,
                border = if (isSelected) null else BorderStroke(1.dp, colors.onSurfaceVariant),
                shadowElevation = if (isSelected) 3.dp else 0.dp
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(record.dayMonth, style = MaterialTheme.typography.labelLarge)
                }
            }
        }
    }
}

/**
 * Shared detail paragraphs use the same readable sizing in History and Analysis.
 */
@Composable
private fun DetailTextBlock(
    title: String,
    body: String,
    modifier: Modifier = Modifier,
    bodyStyle: TextStyle = MaterialTheme.typography.bodyLarge.copy(fontSize = 18.sp, lineHeight = 28.sp)
) {
    val colors = MaterialTheme.colorScheme
    Column(
        modifier = modifier,
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium,
            color = colors.onSurface,
            modifier = Modifier.semantics { heading() }
        )
        Text(
            text = body,
            style = bodyStyle,
            color = colors.onSurfaceVariant
        )
    }
}

/**
 * Formats the monitoring summary into clear, digestible thematic sections without a general title.
 */
@Composable
private fun LocalSummaryCard(location: LocationClimateData, record: DailyClimateRecord) {
    val colors = MaterialTheme.colorScheme
    val sections = remember(location.id, record.dateIso) {
        ClimateFixtures.getSummarySections(location, record)
    }

    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(colors.surface, RoundedCornerShape(16.dp))
            .padding(UrbanSpacing.Medium),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        // 1. Comportamento térmico
        DetailTextBlock(
            title = stringResource(R.string.local_summary_thermal),
            body = "${sections.thermalStatement} ${sections.humidityStatement}"
        )

        HorizontalDivider(color = colors.outline.copy(alpha = 0.25f))

        // 2. Condições urbanas e sombra
        DetailTextBlock(
            title = stringResource(R.string.local_summary_urban),
            body = sections.urbanStatement
        )

        HorizontalDivider(color = colors.outline.copy(alpha = 0.25f))

        // 3. População monitorada
        DetailTextBlock(
            title = stringResource(R.string.local_summary_population),
            body = sections.populationStatement
        )
    }
}

@Composable
private fun SmartAnalysisCard(location: LocationClimateData, record: DailyClimateRecord) {
    val colors = MaterialTheme.colorScheme
    val paragraphStyle = MaterialTheme.typography.bodyLarge.copy(fontSize = 18.sp, lineHeight = 28.sp)
    val sections = remember(location.id, record.dateIso) {
        ClimateFixtures.getSmartAnalysisSections(location, record)
    }

    DetailTextBlock(
        title = stringResource(R.string.local_diagnosis_territorial),
        body = sections.diagnosisStatement,
        modifier = Modifier.fillMaxWidth().background(colors.surface, RoundedCornerShape(16.dp))
            .padding(UrbanSpacing.Medium),
        bodyStyle = paragraphStyle
    )

    Column(Modifier.fillMaxWidth().background(colors.surface, RoundedCornerShape(16.dp))
        .padding(UrbanSpacing.Medium), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
        Text(stringResource(R.string.local_proposal_priority), style = MaterialTheme.typography.titleMedium,
            color = colors.primary, modifier = Modifier.semantics { heading() })
        Text(stringResource(R.string.analysis_proposal_title), style = MaterialTheme.typography.titleLarge,
            color = colors.onSurface)
        val emphasis = stringResource(R.string.analysis_proposal_emphasis)
        Text(buildAnnotatedString {
            append(sections.proposalStatement)
            val start = sections.proposalStatement.indexOf(emphasis)
            if (start >= 0) {
                addStyle(SpanStyle(fontWeight = FontWeight.Bold, color = colors.onSurface), start, start + emphasis.length)
            }
        }, style = paragraphStyle, color = colors.onSurfaceVariant)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            AnalysisCoverageIndicator(stringResource(R.string.analysis_current_cover), location.currentTreeCoverPercent,
                colors.onSurface, Modifier.weight(1f))
            AnalysisCoverageIndicator(stringResource(R.string.chart_legend_planted), location.proposedTreeCoverPercent,
                colors.primary, Modifier.weight(1f))
        }
    }

    Column(Modifier.fillMaxWidth().background(colors.primary, RoundedCornerShape(16.dp))
        .padding(UrbanSpacing.Medium), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Medium),
            verticalAlignment = Alignment.CenterVertically) {
            Text("−${ClimateFixtures.formatTemperature(record.peakReductionCelsius)}",
                style = MaterialTheme.typography.headlineLarge, color = colors.onPrimary)
            Text(stringResource(R.string.analysis_peak_reduction), Modifier.weight(1f).semantics { heading() },
                style = MaterialTheme.typography.titleMedium, color = colors.onPrimary)
        }
        Text(stringResource(R.string.analysis_peak_comparison,
            ClimateFixtures.formatTemperature(record.maxTemperature),
            ClimateFixtures.formatTemperature(record.proposedMaxTemperature)),
            style = paragraphStyle, color = colors.onPrimary)
    }
}

@Composable
private fun AnalysisCoverageIndicator(label: String, percent: Int, valueColor: Color, modifier: Modifier) {
    val colors = MaterialTheme.colorScheme
    Column(modifier.padding(top = UrbanSpacing.Small),
        verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(label, style = MaterialTheme.typography.labelSmall, color = colors.onSurfaceVariant)
        Text("$percent%", style = MaterialTheme.typography.headlineMedium, color = valueColor)
    }
}

@Composable
private fun HistoryIndicators(location: LocationClimateData, record: DailyClimateRecord) {
    Column(verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            IndicatorCard(
                label = stringResource(R.string.local_max_temperature),
                value = ClimateFixtures.formatTemperature(record.maxTemperature),
                modifier = Modifier.weight(1f)
            )
            IndicatorCard(
                label = stringResource(R.string.indicator_diff_target),
                value = ClimateFixtures.formatDifferenceFromTarget(record.differenceFromTarget),
                modifier = Modifier.weight(1f)
            )
        }
        IndicatorCard(
            label = stringResource(R.string.indicator_min_humidity),
            value = "${record.minHumidity}%",
            modifier = Modifier.fillMaxWidth()
        )
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            IndicatorCard(
                label = stringResource(R.string.indicator_tree_cover),
                value = "${location.currentTreeCoverPercent}%",
                modifier = Modifier.weight(1f)
            )
            IndicatorCard(
                label = stringResource(R.string.map_impervious_surface),
                value = "${location.imperviousSoilPercent}%",
                modifier = Modifier.weight(1f)
            )
        }
        IndicatorCard(
            label = stringResource(R.string.indicator_population_period),
            value = ClimateFixtures.formatInteger(location.impactedPopulation),
            modifier = Modifier.fillMaxWidth(),
            singleLine = false
        )
        IndicatorCard(
            label = stringResource(R.string.indicator_seniors),
            value = ClimateFixtures.formatInteger(location.seniorPopulation),
            modifier = Modifier.fillMaxWidth(),
            singleLine = false
        )
        IndicatorCard(
            label = stringResource(R.string.indicator_days_above_target),
            value = "${location.daysAboveTargetCount} de ${location.totalDaysCount}",
            modifier = Modifier.fillMaxWidth(),
            singleLine = false
        )
    }
}

@Composable
private fun HistoryTemperatureChart(record: DailyClimateRecord) {
    val minSample = record.samples.minByOrNull { it.currentTemperature }
    val maxSample = record.samples.maxByOrNull { it.currentTemperature }
    val targetClause = if (record.isAboveTarget) {
        "ultrapassada em ${ClimateFixtures.formatTemperature(record.differenceFromTarget)}"
    } else {
        "atingida ou respeitada"
    }
    val description = "Gráfico de temperatura em ${record.fullDateFormatted}. Mínima de ${ClimateFixtures.formatTemperature(minSample?.currentTemperature ?: 0.0)} às ${minSample?.hourLabel}, máxima de ${ClimateFixtures.formatTemperature(record.maxTemperature)} às ${maxSample?.hourLabel}. Meta térmica de 36 °C $targetClause."

    HourlySeriesChart(
        title = stringResource(R.string.chart_temperature_title),
        hours = record.samples.map { it.hourLabel },
        primarySeries = record.samples.map { it.currentTemperature },
        primaryColor = MaterialTheme.colorScheme.primary,
        yMin = 18.0,
        yMax = 42.0,
        referenceValue = ThermalTargetCelsius,
        referenceLabel = "36 °C",
        unit = "°C",
        accessibleDescription = description
    )
}

@Composable
private fun HistoryHumidityChart(record: DailyClimateRecord) {
    val minSample = record.samples.minByOrNull { it.humidityPercent }
    val maxSample = record.samples.maxByOrNull { it.humidityPercent }
    val description = "Gráfico de umidade em ${record.fullDateFormatted}. Mínima de ${record.minHumidity}% às ${minSample?.hourLabel}, máxima de ${maxSample?.humidityPercent}% às ${maxSample?.hourLabel}."

    HourlySeriesChart(
        title = stringResource(R.string.chart_humidity_title),
        hours = record.samples.map { it.hourLabel },
        primarySeries = record.samples.map { it.humidityPercent.toDouble() },
        primaryColor = MaterialTheme.colorScheme.onSurfaceVariant,
        yMin = 0.0,
        yMax = 100.0,
        referenceValue = null,
        referenceLabel = null,
        unit = "%",
        accessibleDescription = description
    )
}

@Composable
private fun SmartAnalysisThermalComparisonChart(location: LocationClimateData, record: DailyClimateRecord) {
    val targetClause = if (record.maxTemperature > ThermalTargetCelsius) {
        "atingindo a meta térmica de 36 °C"
    } else {
        "ampliando a margem em relação à meta térmica de 36 °C"
    }
    val description = "Comparação térmica ao longo do dia em ${record.fullDateFormatted}. Cenário atual com pico de ${ClimateFixtures.formatTemperature(record.maxTemperature)}; com arborização pico de ${ClimateFixtures.formatTemperature(record.proposedMaxTemperature)}, redução estimada de ${ClimateFixtures.formatTemperature(location.peakReductionCelsius)}, $targetClause."

    HourlySeriesChart(
        title = stringResource(R.string.chart_smart_analysis_title),
        hours = record.samples.map { it.hourLabel },
        primarySeries = record.samples.map { it.currentTemperature },
        primaryColor = UrbanHeat,
        primaryName = stringResource(R.string.chart_legend_current),
        secondarySeries = record.samples.map { it.proposedTemperature },
        secondaryColor = MaterialTheme.colorScheme.primary,
        secondaryName = stringResource(R.string.chart_legend_planted),
        yMin = 18.0,
        yMax = 42.0,
        referenceValue = ThermalTargetCelsius,
        referenceLabel = "36 °C",
        unit = "°C",
        accessibleDescription = description
    )
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun HistoryPreview() {
    UrbanShadeTheme(true) { LocalDetailsScreen(demoMapPlaces()[1]) }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun SmartAnalysisPreview() {
    UrbanShadeTheme(true) { LocalDetailsScreen(demoMapPlaces()[1], initialAnalysis = true) }
}
