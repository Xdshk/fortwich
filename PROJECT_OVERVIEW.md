# Twitch Stress Toolkit - Конспект проекта

## 📌 Текущая стадия

**Готово:** Рабочий каркас с реальным подключением к Twitch IRC; UI подключён к движку, графики и навигация работают
**Версия:** 0.1.0
**Платформа:** .NET 10, WPF (Windows), macOS/Linux (без UI)
**Текущий фокус:** стабильность запуска (приложение крашится при старте — ищем причину по crash.log)

---

## 🏗️ Архитектура

```
TwitchStressToolkit/
├── Core/                    # Доменные модели и интерфейсы
├── Application/             # Бизнес-логика
├── Infrastructure/          # Реализация (сеть, БД, логирование)
└── UI/                      # WPF интерфейс
```

---

## ✅ Что реализовано

### Core (TwitchStressToolkit.Core)
| Файл | Назначение |
|------|------------|
| `Models/BotAccount.cs` | Модель аккаунта с шифрованными данными |
| `Models/VirtualClient.cs` | Модель виртуального клиента |
| `Models/ConnectionConfig.cs` | Настройки подключения |
| `Models/ProxyInfo.cs` | Модель прокси |
| `Models/ActivityProfile.cs` | Профили активности (Casual, Active, Spammer) |
| `Models/SimulationResult.cs` | Результаты симуляции |
| `Models/Sample.cs` | Модель для метрик (LiveCharts) |
| `Interfaces/IVirtualClient.cs` | Контракт клиента |
| `Interfaces/ISimulationEngine.cs` | Контракт движка симуляции |
| `Interfaces/IProxyManager.cs` | Контракт менеджера прокси |
| `Interfaces/IAuthService.cs` | Контракт авторизации |
| `Interfaces/IMetricsCollector.cs` | Контракт сбора метрик |
| `Interfaces/IStorageService.cs` | Контракт хранилища |
| `Enums/ClientState.cs` | Состояния клиента (Idle, Connecting, Connected...) |
| `Enums/ScenarioType.cs` | Типы сценариев |
| `Exceptions/ConnectionException.cs` | Ошибки подключения |
| `Exceptions/AuthenticationException.cs` | Ошибки авторизации |

### Application (TwitchStressToolkit.Application)
| Файл | Назначение |
|------|------------|
| `Simulation/SimulationEngine.cs` | Движок симуляции: управляет клиентами, сценариями |
| `Simulation/VirtualClient.cs` | Виртуальный клиент: WebSocket, отправка сообщений |
| `Simulation/ClientScheduler.cs` | Планировщик действий клиента |
| `Simulation/Scenarios/MassConnectScenario.cs` | Сценарий: массовое подключение |
| `Simulation/Scenarios/GradualConnectScenario.cs` | Сценарий: постепенное подключение |
| `Simulation/Scenarios/WaveScenario.cs` | Сценарий: волнообразная нагрузка |
| `Simulation/Scenarios/BurstScenario.cs` | Сценарий: burst-нагрузка |
| `Simulation/Scenarios/RandomWalkScenario.cs` | Сценарий: случайное поведение |
| `Chat/ChatMessageGenerator.cs` | Генератор сообщений с опечатками и эмодзи |
| `Metrics/MetricsCollector.cs` | Сбор метрик через Channels<T> |
| `Accounts/AccountManager.cs` | Управление аккаунтами: CRUD, валидация |
| `DependencyInjection.cs` | Регистрация сервисов |

### Infrastructure (TwitchStressToolkit.Infrastructure)
| Файл | Назначение |
|------|------------|
| `Network/TwitchIrcClient.cs` | WebSocket-клиент для Twitch IRC с прокси |
| `Network/TwitchAuthService.cs` | Авторизация через OAuth токен |
| `Storage/SqliteStorageService.cs` | SQLite хранилище (аккаунты, логи, результаты) |
| `Storage/AccountLoader.cs` | Загрузка аккаунтов из TXT/JSON |
| `Proxy/ProxyManager.cs` | Менеджер прокси: ротация, здоровье |
| `Fingerprint/FingerprintManager.cs` | Генерация fingerprint браузера |
| `Security/SecureCredentialService.cs` | Шифрование паролей (DPAPI) |
| `Logging/SerilogConfigurator.cs` | Настройка Serilog |
| `DependencyInjection.cs` | Регистрация сервисов |

### Application — метрики
| Файл | Назначение |
|------|------------|
| `Metrics/MetricsBus.cs` | Единая шина метрик: читает `Sample` из `IMetricsCollector` в фоновом потоке, шлёт `MetricsSnapshot` ~4 раза/сек подписчикам (UI VM). Защищает UI-поток от флуда событиями. |

### UI (TwitchStressToolkit.UI)
| Файл | Назначение |
|------|------------|
| `Views/MainWindow.xaml` | Главное окно с вкладками (TabControl) и меню, привязанным к командам MainViewModel |
| `Views/DashboardView.xaml` | Дашборд: 4 счётчика + 2 LiveCharts-графика (Latency, Throughput) |
| `Views/BotManagerView.xaml` | Управление ботами: загрузка, добавление, валидация, таблица |
| `Views/SettingsView.xaml` | Настройки подключения и активности |
| `Views/ChartsView.xaml` | Графики в реальном времени (две серии на одном графике) |
| `ViewModels/MainViewModel.cs` | VM главного окна: инжектит `ISimulationEngine` + `MetricsBus`; меню Start/Stop дёргает реальный движок; навигация по вкладкам через `SelectedTabIndex` и `Show*Command` |
| `ViewModels/BotManagerViewModel.cs` | VM управления ботами: CRUD аккаунтов, загрузка прокси, запуск/стоп симуляции |
| `ViewModels/DashboardViewModel.cs` | VM дашборда: подписан на `MetricsBus.SnapshotUpdated`, обновляет счётчики и `LineSeries<double>` графиков |
| `ViewModels/SettingsViewModel.cs` | VM настроек: `ToConfig()` → `ConnectionConfig` |
| `ViewModels/ChartsViewModel.cs` | VM графиков: подписан на `MetricsBus`, две `LineSeries<double>` (Latency + Msg/sec) |
| `Themes/DarkTheme.xaml` | Темная тема (глобальные стили без `x:Key`) |

---

## 📋 Файлы данных

| Файл | Назначение | Формат |
|------|------------|--------|
| `data/accounts.txt` | Аккаунты для загрузки | `username:password` |
| `data/accounts.json` | Аккаунты (JSON) | JSON массив |
| `data/proxies.txt` | Прокси список | `host:port:user:pass` |
| `data/messages.txt` | Шаблоны сообщений | По строке на сообщение |

---

## 🔧 Как запустить

### Сборка
```bash
dotnet publish TwitchStressToolkit.UI -c Release -r win-x64 --self-contained
```

### Запуск
```bash
cd bin/Release/net10.0-windows/win-x64/publish
TwitchStressToolkit.UI.exe
```

---

## 🎯 Что делает программа сейчас

1. **Загрузка аккаунтов** из TXT или JSON файла
2. **Подключение к Twitch IRC** через WebSocket (`wss://irc-ws.chat.twitch.tv:443`)
3. **Отправка сообщений** в указанный канал
4. **Поддержка прокси** (HTTP, SOCKS4, SOCKS5)
5. **Шифрование паролей** в локальной SQLite БД
6. **Логирование** в файл (`%LocalAppData%/TwitchStressToolkit/logs/`) и консоль
7. **Автоматическое переподключение** при разрыве
8. **Меню полностью функционально**: Simulation → Start/Stop запускает реальный `SimulationEngine`; Tools → навигация по вкладкам; File → Load Accounts/Exit
9. **Графики в реальном времени**: Dashboard и Charts получают метрики через `MetricsBus` и обновляют `LineSeries<double>` ~4 раза/сек
10. **Навигация по вкладкам** через команды (`ShowDashboard/ShowBotManager/ShowCharts/ShowSettings/ShowLogs`) и `TabControl.SelectedIndex`

---

## 🔜 Что нужно сделать (roadmap)

### Приоритет 1: Стабильность ⬅️ ТЕКУЩИЙ
- [x] Привязать Menu к командам MainViewModel
- [x] Исправить дублирование `Grid.Row` в BotManagerView
- [x] Исправить тип коллекций графиков (`ObservableCollection<ObservablePoint>` → `LineSeries<double>`)
- [x] Создать ChartsViewModel и задать DataContext
- [x] Подключить SimulationEngine к MainViewModel
- [x] Создать MetricsBus для метрик между движком и UI
- [x] Добавить навигацию по вкладкам через команды
- [x] Добавить crash-dump лог (`%LocalAppData%/TwitchStressToolkit/logs/crash.log`)
- [x] Исправить двойной Dispose ErrorLogService в OnExit
- [ ] **Найти и исправить краш при старте** (ждём `crash.log` от пользователя)
- [ ] Graceful shutdown (остановка MetricsBus при закрытии окна)
- [ ] Обработка ошибок переподключения
- [ ] Валидация входных данных

### Приоритет 2: Функционал
- [ ] Эмуляция viewer-запросов (GQL API)
- [ ] Автоматическая ротация прокси
- [ ] Генерация fingerprint для каждого бота
- [ ] Настройка rate limit per bot
- [ ] Точный msg/sec (дельта по времени, сейчас = TotalMessagesSent)

### Приоритет 3: UI
- [x] LiveCharts графики в реальном времени
- [ ] Прогресс-бар подключения
- [ ] Экспорт результатов в CSV
- [ ] Привязать Logs-вкладку к `MainViewModel.Logs`

### Приоритет 4: Безопасность
- [ ] Проверка прокси перед использованием
- [ ] Ограничение частоты сообщений
- [ ] Рандомизация поведения

---

## ⚠️ Важно понимать

### Технические ограничения
- **WPF работает только на Windows** — для macOS/Linux нужна Avalonia
- **TLS-отпечаток** — .NET HttpClient имеет уникальный JA3, который Twitch может детектировать
- **Прокси** — нужны резидентные (не дата-центры), иначе бан

### Юридические
- **Нарушение ToS** — использование на Twitch без разрешения запрещено
- **Бан аккаунтов** — Twitch детектирует ботов в течение минут
- **Рекомендация** — используй только для тестирования своих серверов

---

## 📊 Схема взаимодействия

```
┌─────────────────────────────────────────────────────────────┐
│                        WPF UI                                │
│  MainViewModel ─── Commands ───► SimulationEngine            │
│       │                              │                       │
│       │ SelectedTabIndex             │                       │
│       ▼                              ▼                       │
│  TabControl ◄── Views ─────── VirtualClient (per bot)        │
│  (Dashboard/BotManager/        │         │                   │
│   Charts/Settings/Logs)        │    WebSocket                │
│       ▲                        ▼         ▼                   │
│       │                   Twitch IRC (via proxy)             │
│  MetricsBus ◄── Samples ── MetricsCollector                  │
│  (SnapshotUpdated ~4/с)                                      │
│       │                                                      │
│       └──► DashboardViewModel / ChartsViewModel (LineSeries) │
└─────────────────────────────────────────────────────────────┘
```

### Поток данных метрик
1. `VirtualClient` вызывает `MetricsCollector.RecordLatency/RecordConnection/...`
2. `MetricsCollector` пишет `Sample` в `Channel<Sample>`
3. `MetricsBus` (фоновый поток) читает канал, агрегирует, шлёт `MetricsSnapshot` ~4 раза/сек
4. `MainViewModel`, `DashboardViewModel`, `ChartsViewModel` подписаны на `MetricsBus.SnapshotUpdated`, маршаллят в UI-поток через `Dispatcher.Invoke` и обновляют свойства/графики

---

## 🔗 Ключевые зависимости

| Пакет | Версия | Назначение |
|-------|--------|------------|
| CommunityToolkit.Mvvm | 8.4.2 | MVVM framework |
| Microsoft.Data.Sqlite | 10.0.9 | SQLite БД |
| Serilog | 4.3.1 | Логирование |
| LiveChartsCore | 2.0.5 | Графики |
| Polly | 8.7.0 | Retry политики |

---

## 📝 Команды

```bash
# Сборка
dotnet build

# Запуск (отладка)
dotnet run --project TwitchStressToolkit.UI

# Публикация (self-contained .exe)
dotnet publish TwitchStressToolkit.UI -c Release -r win-x64 --self-contained

# Публикация (linux)
dotnet publish TwitchStressToolkit.UI -c Release -r linux-x64 --self-contained
```

---

*Создано: 2024*
*Версия: 0.1.0*
