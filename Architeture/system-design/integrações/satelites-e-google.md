# Satélites e serviços Google

Para complementar o mapa, foram levantadas fontes de vegetação e temperatura da superfície com acesso gratuito. A proposta é preparar os dados de uma área pequena e importar os resultados no banco do UrbanShade.

As condições de acesso foram consultadas em 09/11/2026. Os exemplos ainda precisam ser testados; o processamento das imagens não foi implementado.

## Opções de acesso

| Serviço | Acesso | Uso no projeto |
|---|---|---|
| Microsoft Planetary Computer | Catálogo público sem conta; maioria dos dados disponível anonimamente, com limites de tráfego | Buscar imagens Landsat e Sentinel-2 e processar o recorte localmente |
| Google Earth Engine | Gratuito para uso acadêmico elegível, com registro e verificação | Calcular indicadores na plataforma e exportar os resultados |
| Copernicus Data Space | Dados Sentinel gratuitos; autenticação e quotas variam por operação | Alternativa para obter imagens Sentinel |
| Google Maps SDK Android | Exige chave e faturamento habilitado; gratuidade depende do SKU | Exibir o mapa e as camadas produzidas pelo UrbanShade |

O Planetary Computer é a primeira opção para obter dados sem cadastro. O Earth Engine facilita o processamento, mas exige a configuração de um projeto Google Cloud. A escolha entre os dois continua em aberto.

## Microsoft Planetary Computer

O catálogo usa STAC, um padrão para descrever imagens e seus arquivos. A busca retorna metadados e links; o cálculo dos indicadores fica por conta do projeto.

- Catálogo: https://planetarycomputer.microsoft.com/api/stac/v1/
- Busca: POST https://planetarycomputer.microsoft.com/api/stac/v1/search
- Coleções: sentinel-2-l2a e landsat-c2-l2

Exemplo de busca para um recorte de São Paulo:

~~~json
{
  "collections": ["sentinel-2-l2a"],
  "bbox": [-46.66, -23.57, -46.62, -23.53],
  "datetime": "2026-01-01T00:00:00Z/2026-10-31T23:59:59Z",
  "limit": 10,
  "query": {"eo:cloud_cover": {"lt": 20}}
}
~~~

Será necessário seguir a paginação e verificar a presença de nuvens dentro da área escolhida. A porcentagem de nuvens da imagem inteira serve apenas como filtro inicial.

Alguns arquivos precisam de URLs com assinatura temporária, geradas para acesso aos dados. O tratamento pode ser feito em Python com pystac-client, planetary-computer, rasterio e geopandas: buscar as imagens, recortar a área, aplicar as correções do produto e salvar as estatísticas.

- [Documentação](https://planetarycomputer.microsoft.com/docs/)
- [Exemplo de acesso e assinatura dos arquivos](https://planetarycomputer.microsoft.com/docs/quickstarts/reading-stac-r/)

## Google Earth Engine

O Earth Engine reúne as imagens e as ferramentas para processá-las. Para o projeto, ele pode calcular os indicadores por área e exportar arquivos para importação no backend.

O acesso acadêmico gratuito depende de elegibilidade e verificação. São necessários uma conta Google, um projeto Cloud registrado, a API habilitada e as permissões de acesso.

- [Uso não comercial](https://earthengine.google.com/noncommercial/)
- [Configuração do acesso](https://developers.google.com/earth-engine/guides/access)

## Vegetação com Sentinel-2

O Sentinel-2 permite calcular o NDVI a partir das bandas vermelha e infravermelho próximo:

NDVI = (infravermelho próximo − vermelho) / (infravermelho próximo + vermelho)

Na coleção COPERNICUS/S2_SR_HARMONIZED do Earth Engine, as bandas B4 e B8 têm pixels de 10 m. O cálculo precisa tratar nuvens, pixels inválidos e denominador zero. Os fatores de correção e os nomes das bandas devem ser conferidos no produto utilizado.

O NDVI pode entrar no IPIU como indicador de vegetação da área. Ele não corresponde diretamente à porcentagem de árvores ou à sombra de uma calçada. O Sentinel-2 também não possui a medição térmica usada pelo Landsat.

- [Sentinel-2 no Earth Engine](https://developers.google.com/earth-engine/datasets/catalog/COPERNICUS_S2_SR_HARMONIZED)

## Cobertura do solo com Dynamic World

A coleção GOOGLE/DYNAMICWORLD/V1 classifica o solo em nove categorias, com pixels de 10 m. Entre elas estão árvores, vegetação rasteira e área construída.

Ela pode ajudar a calcular a proporção de cobertura arbórea por área, desde que sejam definidos o critério de classificação e o limite de confiança. A probabilidade da classe trees, isoladamente, não é uma porcentagem de cobertura.

- [Dynamic World](https://developers.google.com/earth-engine/datasets/catalog/GOOGLE_DYNAMICWORLD_V1)

## Temperatura da superfície com Landsat

O produto Landsat Collection 2 Level-2 Surface Temperature permite comparar superfícies mais quentes e mais frias na data da imagem.

No Earth Engine, uma das coleções é LANDSAT/LC08/C02/T1_L2. Para usar a banda ST_B10, serão selecionadas imagens L2SP com dados térmicos válidos. A conversão dos valores brutos dessa banda é:

Temperatura em °C = valor bruto × 0.00341802 + 149 − 273.15

Esse fator é específico do produto e não deve ser reaplicado a valores já convertidos. O tratamento inclui máscaras de nuvens, sombras e pixels inválidos.

A camada terá data de aquisição, resolução e proporção de pixels válidos. A grade entregue de 30 m não garante precisão térmica por calçada. Como as imagens dependem das passagens do satélite e das condições de nuvens, essa camada será usada como referência histórica.

A temperatura da superfície será mantida separada da temperatura do ar apresentada pela Open-Meteo e pelos sensores simulados.

- [Coleção Landsat e fatores de conversão](https://developers.google.com/earth-engine/datasets/catalog/LANDSAT_LC08_C02_T1_L2)
- [Temperatura da superfície — USGS](https://www.usgs.gov/landsat-missions/landsat-surface-temperature)

## Copernicus Data Space

É uma alternativa oficial para acessar dados Sentinel. O catálogo fica em https://stac.dataspace.copernicus.eu/v1/ e inclui a coleção sentinel-2-l2a.

Pesquisa, download e processamento têm condições próprias. Serviços como openEO e Sentinel Hub possuem franquias de processamento; o acesso gratuito não é ilimitado.

- [Catálogo STAC](https://documentation.dataspace.copernicus.eu/APIs/STAC.html)
- [APIs disponíveis](https://documentation.dataspace.copernicus.eu/APIs.html)
- [Condições de acesso](https://documentation.dataspace.copernicus.eu/FAQ.html)

## Google Maps e Google Earth

O Maps SDK Android pode exibir os marcadores, as rotas e as camadas térmicas calculadas pelo UrbanShade. Seu uso exige faturamento habilitado e chave ou token. Places, Routes e outros serviços têm cobrança e franquias próprias.

Para analisar vegetação e temperatura, a opção do Google é o Earth Engine. As imagens exibidas no Google Maps ou no Google Earth não substituem os produtos científicos descritos neste documento.

- [Maps Android: uso e faturamento](https://developers.google.com/maps/documentation/android-sdk/usage-and-billing)
- [Preços e franquias](https://developers.google.com/maps/billing-and-pricing/overview)
- [Earth Engine](https://earthengine.google.com/)

## Preparação dos dados

Depois de escolher a região e a plataforma de acesso, o processamento deve gerar os indicadores por área, acompanhados de fonte, data da imagem, método e cobertura válida. Esses resultados serão importados como estimativas derivadas de satélite e poderão compor o IPIU.

Áreas sem pixels válidos ficarão sem valor. Sombra por horário, circulação de pedestres e vulnerabilidade social continuarão dependendo de outras fontes ou de cenários simulados identificados.

No AquaGuard, o MockSatelliteService simula a detecção de água usando outros dados. A integração com imagens reais acrescenta uma etapa de preparação que não existia naquele fluxo.

[Mapas e dados urbanos](mapas-e-dados-urbanos.md)
