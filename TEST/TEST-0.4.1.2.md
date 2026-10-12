# TEST 0.4.1.2 — compile hotfix 2

1. Убедиться, что `StorageAnalyzerService.cs` не содержит `TrimEnd('\\', '/')`.
2. Убедиться, что строки используют `Path.DirectorySeparatorChar` и `Path.AltDirectorySeparatorChar`.
3. Выполнить `BUILD-AND-RUN-WINDOWS.cmd`.
4. Проверить запуск Storage Analyzer.
