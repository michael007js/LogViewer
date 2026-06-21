# ConfigStore-v1 可执行计划

## 目标

建立统一配置存储基础设施 `AppConfigStore`，替代散落的 Properties.Settings 硬编码持久化模式，支持多模块配置分区、类型安全读写、运行时热更新与变更通知。

配置文件路径：`%LOCALAPPDATA%\LogViewer\logcat-config.json`（✅ 已采纳：c:\ 根目录普通用户无写入权限，改用 LocalApplicationData）。

## 架构

- JSON 文件 + 内存快照 + 变更事件的分层架构
- 核心：`AppConfigStore`（write-through 原子写入 + 线程安全）+ 分区注册模型
- 配置文件：`%LOCALAPPDATA%\LogViewer\logcat-config.json`，单文件全局管理（✅ 已采纳：c:\ 无写入权限）
- ✅ 配置文件路径：`Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` + `\LogViewer\logcat-config.json`
- 配置文件路径硬编码在 `AppConfigStore.cs` 的 `FileName` 常量 + `GetConfigDirectory()` 动态方法，修改即可改变存储路径
- 损坏文件 rename 为 `.corrupt.bak`，不静默丢弃

## 技术选型

| 类别 | 选型 | 说明 |
|------|------|------|
| 序列化 | `System.Text.Json` | 零额外依赖，且与通信协议一致 |
| 持久化 | `%LOCALAPPDATA%\LogViewer\logcat-config.json` | ✅ 已采纳：c:\无写入权限改用LocalApplicationData；路径由 `GetConfigDirectory()` 动态获取 |
| 线程安全 | `lock(Sync)` + write-through | Set 先写磁盘再更新内存 |
| 设计时检测 | `AppDesignTimeHelper` + `Func<bool>` 委托注入 | ✅ 已采纳方案C：MainForm注册分区时传入委托，AppSettings.Load不再引用Utils |

## 数据模型约定

- 分区 POCO 使用 PascalCase 属性，命名以 `Config` 结尾
- JSON 序列化使用 `System.Text.Json`，**必须** `PropertyNameCaseInsensitive = true`
- 分区键 = POCO 类名去掉 `Config` 后缀（如 `NetworkLogConfig` → `"NetworkLog"`）
- 每个分区 POCO 必须提供无参构造函数和合理默认值
- 分区 POCO 遵循模块自治：属于哪个模块放哪个模块目录（如 `NetworkLogConfig` 放 `Models/`）
- ConfigStore 不持有任何模块特定知识

## 依赖关系

```
Task 0 → Task 1 → Task 2 → Task 3 → Task 4
```

---

## Task 0：提取 AppDesignTimeHelper + 创建核心数据模型

### 文件清单

| 操作 | 路径 |
|------|------|
| 新增 | `Utils/AppDesignTimeHelper.cs` |
| 新增 | `Models/AppConfigSchema.cs` |
| 新增 | `Models/AppConfigSection.cs` |
| 新增 | `Models/AppConfigChangedEventArgs.cs` |

### Step 1：创建 AppDesignTimeHelper

文件：`LogViewer/Utils/AppDesignTimeHelper.cs`

```csharp
using System.ComponentModel;
using System.Diagnostics;

namespace LogViewer.Utils;

public static class AppDesignTimeHelper
{
    public static bool IsDesignTime => IsDesignTimeContext();

    public static bool IsDesignTimeFor(Control? control)
    {
        return IsDesignTimeContext(control);
    }

    private static bool IsDesignTimeContext(Control? control = null)
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return true;
        }

        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (current.Site?.DesignMode == true)
            {
                return true;
            }
        }

        string processName = Process.GetCurrentProcess().ProcessName;
        if (processName.Contains("devenv", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("designtoolsserver", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("xdesproc", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AppDomain.CurrentDomain.FriendlyName.Contains(
            "DesignToolsServer", StringComparison.OrdinalIgnoreCase);
    }
}
```

### Step 2：创建 AppConfigSchema

文件：`LogViewer/Models/AppConfigSchema.cs`

```csharp
using System.Text.Json.Serialization;

namespace LogViewer.Models;

internal sealed class AppConfigSchema
{
    [JsonPropertyName("SchemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("Sections")]
    public Dictionary<string, JsonElement> Sections { get; set; } = new();
}
```

### Step 3：创建 AppConfigSection

文件：`LogViewer/Models/AppConfigSection.cs`

```csharp
namespace LogViewer.Models;

internal sealed class AppConfigSection
{
    public string Key { get; }
    public Type ConfigType { get; }
    public Func<object> DefaultValueFactory { get; }

    public AppConfigSection(
        string key,
        Type configType,
        Func<object> defaultValueFactory)
    {
        Key = key;
        ConfigType = configType;
        DefaultValueFactory = defaultValueFactory;
    }
}
```

### Step 4：创建 AppConfigChangedEventArgs

文件：`LogViewer/Models/AppConfigChangedEventArgs.cs`

```csharp
namespace LogViewer.Models;

public sealed class AppConfigChangedEventArgs : EventArgs
{
    public string SectionKey { get; }
    public object? OldValue { get; }
    public object NewValue { get; }

    public AppConfigChangedEventArgs(string sectionKey, object? oldValue, object newValue)
    {
        SectionKey = sectionKey;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
```

### Step 5：构建验证

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

---

## Task 1：创建 AppConfigStore 核心

### 文件清单

| 操作 | 路径 |
|------|------|
| 新增 | `Models/AppConfigStore.cs` |

### Step 1：创建 AppConfigStore

文件：`LogViewer/Models/AppConfigStore.cs`

- ✅ 已采纳方案C：AppConfigStore 通过 `Func<bool>` 委托注入设计时检测，UI层注册分区时传入 `AppDesignTimeHelper.IsDesignTime`，消除 Models → Utils 依赖
- Set 采用 write-through：锁内先持久化成功再更新内存快照，持久化失败抛异常、内存不变
- Get/Set 双向深拷贝（JSON 往返序列化）
- ConfigChanged 事件在锁外触发（避免事件处理器调用 Get/Set 死锁）
- ✅ ConfigChanged 可在任意线程触发，UI订阅者必须使用 `BeginInvoke` 切回 UI 线程（已在代码注释中标注）
- 设计时 Set 更新 Snapshot 但跳过持久化
- Initialize 后不可再注册分区
- 损坏文件 rename 为 `.corrupt.bak` + Debug 日志
- 临时文件策略：写 `logcat-config.tmp.{Guid}` → `File.Move` 原子替换
- `AppConfigStore.cs` 的 using 不含 UI 命名空间
- MigrateLegacySettingsFile 本轮实施（从 Properties.Settings 读取旧值写入 logcat-config.json）
- ResetForTesting ✅ 改用 `GetInvocationList()` 逐个移除订阅者 + 支持临时目录注入

```csharp
using System.Diagnostics;
using System.Text.Json;

namespace LogViewer.Models;

public static class AppConfigStore
{
    private static readonly object Sync = new();
    private static readonly List<AppConfigSection> Sections = new();
    private static readonly Dictionary<string, object> Snapshot = new();
    private static bool _initialized;

    // ✅ 设计时检测委托——UI层注册分区时传入 AppDesignTimeHelper.IsDesignTime，消除 Models → Utils 依赖
    private static Func<bool>? _isDesignTimeCheck;

    private const int CurrentSchemaVersion = 1;

    // ✅ 已采纳：c:\ 根目录普通用户无写入权限，改用 %LOCALAPPDATA%\LogViewer
    private const string FileName = "logcat-config.json";

    // ✅ 测试注入：允许测试时替换配置目录，避免写入生产配置文件
    private static string? _overrideConfigDirectory;

    private static string GetConfigDirectory() =>
        _overrideConfigDirectory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogViewer");

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
/// 配置变更事件。⚠️ 可在任意线程触发（包括后台线程），UI订阅者必须使用 BeginInvoke 切回 UI 线程。
/// </summary>
    public static event EventHandler<AppConfigChangedEventArgs>? ConfigChanged;

    public static void RegisterSection<T>(
        string key,
        Func<T> defaultValueFactory,
        Func<bool>? isDesignTimeCheck = null) where T : class, new()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                throw new InvalidOperationException(
                    "AppConfigStore 已初始化，不可再注册分区。请在 Initialize() 之前注册。");
            }

            if (Sections.Any(s => s.Key == key))
            {
                throw new InvalidOperationException(
                    $"配置分区 '{key}' 已注册，不可重复注册。");
            }

            _isDesignTimeCheck = isDesignTimeCheck ?? _isDesignTimeCheck;

            Sections.Add(new AppConfigSection(
                key, typeof(T), () => defaultValueFactory()));
        }
    }

    public static void Initialize()
    {
        if (_initialized) return;

        // ⚠️ 假设 Initialize 只在 UI 线程调用（WinForms 场景安全）
        if (_isDesignTimeCheck?.Invoke() ?? false)
        {
            lock (Sync)
            {
                if (_initialized) return;
                foreach (AppConfigSection section in Sections)
                {
                    Snapshot[section.Key] = section.DefaultValueFactory();
                }
                _initialized = true;
            }
            return;
        }

        lock (Sync)
        {
            if (_initialized) return;

            try
            {
                CleanupTempFiles();
                AppConfigSchema schema = LoadSchema();

                // ✅ 已修复：MigrateLegacy 只在 schema 中不存在该分区时才写入
                // 避免"非首次运行时旧 Properties.Settings 值覆盖已有 logcat-config.json 值"的问题
                MigrateLegacySettingsFile(schema);
                PopulateSnapshot(schema);
                SaveSchema(schema);
            }
            catch
            {
                // ✅ 决策#18：即使初始化过程出错，也标记已初始化，避免后续调用无限重试
                Debug.WriteLine("[AppConfigStore] 初始化异常，使用默认分区值");
                foreach (AppConfigSection section in Sections)
                {
                    if (!Snapshot.ContainsKey(section.Key))
                    {
                        Snapshot[section.Key] = section.DefaultValueFactory();
                    }
                }
            }

            _initialized = true;
        }
    }

    public static T Get<T>(string key) where T : class, new()
    {
        EnsureInitialized();

        lock (Sync)
        {
            if (!Snapshot.TryGetValue(key, out object? value))
            {
                throw new InvalidOperationException(
                    $"配置分区 '{key}' 未注册。");
            }

            if (value is not T typed)
            {
                throw new InvalidOperationException(
                    $"配置分区 '{key}' 的类型不匹配。期望 {typeof(T).Name}，实际 {value.GetType().Name}。");
            }

            return DeepClone(typed);
        }
    }

    public static void Set<T>(string key, T value) where T : class, new()
    {
        EnsureInitialized();

        object? oldValue;
        T clonedValue;

        lock (Sync)
        {
            if (!Snapshot.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"配置分区 '{key}' 未注册。");
            }

            clonedValue = DeepClone(value);
            oldValue = Snapshot.TryGetValue(key, out object? existing)
                ? DeepClone((T)existing)
                : null;

            if (!(_isDesignTimeCheck?.Invoke() ?? false))
            {
                SaveSchemaWithNewValue(key, clonedValue);
            }

            Snapshot[key] = clonedValue;
        }

        ConfigChanged?.Invoke(null, new AppConfigChangedEventArgs(key, oldValue, clonedValue));
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetForTesting(string? overrideConfigDirectory = null)
    {
        lock (Sync)
        {
            Snapshot.Clear();
            Sections.Clear();
            _initialized = false;
            _isDesignTimeCheck = null;
            _overrideConfigDirectory = overrideConfigDirectory;
            if (ConfigChanged is not null)
            {
                foreach (EventHandler<AppConfigChangedEventArgs> d in ConfigChanged.GetInvocationList())
                {
                    ConfigChanged -= d;
                }
            }
        }
    }

    private static T DeepClone<T>(T value) where T : class, new()
    {
        try
        {
            string json = JsonSerializer.Serialize(value, s_jsonOptions);
            return JsonSerializer.Deserialize<T>(json, s_jsonOptions)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"分区 POCO {typeof(T).Name} 不支持 JSON 往返序列化。" +
                "请确保 POCO 无循环引用、无不支持序列化的属性类型（如 Stream、IntPtr）。", ex);
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "AppConfigStore 尚未初始化，请先调用 Initialize()。");
        }
    }

    private static AppConfigSchema LoadSchema()
    {
        try
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                return new AppConfigSchema();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfigSchema>(json, s_jsonOptions) ?? new AppConfigSchema();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppConfigStore] 配置文件损坏，将备份：{ex.Message}");
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string bakPath = path + ".corrupt.bak";
                    File.Move(path, bakPath, overwrite: true);
                    Debug.WriteLine($"[AppConfigStore] 损坏文件已备份为：{bakPath}");
                }
            }
            catch
            {
            }

            return new AppConfigSchema();
        }
    }

    private static void PopulateSnapshot(AppConfigSchema schema)
    {
        foreach (AppConfigSection section in Sections)
        {
            if (schema.Sections.TryGetValue(section.Key, out JsonElement element))
            {
                object? value = element.Deserialize(section.ConfigType, s_jsonOptions);
                Snapshot[section.Key] = value ?? section.DefaultValueFactory();
            }
            else
            {
                Snapshot[section.Key] = section.DefaultValueFactory();
            }
        }
    }

    private static void SaveSchemaWithNewValue<T>(string key, T value) where T : class, new()
    {
        var schema = new AppConfigSchema { SchemaVersion = CurrentSchemaVersion };

        foreach (KeyValuePair<string, object> kvp in Snapshot)
        {
            string sectionJson = JsonSerializer.Serialize(kvp.Value, kvp.Value.GetType(), s_jsonOptions);
            schema.Sections[kvp.Key] = JsonSerializer.Deserialize<JsonElement>(sectionJson, s_jsonOptions);
        }

        string newSectionJson = JsonSerializer.Serialize(value, typeof(T), s_jsonOptions);
        schema.Sections[key] = JsonSerializer.Deserialize<JsonElement>(newSectionJson, s_jsonOptions);

        SaveSchemaCore(schema);
    }

    private static void SaveSchemaCore(AppConfigSchema schema)
    {
        string path = GetConfigPath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = Path.Combine(directory!, $"logcat-config.tmp.{Guid.NewGuid():N}");
        string json = JsonSerializer.Serialize(schema, s_jsonOptions);

        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw new IOException("配置文件写入失败，请检查磁盘空间和文件权限。", ex);
        }
    }

    private static void SaveSchema(AppConfigSchema schema)
    {
        try
        {
            SaveSchemaCore(schema);
        }
        catch
        {
        }
    }

    private static void CleanupTempFiles()
    {
        try
        {
            string path = GetConfigPath();
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

            foreach (string tmpFile in Directory.GetFiles(directory, "logcat-config.tmp.*"))
            {
                try { File.Delete(tmpFile); } catch { }
            }
        }
        catch
        {
        }
    }

    private static void MigrateLegacySettingsFile(AppConfigSchema schema)
    {
        // ✅ 决策#19：本轮实施——从 Properties.Settings 读取旧值写入 schema
        // ⚠️ 过渡性代码：AppConfigStore 在 Models/ 引用 Properties.Settings
        // Properties 命名空间为 LogViewer.Properties，不违反依赖方向
        // 但 Models 层有了对 System.Configuration 的依赖——本轮可接受，后续删除 Migrate 时一并清理
        try
        {
            // ✅ 只在 schema 中不存在 NetworkLog 分区时才迁移——避免覆盖已有配置
            if (schema.Sections.ContainsKey("NetworkLog")) return;

            var legacy = Properties.Settings.Default;
            // ✅ 已删除 SettingsLoaded 守卫——ApplicationSettingsBase 无此属性（是事件而非bool）

            // ⚠️ [补充约束条件] Properties.Settings.Designer.cs 中 MaxBodySizeKb 的
            // DefaultSettingValueAttribute 是 50，但 Settings.settings 中是 500——两者不同步
            // 运行时未持久化的用户首次访问 MaxBodySizeKb 返回 50（Designer 优先级更高）
            // 此处 != 500 判断在用户从未修改过此值时也会为 true（因为实际值是 50）
            // 结论：hasAnyNonDefault 对 MaxBodySizeKb 总是 true——可接受，迁移无副作用

            bool hasAnyNonDefault = legacy.ServerPort != 9527
                || legacy.MaxLogEntriesPerDevice != 5000
                || legacy.MaxLogEntriesAll != 10000
                || legacy.MaxSystemLogEntries != 10000
                || legacy.AndroidQueueSize != 1000
                || legacy.MaxBodySizeKb != 500
                || !legacy.AutoAdbReverse
                || !legacy.AutoStartLogcat
                || !legacy.AutoFormatJson
                || legacy.FontSize != 11
                || !string.IsNullOrEmpty(legacy.LogcatFilter)
                || !string.IsNullOrEmpty(legacy.AdbPath);

            if (!hasAnyNonDefault) return;

            var migrated = new NetworkLogConfig
            {
                ServerPort = legacy.ServerPort,
                MaxLogEntriesPerDevice = legacy.MaxLogEntriesPerDevice,
                MaxLogEntriesAll = legacy.MaxLogEntriesAll,
                MaxSystemLogEntries = legacy.MaxSystemLogEntries,
                AndroidQueueSize = legacy.AndroidQueueSize,
                MaxBodySizeKb = legacy.MaxBodySizeKb,
                AutoAdbReverse = legacy.AutoAdbReverse,
                AutoStartLogcat = legacy.AutoStartLogcat,
                AutoFormatJson = legacy.AutoFormatJson,
                FontSize = legacy.FontSize,
                LogcatFilter = legacy.LogcatFilter ?? "",
                AdbPath = legacy.AdbPath ?? ""
            };

            string sectionJson = JsonSerializer.Serialize(migrated, s_jsonOptions);
            schema.Sections["NetworkLog"] = JsonSerializer.Deserialize<JsonElement>(sectionJson, s_jsonOptions);
            Debug.WriteLine("[AppConfigStore] 已从 Properties.Settings 迁移配置");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppConfigStore] 迁移 Properties.Settings 失败（忽略）：{ex.Message}");
        }
    }

    private static string GetConfigPath()
    {
        return Path.Combine(GetConfigDirectory(), FileName);
    }
}
```

### Step 2：构建验证

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

---

## Task 2：创建测试基础设施 + 配置存储测试

✅ 已纠正：项目当前无测试框架，需先创建独立测试项目再写测试。

### 文件清单

| 操作 | 路径 |
|------|------|
| 新增 | `LogViewer.Tests/LogViewer.Tests.csproj` |
| 新增 | `LogViewer.Tests/AppConfigStoreTests.cs` |

### Step 0：创建独立测试项目 + 添加 xUnit

文件：`LogViewer.Tests/LogViewer.Tests.csproj`

✅ 已修复：主项目 csproj 添加 InternalsVisibleTo，测试项目可访问 internal 类。

**主项目 `LogViewer.csproj` 需添加**：
```xml
<ItemGroup>
  <InternalsVisibleTo Include="LogViewer.Tests" />
</ItemGroup>
```

> 说明：`AppConfigSchema`/`AppConfigSection` 是 `internal`，`AppConfigStore.ResetForTesting()` 是 `internal`——缺 InternalsVisibleTo 测试项目编译失败。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <!-- ✅ 已纠正：与主项目一致，避免 WinForms 类型引用失败 -->
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\LogViewer\LogViewer.csproj" />
  </ItemGroup>
</Project>
```

```bash
rtk dotnet restore .\LogViewer.Tests\LogViewer.Tests.csproj
```

### Step 1：创建 AppConfigStoreTests

文件：`LogViewer.Tests/AppConfigStoreTests.cs`

<!-- ✅ 已采纳临时目录注入方案，旧批注已过时，保留供参考 -->

```csharp
using LogViewer.Models;
using Xunit;

namespace LogViewer.Tests;

[Collection("AppConfigStore")]
public sealed class AppConfigStoreTests : IDisposable
{
    private sealed class TestConfig
    {
        public string Name { get; set; } = "Default";
        public int Value { get; set; } = 42;
    }

    private readonly string _testKey;
    private readonly string _testDir;

    public AppConfigStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"AppConfigStoreTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        AppConfigStore.ResetForTesting(_testDir);
        _testKey = $"Test_{Guid.NewGuid():N}";
    }

    public void Dispose()
    {
        AppConfigStore.ResetForTesting();
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Get_BeforeInitialize_Throws()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        Assert.Throws<InvalidOperationException>(
            () => AppConfigStore.Get<TestConfig>(_testKey));
    }

    [Fact]
    public void Get_AfterInitialize_ReturnsDefaultValue()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        AppConfigStore.Initialize();

        TestConfig config = AppConfigStore.Get<TestConfig>(_testKey);
        Assert.Equal("Default", config.Name);
        Assert.Equal(42, config.Value);
    }

    [Fact]
    public void Set_ThenGet_ReturnsNewValue()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        AppConfigStore.Initialize();

        AppConfigStore.Set(_testKey, new TestConfig { Name = "Updated", Value = 99 });
        TestConfig config = AppConfigStore.Get<TestConfig>(_testKey);
        Assert.Equal("Updated", config.Name);
        Assert.Equal(99, config.Value);
    }

    [Fact]
    public void Get_UnregisteredKey_Throws()
    {
        AppConfigStore.Initialize();
        Assert.Throws<InvalidOperationException>(
            () => AppConfigStore.Get<TestConfig>("NonExistent"));
    }

    [Fact]
    public void RegisterSection_DuplicateKey_Throws()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        Assert.Throws<InvalidOperationException>(
            () => AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig()));
    }

    [Fact]
    public void ConfigChanged_IsRaisedWithCorrectArgs()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        AppConfigStore.Initialize();

        AppConfigChangedEventArgs? captured = null;
        AppConfigStore.ConfigChanged += handler;
        try
        {
            var newValue = new TestConfig { Name = "Event", Value = 77 };
            AppConfigStore.Set(_testKey, newValue);

            Assert.NotNull(captured);
            Assert.Equal(_testKey, captured!.SectionKey);
            Assert.NotNull(captured.OldValue);
            Assert.Equal("Event", ((TestConfig)captured.NewValue).Name);
        }
        finally
        {
            AppConfigStore.ConfigChanged -= handler;
        }

        void handler(object? sender, AppConfigChangedEventArgs e) => captured = e;
    }

    [Fact]
    public void Get_DeepCopy_ModifyingReturnedValueDoesNotAffectStore()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        AppConfigStore.Initialize();

        TestConfig first = AppConfigStore.Get<TestConfig>(_testKey);
        first.Name = "Mutated";

        TestConfig second = AppConfigStore.Get<TestConfig>(_testKey);
        Assert.Equal("Default", second.Name);
    }

    [Fact]
    public void Set_DeepCopy_ModifyingOriginalDoesNotAffectStore()
    {
        AppConfigStore.RegisterSection<TestConfig>(_testKey, () => new TestConfig());
        AppConfigStore.Initialize();

        var original = new TestConfig { Name = "Original", Value = 1 };
        AppConfigStore.Set(_testKey, original);

        original.Name = "Mutated";

        TestConfig fromStore = AppConfigStore.Get<TestConfig>(_testKey);
        Assert.Equal("Original", fromStore.Name);
    }
}
```

### Step 2：构建验证 + 测试运行

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
rtk dotnet build .\LogViewer.Tests\LogViewer.Tests.csproj
rtk dotnet test .\LogViewer.Tests\LogViewer.Tests.csproj
```

---

## Task 3：迁移 AppSettings 到 AppConfigStore

### 文件清单

| 操作 | 路径 |
|------|------|
| 新增 | `Models/NetworkLogConfig.cs` |
| 修改 | `Models/AppSettings.cs` |

### Step 1：创建 NetworkLogConfig

文件：`LogViewer/Models/NetworkLogConfig.cs`

✅ 已纠正：字段名与 AppSettings 保持一致，补充遗漏字段。

```csharp
namespace LogViewer.Models;

public sealed class NetworkLogConfig
{
    public int ServerPort { get; set; } = 9527;
    public int MaxLogEntriesPerDevice { get; set; } = 5000;
    public int MaxLogEntriesAll { get; set; } = 10000;
    public int MaxSystemLogEntries { get; set; } = 10000;
    public bool AutoAdbReverse { get; set; } = true;
    public bool AutoStartLogcat { get; set; } = true;
    public bool AutoFormatJson { get; set; } = true;
    public int FontSize { get; set; } = 11;
    public string LogcatFilter { get; set; } = "";
    public string AdbPath { get; set; } = "";
    public int AndroidQueueSize { get; set; } = 1000;
    public int MaxBodySizeKb { get; set; } = 500;
}
```

### Step 2：修改 AppSettings — 完整改造方案

✅ 已采纳方案C：注册分区职责移到 MainForm（UI 层），AppSettings.Load() 不再注册分区、不再引用 Utils 层，消除 Models → Utils 依赖。

改造后的 AppSettings.cs 语义：
- `Load()` → 仅从 AppConfigStore 获取配置（假设 MainForm 已注册分区并初始化）
- `Save()` → AppConfigStore.Set 写入新值（write-through，失败时回滚本地字段）
- AppSettings 字段保留为本地缓存（属性直接读写），但持久化委托 AppConfigStore
- SettingsDialog 和 MainForm 调用方式不变（仍通过 AppSettings 实例），AppSettings 内部委托 AppConfigStore
- ✅ AppSettings.cs 不再 `using LogViewer.Utils;`，依赖方向 Models → Models 合规

```csharp
using System.Diagnostics;

namespace LogViewer.Models;

public class AppSettings
{
    // ✅ 已修复：string 属性加 `= ""` 默认值，避免 Nullable(enable) 警告
    public int ServerPort { get; set; } = 9527;
    public int MaxLogEntriesPerDevice { get; set; } = 5000;
    public int MaxLogEntriesAll { get; set; } = 10000;
    public int MaxSystemLogEntries { get; set; } = 10000;
    public bool AutoAdbReverse { get; set; } = true;
    public bool AutoStartLogcat { get; set; } = true;
    public bool AutoFormatJson { get; set; } = true;
    public int FontSize { get; set; } = 11;
    public string LogcatFilter { get; set; } = "";
    public int AndroidQueueSize { get; set; } = 1000;
    public int MaxBodySizeKb { get; set; } = 500;
    public string AdbPath { get; set; } = "";

    private NetworkLogConfig? _lastSavedConfig;

    // ✅ Load() 不再注册分区——由 MainForm 构造时注册
    public static AppSettings Load()
    {
        try
        {
            NetworkLogConfig config = AppConfigStore.Get<NetworkLogConfig>("NetworkLog");
            var settings = FromConfig(config);
            settings._lastSavedConfig = config;
            return settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppSettings] 配置加载失败，使用默认设置：{ex.Message}");
            var defaults = FromConfig(new NetworkLogConfig());
            defaults._lastSavedConfig = new NetworkLogConfig();
            return defaults;
        }
    }

    public void Save()
    {
        NetworkLogConfig newConfig = ToConfig(this);
        try
        {
            AppConfigStore.Set("NetworkLog", newConfig);
            _lastSavedConfig = newConfig;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[AppSettings] 设置持久化失败：{ex.Message}");
            if (_lastSavedConfig is not null)
            {
                AppSettings rollback = FromConfig(_lastSavedConfig);
                ServerPort = rollback.ServerPort;
                MaxLogEntriesPerDevice = rollback.MaxLogEntriesPerDevice;
                MaxLogEntriesAll = rollback.MaxLogEntriesAll;
                MaxSystemLogEntries = rollback.MaxSystemLogEntries;
                AutoAdbReverse = rollback.AutoAdbReverse;
                AutoStartLogcat = rollback.AutoStartLogcat;
                AutoFormatJson = rollback.AutoFormatJson;
                FontSize = rollback.FontSize;
                LogcatFilter = rollback.LogcatFilter;
                AdbPath = rollback.AdbPath;
                AndroidQueueSize = rollback.AndroidQueueSize;
                MaxBodySizeKb = rollback.MaxBodySizeKb;
            }
        }
    }

    private static AppSettings FromConfig(NetworkLogConfig c) => new()
    {
        ServerPort = c.ServerPort,
        MaxLogEntriesPerDevice = c.MaxLogEntriesPerDevice,
        MaxLogEntriesAll = c.MaxLogEntriesAll,
        MaxSystemLogEntries = c.MaxSystemLogEntries,
        AutoAdbReverse = c.AutoAdbReverse,
        AutoStartLogcat = c.AutoStartLogcat,
        AutoFormatJson = c.AutoFormatJson,
        FontSize = c.FontSize,
        LogcatFilter = c.LogcatFilter,
        AdbPath = c.AdbPath,
        AndroidQueueSize = c.AndroidQueueSize,
        MaxBodySizeKb = c.MaxBodySizeKb
    };

    private static NetworkLogConfig ToConfig(AppSettings s) => new()
    {
        ServerPort = s.ServerPort,
        MaxLogEntriesPerDevice = s.MaxLogEntriesPerDevice,
        MaxLogEntriesAll = s.MaxLogEntriesAll,
        MaxSystemLogEntries = s.MaxSystemLogEntries,
        AutoAdbReverse = s.AutoAdbReverse,
        AutoStartLogcat = s.AutoStartLogcat,
        AutoFormatJson = s.AutoFormatJson,
        FontSize = s.FontSize,
        LogcatFilter = s.LogcatFilter,
        AdbPath = s.AdbPath,
        AndroidQueueSize = s.AndroidQueueSize,
        MaxBodySizeKb = s.MaxBodySizeKb
    };
}
```

**MainForm.cs 改动点**：

✅ 已采纳方案C + 方案B（删掉多余 Load）：

1. **92行改为**：先注册分区 + 初始化，再 Load
```csharp
// MainForm 构造函数中（原 92 行附近）
AppConfigStore.RegisterSection<NetworkLogConfig>(
    "NetworkLog", () => new NetworkLogConfig(), AppDesignTimeHelper.IsDesignTime);
AppConfigStore.Initialize();
_settings = AppSettings.Load();
```

2. **1052行删除** `_settings = AppSettings.Load();` — SettingsDialog 修改的就是传入的 `_settings` 引用，Save() 后字段已是新值，无需重新 Load。ApplySettings() 等后续调用直接用 `_settings` 即可。

### Step 3：构建验证

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

### Step 4：手动 smoke — 设置切换与持久化

> **✅ [画流程图解释] 采纳后完整数据流路径**：
>
> ```
> ┌──────────────────────────── 启动阶段 ──────────────────────────────────┐
> │                                                                        │
> │  MainForm() 构造                                                       │
> │    ├─► AppConfigStore.RegisterSection("NetworkLog", ..., IsDesignTime)  │
> │    ├─► AppConfigStore.Initialize()                                      │
> │    │    ├─► LoadSchema() ← %LOCALAPPDATA%\LogViewer\...          │
> │    │    ├─► MigrateLegacySettingsFile(schema)                           │
> │    │    │    └─► Properties.Settings → schema（仅 schema 无 NetworkLog 时）│
> │    │    ├─► PopulateSnapshot(schema) → 内存 Snapshot                     │
> │    │    └─► SaveSchema(schema) → 写 logcat-config.json                  │
> │    └─► AppSettings.Load() ← Get("NetworkLog")                          │
> │         └─► DeepClone(Snapshot["NetworkLog"]) → AppSettings 实例 ✅     │
> └────────────────────────────────────────────────────────────────────────┘
>
> ┌──────────────────────────── 设置修改阶段 ───────────────────────────────┐
> │                                                                         │
> │  SettingsDialog.OnOkClick()                                             │
> │    ├─► _settings.ServerPort = 9999  （直接修改 AppSettings 字段）        │
> │    └─► _settings.Save()                                                 │
> │         ├─► ToConfig(this) → NetworkLogConfig { ServerPort=9999 }       │
> │         ├─► AppConfigStore.Set("NetworkLog", newConfig)                 │
> │         │    ├─► DeepClone(newConfig) → clonedValue                      │
> │         │    ├─► DeepClone(Snapshot[key]) → oldValue                     │
> │         │    ├─► SaveSchemaWithNewValue(key, clonedValue)                │
> │         │    │    └─► 序列化所有分区 → 写临时文件 → Move 覆盖            │
> │         │    ├─► Snapshot[key] = clonedValue  ← 仅成功后才执行           │
> │         │    └─► 锁外: ConfigChanged?.Invoke(...)                        │
> │         └─► _lastSavedConfig = newConfig                                 │
> │                                                                         │
> │  ✅ MainForm:1052 不再重新 Load()——_settings 已是最新值                  │
> │     直接 ApplySettings() 即可                                            │
> └─────────────────────────────────────────────────────────────────────────┘
>
> ┌──────────────────────────── 重启验证阶段 ───────────────────────────────┐
> │                                                                         │
> │  重新启动 → MainForm 构造                                                │
> │    ├─► RegisterSection + Initialize                                     │
> │    │    ├─► LoadSchema() ← %LOCALAPPDATA%\...\logcat-config.json        │
> │    │    ├─► MigrateLegacy（schema 含 NetworkLog → 跳过迁移 ✅）           │
> │    │    ├─► PopulateSnapshot → Snapshot["NetworkLog"] = 已保存值          │
> │    └─► AppSettings.Load() → Get → 返回上次保存的配置 ✅                  │
> └─────────────────────────────────────────────────────────────────────────┘
> ```

```bash
rtk dotnet run --project .\LogViewer
```

验证：修改设置后关闭重启，设置值被保留。

---

## Task 4：文档更新

### 文件清单

| 操作 | 路径 |
|------|------|
| 修改 | `.ai/agents/MEMORY.md` |
| 修改 | `.ai/agents/directory-tree.md` |
| 修改 | `.ai/agents/tech-stack.md` |

### Step 1：更新 MEMORY.md

在「关键文件位置」新增 AppConfigStore + AppDesignTimeHelper 表格；在「技术决策记录」新增相关决策；在「协议踩坑」补充 JSON 反序列化必须 PropertyNameCaseInsensitive。

### Step 2：更新 directory-tree.md

在 `Models/` 下新增 `AppConfigStore.cs` / `AppConfigSchema.cs` / `AppConfigSection.cs` / `AppConfigChangedEventArgs.cs` / `NetworkLogConfig.cs`；在 `Utils/` 下新增 `AppDesignTimeHelper.cs`。

### Step 3：更新 tech-stack.md

在「核心模块」表新增 AppConfigStore + AppDesignTimeHelper 行。

### Step 4：构建验证

```bash
rtk dotnet build .\LogViewer\LogViewer.csproj
```

---

## 已确认决策摘要

| # | 决策 | 结论 |
|---|------|------|
| 1 | 存储模型 | 单文件 logcat-config.json + 分区字典 |
| 2 | Get/Set 深拷贝 | 双向深拷贝（JSON 往返） |
| 3 | Set 持久化模式 | write-through：锁内先写磁盘再更新内存 |
| 4 | 持久化失败策略 | 抛 IOException，内存快照不变 |
| 5 | 事件模型 | 单事件 ConfigChanged + SectionKey 过滤，锁外触发 ⚠️可在任意线程，UI订阅者需BeginInvoke |
| 6 | AppConfigStore | static 类 |
| 7 | 设计时检测 | ✅ `Func<bool>` 委托注入（UI层注册时传入 AppDesignTimeHelper.IsDesignTime），消除 Models→Utils 依赖 |
| 8 | AppSettings 兼容 | 改为委托 AppConfigStore，Properties.Settings 逐步弃用 |
| 9 | 分区注册位置 | ✅ MainForm 构造时注册（原"AppSettings内部注册"已纠正——避免 Models→Utils 依赖） |
| 10 | 临时文件名 | Guid.NewGuid() |
| 11 | 损坏文件处理 | rename .corrupt.bak + Debug.WriteLine |
| 12 | NetworkLogConfig 位置 | 放 Models/ 下（模块自治） |
| 13 | RegisterSection 守卫 | Initialize 后注册抛 InvalidOperationException |
| 14 | ResetForTesting 事件清理 | ✅ 改用 `GetInvocationList()` 逐个移除订阅者 |
| 15 | 测试隔离 | [Collection("AppConfigStore")] + 临时目录注入避免写生产配置 |
| 16 | 设计时 Set 语义 | 更新 Snapshot 但跳过持久化 |
| 17 | 设置保存失败语义 | ✅ write-through：AppConfigStore.Set 失败→内存不变；AppSettings.Save 失败→回滚本地字段 |
| 18 | EnsureInitialized 异常回退 | ✅ Initialize catch 中设 `_initialized = true`，回退填充默认分区值 |
| 19 | MigrateLegacySettingsFile | ✅ 本轮实施——仅 schema 无 NetworkLog 时迁移（避免覆盖已有配置） |
| 20 | Set 无相等性检查 | 由调用者负责短路 |
| 21 | 配置文件路径 | ✅ `%LOCALAPPDATA%\LogViewer\logcat-config.json`（原 c:\ 无写入权限已纠正） |
| 22 | MainForm:1052 多余 Load | ✅ 删除 `_settings = AppSettings.Load()`（SettingsDialog 修改同一实例，Save 后无需重载） |
| 23 | _lastSavedConfig 初始化 | ✅ Load() 成功后立即赋值，确保首次 Save 失败时能回滚 |
| 24 | 测试项目框架 | ✅ net8.0-windows（与主项目一致） |
| 25 | InternalsVisibleTo | ✅ 主项目 csproj 添加 InternalsVisibleTo("LogViewer.Tests") |
| 26 | SettingsLoaded 守卫 | ✅ 已删除——ApplicationSettingsBase.SettingsLoaded 是事件不是属性 |
| 27 | AppSettings string 默认值 | ✅ 所有 string 属性加 `= ""` 避免 Nullable 警告 |

## 已知限制

- ConfigChangedEventArgs.OldValue/NewValue 为 `object` 弱类型，订阅者需手动转型
- Set 不做相等性检查，调用者应在业务层短路
- ConfigChanged 锁外触发，多线程并发 Set 时事件顺序可能反转
- ConfigChanged 可在任意线程触发，UI订阅者必须使用 BeginInvoke
- DeepClone 依赖 JSON 往返，含循环引用/不可序列化属性的 POCO 会抛异常
- SaveSchemaWithNewValue 仅保留已注册分区，未注册分区在首次 Set 时被清理
- AppConfigSchema 每次 Set 全量序列化所有分区再写文件——当前分区少（1-3个）性能可忽略，未来分区10+时频繁 Set 可能成为瓶颈
- Initialize 假设只在 UI 线程调用（lock 内二次检查保安全，WinForms 场景下实际无风险）
- Properties.Settings 文件本轮保留（MigrateLegacySettingsFile 需要读取），下轮删除 Settings.settings/Settings.Designer.cs
