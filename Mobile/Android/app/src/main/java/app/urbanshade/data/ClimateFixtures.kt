package app.urbanshade.data

import java.text.NumberFormat
import java.util.Locale
import kotlin.math.abs
import kotlin.math.roundToInt

/**
 * Single source of truth for climate monitoring fixtures, daily series,
 * and afforestation proposals across Local Details, Smart Analysis, Alerts, and Map.
 *
 * Fully decoupled from Compose UI; values are formatted for Brazilian Portuguese.
 */

const val ThermalTargetCelsius = 36.0

data class HourlyClimateSample(
    val hourLabel: String,
    val currentTemperature: Double,
    val humidityPercent: Int,
    val proposedTemperature: Double
)

data class DailyClimateRecord(
    val dateIso: String,
    val dayMonth: String,
    val fullDateFormatted: String,
    val samples: List<HourlyClimateSample>,
    val peakReductionCelsius: Double
) {
    val maxTemperature: Double = samples.maxOf { it.currentTemperature }
    val minHumidity: Int = samples.minOf { it.humidityPercent }
    val differenceFromTarget: Double = maxTemperature - ThermalTargetCelsius
    val isAboveTarget: Boolean = maxTemperature > ThermalTargetCelsius
    val averageTemperature: Double = samples.map { it.currentTemperature }.average()
    val proposedMaxTemperature: Double = maxTemperature - peakReductionCelsius
}

data class LocationClimateData(
    val id: String,
    val name: String,
    val shortName: String,
    val currentTreeCoverPercent: Int,
    val proposedTreeCoverPercent: Int,
    val imperviousSoilPercent: Int,
    val impactedPopulation: Int,
    val seniorPopulation: Int,
    val peakReductionCelsius: Double,
    val dailyRecords: List<DailyClimateRecord>
) {
    val daysAboveTargetCount: Int = dailyRecords.count { it.isAboveTarget }
    val totalDaysCount: Int = dailyRecords.size
}

data class SummarySections(
    val thermalStatement: String,
    val humidityStatement: String,
    val urbanStatement: String,
    val populationStatement: String
)

data class SmartAnalysisSections(
    val diagnosisStatement: String,
    val proposalStatement: String,
    val reductionStatement: String
)

object ClimateFixtures {

    private val ptBrLocale = Locale.forLanguageTag("pt-BR")
    private val intFormatter = NumberFormat.getIntegerInstance(ptBrLocale)

    private val hours = listOf("06h", "09h", "12h", "15h", "18h")

    // Base series for December 26, 2026
    private val jardimBaseTemps = listOf(27.0, 32.4, 37.4, 39.2, 34.0)
    private val jardimBaseHumidity = listOf(60, 43, 30, 24, 39)
    private const val JardimReduction = 3.8

    private val confiancaBaseTemps = listOf(26.0, 31.0, 35.2, 38.0, 32.8)
    private val confiancaBaseHumidity = listOf(65, 49, 36, 30, 44)
    private const val ConfiancaReduction = 2.8

    // Offsets per day relative to 26/12
    private data class DayOffset(
        val dateIso: String,
        val dayMonth: String,
        val fullDate: String,
        val jardimTempOffset: Double,
        val confiancaTempOffset: Double,
        val humidityOffset: Int
    )

    private val offsets = listOf(
        DayOffset("2026-12-26", "26/12", "26/12/2026", 0.0, 0.0, 0),
        DayOffset("2026-12-25", "25/12", "25/12/2026", -1.4, -0.7, 3),
        DayOffset("2026-12-24", "24/12", "24/12/2026", -0.8, -2.5, 1),
        DayOffset("2026-12-23", "23/12", "23/12/2026", -4.1, -3.4, 8)
    )

    val JardimLocation: LocationClimateData by lazy {
        val records = offsets.map { offset ->
            val samples = hours.indices.map { i ->
                val temp = roundOneDecimal(jardimBaseTemps[i] + offset.jardimTempOffset)
                val hum = jardimBaseHumidity[i] + offset.humidityOffset
                val proposed = roundOneDecimal(temp - JardimReduction)
                HourlyClimateSample(hours[i], temp, hum, proposed)
            }
            DailyClimateRecord(
                dateIso = offset.dateIso,
                dayMonth = offset.dayMonth,
                fullDateFormatted = offset.fullDate,
                samples = samples,
                peakReductionCelsius = JardimReduction
            )
        }
        LocationClimateData(
            id = "jardim",
            name = "Jardim das Nações",
            shortName = "Jardim das Nações",
            currentTreeCoverPercent = 8,
            proposedTreeCoverPercent = 22,
            imperviousSoilPercent = 78,
            impactedPopulation = 10000,
            seniorPopulation = 1200,
            peakReductionCelsius = JardimReduction,
            dailyRecords = records
        )
    }

    val ConfiancaLocation: LocationClimateData by lazy {
        val records = offsets.map { offset ->
            val samples = hours.indices.map { i ->
                val temp = roundOneDecimal(confiancaBaseTemps[i] + offset.confiancaTempOffset)
                val hum = confiancaBaseHumidity[i] + offset.humidityOffset
                val proposed = roundOneDecimal(temp - ConfiancaReduction)
                HourlyClimateSample(hours[i], temp, hum, proposed)
            }
            DailyClimateRecord(
                dateIso = offset.dateIso,
                dayMonth = offset.dayMonth,
                fullDateFormatted = offset.fullDate,
                samples = samples,
                peakReductionCelsius = ConfiancaReduction
            )
        }
        LocationClimateData(
            id = "confianca",
            name = "Bairro da Confiança",
            shortName = "B. da Confiança",
            currentTreeCoverPercent = 14,
            proposedTreeCoverPercent = 26,
            imperviousSoilPercent = 72,
            impactedPopulation = 8000,
            seniorPopulation = 850,
            peakReductionCelsius = ConfiancaReduction,
            dailyRecords = records
        )
    }

    private val locationsById by lazy {
        listOf(JardimLocation, ConfiancaLocation).associateBy { it.id }
    }

    fun getLocation(id: String): LocationClimateData {
        return locationsById[id] ?: JardimLocation
    }

    fun getAllLocations(): List<LocationClimateData> {
        return listOf(JardimLocation, ConfiancaLocation)
    }

    fun formatTemperature(value: Double): String {
        return String.format(ptBrLocale, "%.1f °C", value)
    }

    fun formatDifferenceFromTarget(diff: Double): String {
        return if (diff > 0.0) {
            String.format(ptBrLocale, "+%.1f °C", diff)
        } else if (diff < 0.0) {
            String.format(ptBrLocale, "-%.1f °C", abs(diff))
        } else {
            "0,0 °C"
        }
    }

    fun formatInteger(value: Int): String {
        return intFormatter.format(value)
    }

    fun formatPopulationShort(value: Int): String {
        return if (value >= 1000 && value % 1000 == 0) {
            "${value / 1000} mil"
        } else {
            intFormatter.format(value)
        }
    }

    /**
     * Decomposes the summary into structured thematic sections for elegant presentation.
     */
    fun getSummarySections(location: LocationClimateData, record: DailyClimateRecord): SummarySections {
        val peakFormatted = formatTemperature(record.maxTemperature)
        val diffAbsFormatted = formatTemperature(abs(record.differenceFromTarget))

        val thermalClause = when {
            record.maxTemperature > ThermalTargetCelsius ->
                "ultrapassando a meta térmica em $diffAbsFormatted"
            record.maxTemperature < ThermalTargetCelsius ->
                "ficando $diffAbsFormatted abaixo da meta térmica"
            else ->
                "atingindo a meta térmica"
        }

        val thermalStatement = "O ${location.name} registrou pico de $peakFormatted, $thermalClause."
        val humidityStatement = "A umidade mínima do dia foi de ${record.minHumidity}%."

        val locationClause = if (location.id == "jardim") {
            "A cobertura arbórea de apenas ${location.currentTreeCoverPercent}% e o solo ${location.imperviousSoilPercent}% impermeável contribuem para o aquecimento e a pouca sombra."
        } else {
            "A cobertura arbórea de ${location.currentTreeCoverPercent}% e o solo ${location.imperviousSoilPercent}% impermeável deixam áreas residenciais e trajetos de pedestres expostos ao calor."
        }

        val popShort = formatPopulationShort(location.impactedPopulation)
        val seniorFormatted = formatInteger(location.seniorPopulation)
        val populationClause = "No período monitorado, $popShort pessoas foram impactadas, incluindo $seniorFormatted com 65 anos ou mais."

        return SummarySections(
            thermalStatement = thermalStatement,
            humidityStatement = humidityStatement,
            urbanStatement = locationClause,
            populationStatement = populationClause
        )
    }

    /**
     * Builds summary text for History and Smart Analysis tabs following exact specification rules.
     */
    fun buildSummaryText(location: LocationClimateData, record: DailyClimateRecord): String {
        val s = getSummarySections(location, record)
        return "${s.thermalStatement} ${s.urbanStatement} ${s.populationStatement} ${s.humidityStatement}"
    }

    /**
     * Decomposes smart analysis into diagnosis, proposal, and estimated reduction sections.
     */
    fun getSmartAnalysisSections(location: LocationClimateData, record: DailyClimateRecord): SmartAnalysisSections {
        val currentPeak = formatTemperature(record.maxTemperature)
        val proposedPeak = formatTemperature(record.proposedMaxTemperature)
        val reduction = formatTemperature(location.peakReductionCelsius)

        val targetClause = if (record.maxTemperature > ThermalTargetCelsius) {
            "atingindo a meta térmica de 36 °C"
        } else {
            "ampliando a margem em relação à meta térmica de 36 °C"
        }

        val diagnosis = if (location.id == "jardim") {
            "A baixa cobertura arbórea e a elevada impermeabilização favorecem o aquecimento e reduzem a disponibilidade de sombra."
        } else {
            "A pouca sombra nos trajetos de pedestres e a elevada proporção de solo impermeável contribuem para o calor na região."
        }

        val proposal = if (location.id == "jardim") {
            "A proposta prioriza o plantio de árvores nas calçadas e nos trajetos mais expostos, ampliando a cobertura arbórea de ${location.currentTreeCoverPercent}% para ${location.proposedTreeCoverPercent}%."
        } else {
            "A proposta amplia a cobertura arbórea de ${location.currentTreeCoverPercent}% para ${location.proposedTreeCoverPercent}%, priorizando calçadas e áreas residenciais mais expostas."
        }

        val reductionStatement = if (location.id == "jardim") {
            "Com essa intervenção, estima-se uma redução de $reduction no pico de temperatura, de $currentPeak para $proposedPeak, $targetClause."
        } else {
            "A redução estimada de $reduction levaria o pico de $currentPeak para $proposedPeak, $targetClause."
        }

        return SmartAnalysisSections(diagnosis, proposal, reductionStatement)
    }

    /**
     * Builds diagnosis and proposal text for Smart Analysis following exact specification rules.
     */
    fun buildSmartAnalysisText(location: LocationClimateData, record: DailyClimateRecord): String {
        val s = getSmartAnalysisSections(location, record)
        return "${s.diagnosisStatement} ${s.proposalStatement} ${s.reductionStatement}"
    }

    private fun roundOneDecimal(value: Double): Double {
        return (value * 10.0).roundToInt() / 10.0
    }
}
