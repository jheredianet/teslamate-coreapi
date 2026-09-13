# API de listas M3U

## Datos generales

- Base URL: la URL pública de la instalación de `coreAPI`.
- Formato: JSON para la API REST y `audio/x-mpegurl` para la lista M3U.
- Autenticación: actualmente no hay autenticación implementada en estos endpoints.
- Identificadores: `id` es el identificador interno de SQLite. El identificador de un canal AceStream es el valor de `StreamUrl` sin el prefijo `acestream://`.
- Orden: las respuestas se devuelven automáticamente por `GroupTitle`, `ChannelName` e identificador interno. No existe ordenamiento manual.

## Modelo `M3UEntry`

```json
{
  "id": 123,
  "groupTitle": "Dazn",
  "tvgLogo": "https://example.com/logo.png",
  "channelName": "Dazn 1 1080p",
  "streamUrl": "acestream://4005aabe21ba9c6d748845db91e4f48d99f58639",
  "tvgId": "Dazn 1 HD"
}
```

`groupTitle`, `channelName` y `streamUrl` son obligatorios al crear o actualizar. `tvgLogo` y `tvgId` son opcionales.

## Obtener todos los canales

```http
GET /api/m3u
```

Parámetros opcionales:

- `q`: busca en `channelName` y `streamUrl`.
- `group`: filtra por `groupTitle` sin distinguir mayúsculas/minúsculas.

Ejemplo:

```http
GET /api/m3u?q=dazn&group=deportes
```

Respuesta `200 OK`: array de objetos `M3UEntry`.

## Obtener un canal

```http
GET /api/m3u/{id}
```

Ejemplo:

```http
GET /api/m3u/123
```

Respuestas:

- `200 OK`: canal encontrado.
- `404 Not Found`: no existe un canal con ese id interno.

## Crear un canal

```http
POST /api/m3u
Content-Type: application/json
```

El campo `id` enviado se ignora y lo asigna SQLite.

Ejemplo:

```json
{
  "groupTitle": "Deportes",
  "tvgLogo": "https://example.com/logo.png",
  "channelName": "Canal Deportes",
  "streamUrl": "acestream://0123456789abcdef0123456789abcdef01234567",
  "tvgId": "Canal Deportes"
}
```

Respuesta `201 Created`: devuelve el canal creado, incluyendo su `id` interno.

El guardado reordena automáticamente toda la lista.

## Actualizar un canal

```http
PUT /api/m3u/{id}
Content-Type: application/json
```

El `id` de la ruta es el que se utiliza; el `id` del JSON se sustituye por él.

Ejemplo:

```http
PUT /api/m3u/123
Content-Type: application/json

{
  "groupTitle": "Deportes",
  "tvgLogo": "https://example.com/new-logo.png",
  "channelName": "Canal Deportes HD",
  "streamUrl": "acestream://0123456789abcdef0123456789abcdef01234567",
  "tvgId": "Canal Deportes HD"
}
```

Respuestas:

- `200 OK`: canal actualizado.
- `404 Not Found`: no existe el id indicado.

## Eliminar un canal

```http
DELETE /api/m3u/{id}
```

Respuestas:

- `204 No Content`: eliminado correctamente.
- `404 Not Found`: no existe el id indicado.

## Obtener la lista M3U para reproducción

```http
GET /stream?id={serverId}&format={format}
```

Parámetros:

- `id`: nombre del servidor configurado en la sección de servidores. Es obligatorio.
- `format`: `mpegts` o `hls`. Si se omite, se usa `mpegts`.

Ejemplos:

```http
GET /stream?id=home&format=mpegts
GET /stream?id=home&format=hls
```

Respuesta `200 OK`:

- Content-Type: `audio/x-mpegurl`.
- Contenido M3U con la cabecera y los `#EXTGRP` guardados durante la última sincronización.
- Para `mpegts`, los canales `acestream://ID` se convierten en `{BaseUrl}/ace/getstream?id=ID`.
- Para `hls`, se convierten en `{BaseUrl}/ace/manifest.m3u8?id=ID`.

Respuestas de error:

- `400 Bad Request`: servidor inexistente, `id` vacío o formato distinto de `mpegts`/`hls`.
- `500 Internal Server Error`: error al leer o generar la lista.

## Sincronización

La sincronización está disponible actualmente desde la UI:

```http
POST /m3u/Synchronize
```

Requiere el token antifalsificación generado por la vista, por lo que no es un endpoint REST pensado para consumo directo de otro agente.

La operación descarga la fuente principal, fusiona por id AceStream, actualiza los canales coincidentes, conserva los canales locales ausentes, elimina duplicados y guarda las URLs como `acestream://ID`.

Fuente principal:

```text
https://git.gay/TokyoGhoulles/AceStream_IDs/raw/branch/main/hashes.m3u
```

## Nota para integradores

No debe usarse el `id` interno de SQLite para deduplicar canales entre sistemas. Para identificar un canal AceStream debe extraerse el valor de:

```text
acestream://ID
```

La lista M3U servida por `/stream` ya contiene las URLs adaptadas al servidor solicitado y debe consumirse como un fichero M3U estándar.
