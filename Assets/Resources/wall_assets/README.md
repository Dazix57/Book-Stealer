# Library wall textures — cortes individuales

42 PNG extraídos del sheet original (1536x1024, "LIBRARY WALL TEXTURES 64x96").
Recorte directo pixel a pixel: sin reescalar, sin filtros, sin recompresión con pérdida.

## Nomenclatura
<material>_<variante>.png  →  p. ej. 03_decayed_painted_variant_e.png

Materiales (7): 01_rotted_wood, 02_rusted_metal, 03_decayed_painted,
                04_cracked_concrete, 05_blood_stained_wood, 06_moldy_green,
                07_dark_brown_brick
Variantes (6): variant_a … variant_f

## Cómo se detectaron los límites
El fondo del sheet es un gris casi negro uniforme (16,16,18). Se localizaron los
"canales" de fondo entre celdas (8-22 px) por proyección de filas y columnas, y cada
tile se cortó exactamente entre canales. Verificación automática de las 42 piezas:
las 4 líneas inmediatamente exteriores a cada recorte son fondo puro y ninguna línea
del borde interior es fondo → no hay recortes cortados ni fondo sobrante.

## index.json
Por archivo: material, variante, caja de origen (source_box = [x0, y0, x1, y1]) y tamaño.

## Nota de escala
El sheet no está dibujado a la escala nominal de 64x96. Los tiles reales miden
201-222 px de ancho y 108-146 px de alto (la altura es uniforme dentro de cada fila
de material; la fila 7, ladrillo, es la más baja). Se entregan a resolución nativa
para no perder información. Para tenerlos exactos a 64x96 hay que remuestrear, y eso
sí implica pérdida.
