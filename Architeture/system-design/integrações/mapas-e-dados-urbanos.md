# Mapas e dados urbanos

O OpenStreetMap será a base para as ruas e os pontos de interesse da região de demonstração. Vegetação e temperatura da superfície podem ser acrescentadas com as fontes descritas em [Satélites e serviços Google](satelites-e-google.md).

## OpenStreetMap e Overpass

A Overpass API permite buscar elementos do OpenStreetMap por região e tags. A ideia é importar uma área pequena, revisar os dados e salvar o grafo no banco. O cálculo de rotas passa a usar essa base local.

Exemplo para o Overpass Turbo, considerando a área visível no editor:

~~~text
[out:json][timeout:25];
(
  way["highway"]({{bbox}});
  nwr["leisure"="park"]({{bbox}});
  nwr["amenity"~"^(school|hospital)$"]({{bbox}});
  nwr["highway"="bus_stop"]({{bbox}});
  nwr["public_transport"="platform"]({{bbox}});
);
out body;
>;
out skel qt;
~~~

A consulta ainda precisa ser testada. Em uma chamada HTTP direta, o marcador bbox deve ser substituído pelas coordenadas sul,oeste,norte,leste.

Antes de montar o grafo, será necessário revisar conexões, travessias, restrições de acesso e vias permitidas para pedestres. Serão preservados os IDs do OSM, as tags, as geometrias e a data da extração.

Os dados seguem a licença ODbL, com atribuição ao OpenStreetMap e obrigações específicas para distribuição de bases derivadas.

- [Overpass API](https://wiki.openstreetmap.org/wiki/Overpass_API)
- [Licença do OpenStreetMap](https://www.openstreetmap.org/copyright)

## Mapa no Android

A biblioteca do aplicativo e o provedor das imagens do mapa ainda serão escolhidos. As imagens de fundo, chamadas tiles, são independentes dos dados usados para calcular as rotas.

Os tiles públicos do OpenStreetMap exigem identificação da aplicação, atribuição e cache. O serviço proíbe download em massa e prefetch, portanto não será usado para preparar mapas offline.

No MVP, origem e destino poderão ser selecionados no mapa. Se houver busca por endereço com Nominatim público, o limite é de uma requisição por segundo para toda a aplicação, com identificação e cache. Autocomplete não é permitido.

- [Política de tiles](https://operations.osmfoundation.org/policies/tiles/)
- [Política do Nominatim](https://operations.osmfoundation.org/policies/nominatim/)

## Sombra e circulação de pedestres

Esses atributos serão cadastrados por trecho, com indicação da fonte: levantamento local, vistoria ou cenário simulado. Um parque ou uma árvore no mapa, por si só, não informa a sombra de uma rua ao longo do dia. A cobertura do OpenStreetMap também pode estar incompleta.

Dados de vegetação por satélite podem complementar o IPIU. A estimativa de sombra por horário continuará dependendo de um método específico.

## GeoSampa

Se a região escolhida for São Paulo, o GeoSampa poderá complementar os dados urbanos. O portal apresentou CAPTCHA durante a consulta. As camadas, os formatos de download e suas licenças ainda precisam ser verificados.

- [GeoSampa](https://geosampa.prefeitura.sp.gov.br/)

## IBGE

A API de agregados do IBGE disponibiliza as tabelas do SIDRA. A escolha do indicador social, da tabela, do ano e da unidade territorial ficará vinculada à região de demonstração.

Para cruzar esses dados com as áreas monitoradas, será necessária a malha territorial correspondente, com o mesmo ano e código geográfico. A disponibilidade por setor ou rua depende da base escolhida. Enquanto não houver uma base selecionada, os valores usados na demonstração serão identificados como simulados.

- [API de agregados do IBGE](https://servicodados.ibge.gov.br/api/docs/agregados?versao=3)

## INMET

Os arquivos históricos do INMET podem ser usados para comparar os resultados com observações de estações meteorológicas. Será preciso escolher a estação, conferir sua distância até a região e tratar lacunas e unidades.

A integração prevista é por importação de arquivos. Um endpoint de API para esse uso ainda não foi validado.

- [Dados históricos do INMET](https://portal.inmet.gov.br/dadoshistoricos)
