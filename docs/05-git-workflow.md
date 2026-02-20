# 05 — Git воркфлоу

## Репозиторий

```
Путь:    E:\Claude Workstation\Projects\ClipNotes\source\
Ветка:   master
Remote:  origin → GitHub (настраивается через gh CLI)
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

Формат: `<type>: <описание на русском>`

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
feat: добавить Hold Mode с визуальным индикатором
build: добавить rebuild-installers.ps1
docs: обновить README и ARCHITECTURE для GitHub
```

## Правила

1. **Коммитить только по запросу пользователя** — не коммитить автоматически
2. **Добавлять файлы явно** (`git add path/to/file`) — не `git add .` или `git add -A`
3. **Не коммитить:** `app\`, `sessions\`, `.cache\`, `*.bin`, `Setup\`
4. **Никогда:** `--no-verify`, `push --force` без явного запроса

## Типичная последовательность коммита

```bash
cd '/mnt/e/Claude Workstation/Projects/ClipNotes/source'

# Посмотреть что изменено
git diff
git status

# Добавить нужные файлы
git add ClipNotes/ViewModels/MainViewModel.cs
git add ClipNotes/Views/MainWindow.xaml

# Коммит с подписью агента
git commit -m "$(cat <<'EOF'
fix: исправить двойной вызов GenerateAsync

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

## Push на GitHub

```bash
# Если GitHub CLI установлен и авторизован:
gh repo create ClipNotes --public --source=. --remote=origin --push

# Или через git remote + PAT:
git remote add origin https://USERNAME:TOKEN@github.com/USERNAME/ClipNotes.git
git push -u origin master

# Создать Release с установочниками:
gh release create v1.0.0 \
  '../Setup/ClipNotes-Setup.exe' \
  '../Setup/ClipNotes-portable.zip' \
  '../Setup/SHA256SUMS.txt' \
  --title "ClipNotes v1.0.0"
```

## Пересборка установочников перед push

```powershell
cd 'E:\Claude Workstation\Projects\ClipNotes\source'
.\rebuild-installers.ps1          # Online Setup + Portable ZIP
.\rebuild-installers.ps1 -Offline # + Offline Setup (скачивает CUDA whisper)
```

Результат: `Setup\ClipNotes-Setup.exe`, `Setup\ClipNotes-portable.zip`, `Setup\SHA256SUMS.txt`
