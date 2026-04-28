# Correções do mapa

Esta versão ajusta a coerência entre o globo e a Tactical View e melhora a aparência cartográfica do mapa.

## Principais mudanças

- Tactical View agora usa tiles quadrados (`MapAspectX = 1.0`), evitando o efeito de mapa achatado/fino em relação ao globo.
- Símbolos grandes/estranhos de terreno foram trocados por textura cartográfica sutil.
- Paleta de biomas ficou mais suave e com aparência de mapa.
- Rios agora são traçados por uma lógica hidrológica mais estável: nascem preferencialmente em montanhas/florestas, procuram cotas mais baixas, puxam para água próxima e evitam zigue-zague aleatório excessivo.
- Visual dos rios na Tactical View recebeu suavização, sombra e brilho leve.
- Redução da quantidade máxima de rios para evitar excesso visual.

## Observação

O build não foi executado neste ambiente porque não há Godot/.NET instalado aqui. O projeto continua estruturado para Godot 4.6.2 C#.
