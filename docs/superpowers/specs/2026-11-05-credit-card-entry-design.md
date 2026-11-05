# Tipo de entrada: tarjeta de crédito

## Intención y por qué

Añadir un tercer tipo de entrada, junto a contraseña y nota, para guardar tarjetas de pago. No
viene de un problema concreto, sino de una mejora que se espera útil. El uso principal es
**pagar online**: abrir la tarjeta y copiar número, caducidad y CVV al formulario de pago. De
paso, guarda datos que no son de pago pero que conviene tener a mano: PIN, teléfono del banco
(para bloquearla) y notas libres.

## Objetivos

- Crear, editar, borrar, marcar como favorita y etiquetar tarjetas, igual que el resto de
  entradas.
- Copiar cada campo con un clic; número, CVV y PIN con autolimpieza del portapapeles.
- Reconocer la tarjeta de un vistazo gracias a un dibujo de tarjeta en la parte superior del
  detalle.

## Fuera de alcance

- Autorrelleno en el navegador.
- Detectar la marca (Visa/Mastercard/…) o mostrar su logo.
- Exportar tarjetas a CSV e importarlas desde Bitwarden, KeePass o CSV. La exportación CSV
  actual filtra por `PasswordItem`, así que deja fuera las tarjetas sin tocar nada.
- Colores o diseños distintos por tarjeta.
- Bloquear el guardado por datos inválidos.

## Consecuencia aceptada

Las bóvedas que contengan tarjetas no se pueden abrir con versiones anteriores de Sekretujo,
porque desconocen el discriminador `"card"` y lo tratan como bóveda corrupta. Las bóvedas
existentes siguen abriéndose sin migrar nada.

## Modelo (Sekretujo.Core)

`CardItem : VaultEntry`, registrado con `[JsonDerivedType(typeof(CardItem), "card")]` en
`VaultEntry`. Todos los campos son `string`, inicializados a `string.Empty`:

| Campo | Contenido |
|---|---|
| `Holder` | Titular |
| `Number` | Número tal como se escribió, que puede llevar espacios o guiones |
| `Expiry` | Caducidad en formato `MM/AA` |
| `Cvv` | CVV/CVC |
| `Pin` | PIN |
| `BankPhone` | Teléfono del banco |
| `Notes` | Notas libres |

Un helper puro y testeado en Core (nombre a elección del constructor) con:

- `Digits(number)`: solo los caracteres `0-9` del número; descarta todo lo demás. Es lo que
  se copia al portapapeles.
- `LastFour(number)`: los últimos 4 dígitos, o vacío si hay menos de 4.
- `IsValidNumber(number)`: exige entre 12 y 19 dígitos y que pasen Luhn.
- `IsExpired(expiry, today)`: una tarjeta caduca al final de su mes. Se aceptan `MM/AA` y
  `MM/AAAA`, con espacios opcionales alrededor de la barra; el mes va de 01 a 12 y puede
  escribirse con una sola cifra («8/28»). Si el formato no es válido, el helper lo indica
  aparte, sin contarlo como caducada.

Tests en `VaultDataSerializationTests` (ida y vuelta de un `CardItem` dentro de `VaultData`)
y en un fichero nuevo para el helper.

## Pantalla de detalle (diseño B)

De arriba abajo:

- **Cabecera:** igual que ahora. La etiqueta de tipo dice «Tarjeta».
- **Tags:** igual que ahora.
- **Columna izquierda:**
  - **Dibujo de tarjeta**, solo de lectura. Un `Border` con degradado violeta, definido como
    clase de estilo nueva en `Styles/Theme.axaml`, con sus propios recursos de color de
    degradado y de texto en cada tema (oscuro y claro). Así el texto se lee bien en los dos:
    en el tema claro, `ColorAccentMuted` (#E3DEFA) es demasiado claro para texto blanco. Muestra el
    título en mayúsculas, el número enmascarado («•••• •••• •••• 1234»), el titular y la
    caducidad, y se actualiza en vivo mientras se edita. Siempre va enmascarado, aunque se
    revele el campo Número.
  - **Card «EDITAR»** con los campos Número (👁 + copiar), Titular y Caducidad en la misma
    fila (copiar), y CVV y PIN en la misma fila (👁 + copiar).
- **Columna derecha:** card «BANCO» con el campo Teléfono (copiar) y card «NOTAS».
- **Pie:** igual que ahora (eliminar, estado de guardado, Descartar, Guardar).

**Comportamiento:**

- Número, CVV y PIN se muestran con `PasswordChar` y cada uno tiene su propio botón 👁. Al
  cargar la entrada, los tres empiezan ocultos.
- Número, CVV y PIN se copian con `ClipboardAutoClearService.CopyAndClear`. Titular,
  caducidad y teléfono se copian con un `SetTextAsync` normal, igual que el usuario de una
  contraseña. El número se copia solo con dígitos. Se reutiliza el indicador `CopiedField` ✓.
- **Avisos** que no bloquean el guardado: un texto en `BrushWarning` bajo el campo afectado,
  con tres casos:
  - el número no es válido (longitud fuera de 12–19 dígitos o no pasa Luhn);
  - la caducidad no tiene un formato válido;
  - la tarjeta ha caducado.

  Con el campo vacío no se muestra ningún aviso.
- `IsDirty`, Descartar y Guardar funcionan igual que en el resto de tipos.

Boceto de referencia: `.superpowers/brainstorm/77050-1793873908/content/card-layout.html`, opción B.

## Integración en la shell

- **Barra superior:** botón «+ Tarjeta» después de «+ Nota», que crea un `CardItem` con el
  título «Nueva tarjeta».
- **Filtro por tipo:** pestaña «Tarjetas» después de «Notas» y un valor `EntryTypeFilter.Cards`.
  Afecta a los dos sitios de `ShellViewModel` que filtran por tipo, incluido el caso de
  una entrada nueva oculta por el filtro (commit 5a757c1).
- **Lista** (`EntryListItemViewModel`): etiqueta «Tarjeta», icono 💳 y subtítulo
  «•••• 1234», o «sin número» si no hay número.
- **Búsqueda:** sin cambios, sigue buscando en el título y las etiquetas. El número de
  tarjeta no se puede buscar.

## Decisiones que quedan al constructor

- Cómo distinguir el tipo en `EntryDetailViewModel`: hoy usa el booleano `IsNote` para
  elegir entre contraseña y nota. Se puede sustituir por un enum de tipo con propiedades
  derivadas `IsPassword`/`IsNote`/`IsCard`.
- El nombre y la ubicación exacta del helper de tarjeta y de la clase de estilo del dibujo.

## Verificación

- `dotnet test tests/Sekretujo.Core.Tests` en verde, con los tests nuevos.
- `dotnet build Sekretujo.slnx` sin errores.
- Prueba manual:
  1. Crear una tarjeta, guardarla, bloquear la bóveda, desbloquearla y comprobar que los
     datos siguen ahí.
  2. Copiar cada campo y comprobar que el número llega sin espacios y que el portapapeles se
     limpia en los campos ocultos.
  3. Ver los avisos con un número que no pase Luhn y con una fecha caducada.
  4. Probar los filtros, el subtítulo de la lista y que una bóveda sin tarjetas sigue
     abriéndose.
