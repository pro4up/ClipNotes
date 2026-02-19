# ClipNotes — Руководство для AI-агента (qwen3-coder-next)

> Этот каталог содержит инструкции для локального Claude Code / AI-агента по работе с проектом ClipNotes.
> Читай файлы последовательно при первом знакомстве. При выполнении конкретной задачи — переходи сразу к нужному разделу.

## Оглавление

| Файл | Содержание |
|------|-----------|
| [01-project-overview.md](01-project-overview.md) | Обзор проекта, стек, структура папок, ключевые файлы |
| [02-build-and-scripts.md](02-build-and-scripts.md) | Сборка, PowerShell скрипты, зависимости, запуск |
| [03-testing.md](03-testing.md) | Тестирование функционала: OBS, горячие клавиши, транскрипция, экспорт |
| [04-debugging.md](04-debugging.md) | Поиск и отладка багов, известные проблемы, паттерны ошибок |
| [05-git-workflow.md](05-git-workflow.md) | Git-воркфлоу, соглашения по коммитам |
| [06-task-decomposition.md](06-task-decomposition.md) | Декомпозиция задач, шаблоны подзадач |

## Быстрый старт

```
1. Прочти 01-project-overview.md → понять проект
2. Выполни сборку по 02-build-and-scripts.md
3. Протестируй по 03-testing.md
4. Баги → 04-debugging.md
5. Коммит → 05-git-workflow.md
```

## Критические правила

- Все относительные пути в PS-скриптах строятся от `$PSScriptRoot`
- В C# пути к инструментам через `PathHelper.cs` (Assembly.Location)
- В `LogService.cs` путь через `Process.GetCurrentProcess().MainModule?.FileName` (не Assembly.Location)
- Никогда не хардкодить абсолютные пути
- Коммитить только по запросу пользователя
- Отвечать на русском языке
