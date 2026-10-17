# TeslaMate CoreAPI
Net Core APIs to extend TeslaMate with batch operations

This is not a working project, it's just an experiment to deal with test data.

## Cambios para la release 1.0.0

- SQLite es la fuente de verdad para los canales y metadatos M3U.
- `/stream` y la descarga generan playlists desde la base de datos, en memoria.
- Se eliminaron la escritura local de playlists M3U y la creación de backups.
- Se quitó el filtro que excluía canales con “ELCANO”.
