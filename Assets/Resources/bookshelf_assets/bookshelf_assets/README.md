# Bookshelf assets — cortes individuales

90 PNG extraídos del sprite sheet original (1536x1024), recorte directo pixel a pixel
(sin reescalar, sin recomprimir con pérdida) + fondo del sheet convertido a transparencia.

## Estructura
bookshelf_assets/
├── tall_bookshelf/        (2x3 tiles en el diseño original)
├── short_bookshelf/       (1x1 tile)
└── wide_low_bookshelf/    (2x1 tiles)

Cada carpeta: 6 materiales x 5 piezas = 30 archivos
Materiales: 01_rotted_wood, 02_rusted_metal, 03_decayed_painted,
            04_cracked_concrete, 05_blood_stained_wood, 06_moldy_green
Piezas:     start, variant_a, variant_b, variant_c, end

## index.json
Contiene para cada archivo: tipo, material, variante, caja de origen en el sheet
(source_box = [x0, y0, x1, y1]) y tamaño final en px.

## Nota sobre tamaños
El sheet no está dibujado a la escala nominal de los rótulos (32x32 / 64x32 / 64x96):
las celdas reales miden ~69-78 px (short), ~93-96 px (wide low) y ~74-112 px de ancho
(tall). Los recortes se entregan a resolución nativa para no perder información.
Si necesitas los tiles exactos a 32x32 / 64x32 / 64x96, hay que remuestrear (eso sí
implica pérdida) — se puede hacer en un segundo paso.
