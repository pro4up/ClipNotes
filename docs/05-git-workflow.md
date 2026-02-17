# 05 — Git воркфлоу

## Репозиторий

```
Путь:   E:\Claude Workstation\Projects\ClipNotes\source\
Ветка:  master
```

WSL-путь для команд: `/mnt/e/Claude Workstation/Projects/ClipNotes/source`

## Основные команды

```bash
cd '/mnt/e/Claude Workstation/Projects/ClipNotes/source'

git status           # что изменено
git diff             # детали изменений
git log --oneline    # история коммитов
git add <file>       # добавить конкретный файл (не git add -A!)
git commit -m "..."  # коммит
```

## Соглашения по коммитам

Формат: `<type>: <описание на русском или английском>`

| Тип | Когда использовать |
|-----|-------------------|
| `feat` | Новый функционал |
| `fix` | Исправление бага |
| `refactor` | Рефакторинг без изменения поведения |
| `docs` | Изменения документации |
| `build` | Изменения скриптов сборки, зависимостей |
| `chore` | Прочие технические задачи |

**Примеры:**
```
fix: исправить deadlock при чтении stdout/stderr FFmpeg
feat: добавить поддержку модели large-v3
build: обновить пути в download-скриптах под новую структуру
docs: добавить инструкцию для AI-агента
```

## Правила

1. **Коммитить только по запросу пользователя** — не коммитить автоматически
2. **Добавлять файлы явно** (`git add path/to/file`) — не `git add .` или `git add -A`
3. **Не коммитить:** `app\`, `sessions\`, `.cache\`, `*.bin`, `*.exe`
4. **Никогда:** `--no-verify`, `push --force` без явного запроса

## .gitignore (должен содержать)

```gitignore
# Собранное приложение
../app/

# Сессии с данными
../sessions/

# Кеш зависимостей
tools/.cache/

# Стандартное .NET
obj/
bin/
*.user
.vs/
```

## Проверить .gitignore

```bash
cd '/mnt/e/Claude Workstation/Projects/ClipNotes/source'
cat .gitignore
```

Если `../app/` и `../sessions/` не исключены — добавить.

## Типичная последовательность коммита после исправления бага

```bash
cd '/mnt/e/Claude Workstation/Projects/ClipNotes/source'

# Посмотреть что изменено
git diff

# Добавить только нужные файлы
git add ClipNotes/Services/AudioProcessingService.cs

# Коммит
git commit -m "fix: исправить deadlock при запуске whisper-cli"
```
