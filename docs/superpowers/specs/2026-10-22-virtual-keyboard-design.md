# Teclado virtual en pantalla

## Intención y por qué

Sekretujo guarda contraseñas y notas localmente; la amenaza que esta función busca mitigar
es un keylogger de teclado físico (hardware o software) capturando lo que el usuario escribe
mientras rellena un campo de la app — incluida la contraseña maestra en el desbloqueo. Un
teclado virtual, manejado con ratón, no genera eventos de teclado, así que un keylogger de
teclado no ve nada.

**No-objetivo / límite conocido:** esto no protege contra captura de pantalla, registro de
posición del ratón, o malware que lee memoria del proceso. Es una mitigación parcial, no una
solución completa. El botón "Revolver" ayuda específicamente contra ataques que infieren la
tecla pulsada por la posición del clic.

## Alcance

- Teclado QWERTY completo: letras, fila de números, símbolos, Shift (mayúsculas + símbolos de
  la fila de números), Espacio, Backspace.
- Escribe en el control de texto que tenga el foco en cada momento, en cualquier pantalla de
  la app (desbloqueo y app ya abierta).
- Botón de toggle persistente que abre/cierra un panel fijo flotando sobre la ventana.
- Botón "Revolver" (randomiza la posición de las teclas de carácter) y botón "Restaurar"
  (vuelve al QWERTY estándar).

## Fuera de alcance

- Auto-clear del portapapeles (se tratará por separado).
- Protección contra captura de pantalla, mouse-loggers o malware a nivel de memoria.
- Persistir el estado de "revuelto" entre sesiones (cada apertura del panel empieza en
  distribución estándar).
- Tecla Enter/salto de línea.
- Caracteres específicos del español (ñ, acentos, ¿¡) — solo layout QWERTY US.

## Decisiones de arquitectura

- **Dónde vive:** todo el control (botón de toggle + panel) se añade a `MainWindow.axaml`,
  por encima del `ContentControl` que intercambia `UnlockViewModel`/`ShellViewModel`. Así
  funciona igual en ambas pantallas sin duplicar nada.
- **Sin ViewModel propio:** el panel es un `UserControl` autocontenido en
  `src/Sekretujo.App/Views/VirtualKeyboardPanel.axaml` con su código detrás
  (`VirtualKeyboardPanel.axaml.cs`). Es puramente una utilidad de la UI (estado de
  Shift/revuelto, qué `TextBox` tiene el foco) sin reglas de negocio que testear — coherente
  con que `Sekretujo.App` no tiene tests de UI/ViewModel (ver CLAUDE.md §9).
- **Seguimiento del foco:** `MainWindow` añade un `AddHandler(GotFocusEvent, ..., handledEventsToo: true)`
  a nivel de ventana (igual patrón que ya usa para `PointerMovedEvent`/`KeyDownEvent` en
  `MainWindow.axaml.cs`). Cuando el control con foco es un `TextBox`, se guarda la referencia
  y se pasa al panel. Pulsar un botón del panel no debe robar el foco del `TextBox` (los
  botones del panel se marcan `Focusable="False"`), así el `TextBox` activo permanece el
  mismo mientras el panel está abierto.
- **Inserción de texto:** al pulsar una tecla, se inserta el carácter en
  `TextBox.CaretIndex` del `TextBox` activo (reemplazando la selección si la hay) y se
  avanza el caret — mismo efecto que escribir esa tecla físicamente. Backspace borra el
  carácter anterior al caret o la selección.
- **Layout de teclas:** cada tecla de carácter tiene un par minúscula/mayúscula (o
  normal/shift-símbolo para números). Shift es un toggle persistente (no momentáneo) que
  alterna qué miembro del par se muestra/escribe en todas las teclas de carácter a la vez.
  No hay Caps Lock separado — Shift cubre ambos casos para no multiplicar estados.
- **Revolver:** baraja (Fisher-Yates) las posiciones de todas las teclas de carácter
  (letras, números y símbolos juntos, un único pool) entre sí; Shift, Backspace, Espacio y
  los botones Revolver/Restaurar nunca cambian de sitio. "Restaurar" reordena a la
  distribución QWERTY original.
- **Enter:** fuera de alcance, confirmado — aunque `Notes`/`Body` (multilínea) usan Enter
  para saltos de línea, de momento no se añade esa tecla; se escriben con el teclado físico
  si hace falta un salto de línea.
- **Idioma del layout:** QWERTY estándar US, sin ñ, acentos ni ¿¡ — confirmado. Queda fuera
  de alcance por ahora (posible ampliación futura).
- **Estilo:** reutiliza las clases de `Theme.axaml` (`Border.card`, `Button`, `Button.ghost`)
  para que el panel combine con el resto de la UI sin añadir estilos nuevos más allá de la
  rejilla de teclas.

## Qué queda al criterio del implementador

- Tamaño exacto, posición (p. ej. anclado a la parte inferior de la ventana) y espaciado del
  panel.
- Icono/texto del botón de toggle y su posición en `MainWindow`.
- Estructura interna de datos para las teclas (p. ej. un record `KeyDef(string Normal, string
  Shifted)` por tecla).
