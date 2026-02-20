# ClipNotes — Руководство для AI-агента

> Этот каталог содержит внутренние инструкции для Claude Code / AI-агента по работе с проектом ClipNotes.
> При первом знакомстве читай файлы последовательно. При конкретной задаче — переходи сразу к нужному разделу.

## Оглавление

| Файл | Содержание |
|------|-----------|
| [01-project-overview.md](01-project-overview.md) | Обзор, стек, структура папок, ключевые файлы |
| [02-build-and-scripts.md](02-build-and-scripts.md) | Сборка, PowerShell скрипты, установочники |
| [03-testing.md](03-testing.md) | Чек-лист тестирования всего функционала |
| [04-debugging.md](04-debugging.md) | Поиск багов, паттерны ошибок, карта файлов |
| [05-git-workflow.md](05-git-workflow.md) | Git-воркфлоу, коммиты, GitHub |
| [06-task-decomposition.md](06-task-decomposition.md) | Декомпозиция задач, шаблоны |

## Быстрый старт

```
1. Прочти 01-project-overview.md → понять проект
2. Сборка → 02-build-and-scripts.md
3. Тестирование → 03-testing.md
4. Баги → 04-debugging.md
5. Коммит → 05-git-workflow.md
```

## Критические правила

- Пути к инструментам — через `PathHelper.cs` (использует `Process.MainModule?.FileName`, не `Assembly.Location`)
- В `LogService.cs` — также `Process.MainModule?.FileName`
- Числа в FFmpeg аргументах — `value.ToString("F3", CultureInfo.InvariantCulture)` (не `{value:F3}` — даст запятую на RU Windows)
- stdout + stderr процессов читать параллельно через `Task.WhenAll` (иначе deadlock)
- Никогда не хардкодить абсолютные пути — всегда через PathHelper или `$PSScriptRoot`
- Коммитить только по явному запросу пользователя
- Отвечать на русском языке
- Не добавлять фичи сверх того что просили
