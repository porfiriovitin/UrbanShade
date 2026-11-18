package app.urbanshade.data

data class AlertItem(
    val id: String,
    val locationId: String,
    val dateIso: String,
    val message: String
)

object ClimateAlerts {

    private val allAlertsList: List<AlertItem> by lazy {
        val jardim = ClimateFixtures.JardimLocation
        val confianca = ClimateFixtures.ConfiancaLocation

        val jardim26 = jardim.dailyRecords.first { it.dateIso == "2026-12-26" }
        val confianca26 = confianca.dailyRecords.first { it.dateIso == "2026-12-26" }

        val jardim25 = jardim.dailyRecords.first { it.dateIso == "2026-12-25" }
        val confianca25 = confianca.dailyRecords.first { it.dateIso == "2026-12-25" }

        val jardim24 = jardim.dailyRecords.first { it.dateIso == "2026-12-24" }

        listOf(
            // 2026-12-26: Jardim das Nações (4)
            AlertItem(
                id = "alert_jardim_2026-12-26_heat",
                locationId = jardim.id,
                dateIso = "2026-12-26",
                message = "Temperatura acima de 39 °C no ${jardim.name}. Grupos de risco devem evitar exposição ao calor intenso."
            ),
            AlertItem(
                id = "alert_jardim_2026-12-26_seniors",
                locationId = jardim.id,
                dateIso = "2026-12-26",
                message = "${jardim.name}: ${ClimateFixtures.formatInteger(jardim.seniorPopulation)} pessoas com 65 anos ou mais estão entre os ${ClimateFixtures.formatPopulationShort(jardim.impactedPopulation)} moradores expostos ao calor no período monitorado."
            ),
            AlertItem(
                id = "alert_jardim_2026-12-26_humidity",
                locationId = jardim.id,
                dateIso = "2026-12-26",
                message = "Umidade mínima de ${jardim26.minHumidity}% no ${jardim.name} acompanha o pico de calor de ${ClimateFixtures.formatTemperature(jardim26.maxTemperature)}."
            ),
            AlertItem(
                id = "alert_jardim_2026-12-26_trees",
                locationId = jardim.id,
                dateIso = "2026-12-26",
                message = "Cobertura arbórea de apenas ${jardim.currentTreeCoverPercent}% no ${jardim.name}. Trajetos com pouca sombra são prioridade de intervenção."
            ),
            // 2026-12-26: Bairro da Confiança (2)
            AlertItem(
                id = "alert_confianca_2026-12-26_heat",
                locationId = confianca.id,
                dateIso = "2026-12-26",
                message = "${confianca.name} registra ${ClimateFixtures.formatTemperature(confianca26.maxTemperature)}, acima da meta térmica de 36 °C. A região tem ${ClimateFixtures.formatInteger(confianca.seniorPopulation)} pessoas com 65 anos ou mais na população impactada."
            ),
            AlertItem(
                id = "alert_confianca_2026-12-26_soil",
                locationId = confianca.id,
                dateIso = "2026-12-26",
                message = "Solo ${confianca.imperviousSoilPercent}% impermeável e cobertura arbórea de ${confianca.currentTreeCoverPercent}% contribuem para o aquecimento no ${confianca.name}."
            ),

            // 2026-12-25: Jardim das Nações (2)
            AlertItem(
                id = "alert_jardim_2026-12-25_heat",
                locationId = jardim.id,
                dateIso = "2026-12-25",
                message = "${jardim.name} registrou pico de ${ClimateFixtures.formatTemperature(jardim25.maxTemperature)}, acima da meta térmica de 36 °C. Grupos de risco devem evitar exposição ao calor intenso."
            ),
            AlertItem(
                id = "alert_jardim_2026-12-25_humidity",
                locationId = jardim.id,
                dateIso = "2026-12-25",
                message = "Umidade mínima de ${jardim25.minHumidity}% no ${jardim.name} acompanha o pico de temperatura de ${ClimateFixtures.formatTemperature(jardim25.maxTemperature)}."
            ),
            // 2026-12-25: Bairro da Confiança (1)
            AlertItem(
                id = "alert_confianca_2026-12-25_heat",
                locationId = confianca.id,
                dateIso = "2026-12-25",
                message = "${confianca.name} registrou pico de ${ClimateFixtures.formatTemperature(confianca25.maxTemperature)}, acima da meta térmica de 36 °C. Grupos de risco devem evitar exposição ao calor intenso."
            ),

            // 2026-12-24: Jardim das Nações (2)
            AlertItem(
                id = "alert_jardim_2026-12-24_heat",
                locationId = jardim.id,
                dateIso = "2026-12-24",
                message = "${jardim.name} registrou pico de ${ClimateFixtures.formatTemperature(jardim24.maxTemperature)}, acima da meta térmica de 36 °C. Grupos de risco devem evitar exposição ao calor intenso."
            ),
            AlertItem(
                id = "alert_jardim_2026-12-24_humidity",
                locationId = jardim.id,
                dateIso = "2026-12-24",
                message = "Umidade mínima de ${jardim24.minHumidity}% no ${jardim.name} acompanha o pico de temperatura de ${ClimateFixtures.formatTemperature(jardim24.maxTemperature)}."
            )
        )
    }

    fun getAllAlerts(): List<AlertItem> = allAlertsList

    fun getAlertsFor(locationId: String, dateIso: String): List<AlertItem> {
        return allAlertsList.filter { it.locationId == locationId && it.dateIso == dateIso }
    }

    fun formatDateHeader(dateIso: String): String {
        val parts = dateIso.split("-")
        return if (parts.size == 3) "${parts[2]}/${parts[1]}/${parts[0]}" else dateIso
    }
}

fun buildClimateAlerts(): List<AlertItem> = ClimateAlerts.getAllAlerts()
