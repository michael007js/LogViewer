---
name: "dotnet-guidelines"
description: ".NET / C# 编码规范。Invoke when working on android.log.client .NET 8 / net8.0-windows / C# 12 code and need the project coding rules for language features, nullability, async, exceptions, logging, data access, testing, security, and review."
version: "1.0.0"
updatedAt: "2026-06-01"
tags: [C#, .NET, 编码规范, 异步, 异常, 日志, 测试, 安全, Nullable]
---

# android.log.client 编码规范（.NET 8 / net8.0-windows / C# 12）

> **目标 SDK**: 以仓库 `global.json` 当前 `10.0.100` / `latestFeature` 为基线
> **目标框架**: `net8.0-windows`
> **语言版本**: `C# 12`（随 `.NET 8` 默认启用）
> **适用范围**: android.log.client 新增代码、重构代码、迁移代码
> **基线原则**: 优先使用 SDK 自带能力、可空引用、现代异步、结构化日志与分析器

调用本技能时，必须同时加载 `rtk` 公共前置技能，不得跳过。`rtk` 负责提供本仓库统一的 shell 命令规范、文件读取策略、工作目录约束、验证方式和项目内执行习惯；不先加载 `rtk`，后续搜索、校验和命令风格就容易偏离仓库约定。

## 快速导航

- [版本基线与默认配置](#sec-0-version)
- [命名规范](#sec-1-naming)
- [格式化与文件组织](#sec-2-formatting)
- [语言特性与类型设计](#sec-3-language)
- [Null 与参数校验](#sec-4-null)
- [异常处理与日志](#sec-5-exception)
- [异步、并发与资源释放](#sec-6-async)
- [数据访问、序列化与配置](#sec-7-data)
- [测试规范](#sec-8-test)
- [安全与性能](#sec-9-security)
- [工具、分析器与 CI](#sec-10-tooling)
- [代码审查检查清单](#sec-11-review)
- [参考资源](#sec-12-reference)

---

<a id="sec-0-version"></a>
## 零、版本基线与默认配置（基于仓库当前 net8.0-windows 基线）

### 0.0 规则等级说明

为避免把"建议"写成"命令"，本文档统一按以下三层理解：

| 等级 | 含义 | 落地方式 |
|------|------|----------|
| **必须遵守** | 与正确性、安全性、兼容性、可维护性或团队统一性直接相关；默认阻塞合并 | 编译、分析器、测试、CI、Code Review |
| **推荐遵守** | 默认采用的写法；如有充分理由可偏离，但应在 PR 中说明 | Code Review、格式化工具、团队约定 |
| **背景说明** | 解释原因、边界和例外，帮助理解规范，不直接作为阻塞项 | 文档说明、设计讨论 |

### 0.1 项目版本基线

**必须遵守**

- 仓库 SDK 基线以 `global.json` 当前配置为准：`10.0.100` + `latestFeature`。
- 目标框架以项目文件为准；当前主项目使用 `net8.0-windows`。
- 默认启用 `Nullable` 与 `ImplicitUsings`。
- 除非有明确兼容性原因，不单独设置 `LangVersion`；如必须设置，只允许 `12.0`，禁止 `latest`、`latestMajor`、`preview`。
- CI 必须启用 .NET SDK 自带分析器，并对代码风格和质量规则进行构建期校验。

**推荐遵守**

- `global.json` 应与仓库真实基线保持一致，不在文档或脚本中伪造更高的 patch 版本。
- 项目统一通过 `Directory.Build.props` 管理分析器、警告级别和公共构建属性。
- 新建项目默认启用 `EnforceCodeStyleInBuild`；CI 中建议启用 `TreatWarningsAsErrors`。

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AnalysisLevel>8.0-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

**背景说明**

- `.NET 8.x` 默认对应 `C# 12`。不要为了"用最新语法"把 `LangVersion` 改成 `latest`，这会让不同机器的编译结果不稳定。
- 对于 .NET 5+ 项目，SDK 已内置分析器；通常不需要额外安装 `Microsoft.CodeAnalysis.NetAnalyzers`，除非你明确希望把规则更新和 SDK 更新解耦。

### 0.2 现代 C# / .NET 特性使用准则

| 特性 | 规则 | 示例 |
|------|------|------|
| **file-scoped namespace** | 单文件单命名空间时默认使用 | `namespace android.log.client.Sdk.NetworkSdk.Core;` |
| **nullable reference types** | 必须启用，用 `?` 表达"确实可空" | `string? remark` |
| **required / init** | DTO、配置对象、不可变输入模型优先使用 | `public required string Name { get; init; }` |
| **collection expressions** | 目标类型明确且可读性更好时优先使用 | `int[] values = [1, 2, 3];` |
| **raw string literals** | 多行 JSON、SQL、正则、模板文本优先使用 | `var sql = """SELECT 1""";` |
| **pattern matching / switch 表达式** | 可读性优于多层 `if` / 类型转换时优先使用 | `return state switch { ... };` |
| **using 声明 / await using** | 生命周期清晰时优先使用 | `await using var stream = ...;` |
| **primary constructors** | 小型、依赖明确、无复杂初始化逻辑的类型可用 | `public sealed class UserService(IUserRepo repo)` |
| **更高语言版本特性** | 若未来仓库升级语言版本，再评估是否引入 `field` 属性等新语法；当前 `net8.0-windows` 基线默认不依赖这类特性 | 以项目文件和 SDK 实际锁定为准 |
| **扩展成员等新语法** | 仅在未来语言版本升级且团队统一后再考虑；当前默认不引入 | 见官方文档 |
| **Span<T> / ReadOnlySpan<T>** | 仅在性能热点且经过验证时使用 | 解析、编码、协议处理 |

### 0.3 禁止项与限制项

**必须遵守**

- 禁止通过关闭 `Nullable`、大面积使用 `!`、`#nullable disable` 来规避警告。
- 禁止把 `dynamic`、反射、`unsafe` 作为常规实现手段；只有在性能、互操作或框架限制下才允许引入，并必须说明原因。
- 禁止在公共 API 中为了追求"零分配"滥用 `ref struct`、`Span<T>`，导致调用方可用性下降。
- 禁止仅因"语法更新"而机械性重写稳定代码。

**推荐遵守**

- 优先使用 BCL 与 SDK 自带能力；新增第三方包前先确认标准库是否已覆盖需求。
- 新语法以"提高可读性"为前提，避免为了炫技降低团队可维护性。

---

<a id="sec-1-naming"></a>
## 一、命名规范（Naming Conventions）

### 1.1 联动原则

- 本节只保留命名规则摘要，**详细命名规范以 [`../donet-naming/SKILL.md`](../donet-naming/SKILL.md) 为准**。
- 当本文件与 [`../donet-naming/SKILL.md`](../donet-naming/SKILL.md) 出现冲突时，以 `donet-naming` 中的定义为准。
- 后续如果需要调整命名规则，先更新 [`../donet-naming/SKILL.md`](../donet-naming/SKILL.md)，再回到本文件同步摘要或示例。

### 1.2 最小命名摘要

| 场景 | 规则 | 示例 |
|------|------|------|
| **命名空间** | PascalCase | `android.log.client.Sdk.NetworkSdk.Core` |
| **类型 / 接口 / 枚举** | PascalCase；接口加 `I` 前缀 | `INetworkClient`、`UserDto` |
| **公开成员** | PascalCase | `GetByIdAsync`、`ConnectionString` |
| **私有字段** | `_camelCase` | `_logger`、`_networkClient` |
| **局部变量 / 参数** | camelCase | `userId`、`retryCount` |
| **异步方法** | `Async` 后缀 | `LoadAsync`、`SaveAsync` |
| **布尔值** | `is` / `has` / `can` / `should` 前缀 | `isEnabled` |
| **集合** | 复数名 | `users`、`roleNames` |
| **配置 / DTO / 契约 / 事件** | `Options` / `Dto` / `Request` / `Response` / `Event` 后缀 | `NetworkClientOptions`、`UserCreatedEvent` |
| **异常 / 扩展类** | `Exception` / `Extensions` 后缀 | `MySqlSdkException`、`NetworkServiceCollectionExtensions` |

### 1.3 在本规范中的使用约定

- 本文件中的类名、方法名、属性名、字段名、参数名和测试名，都必须遵循 [`../donet-naming/SKILL.md`](../donet-naming/SKILL.md)。
- 涉及主构造函数、record positional 参数、集合表达式变量命名，以及未来更高语言版本特性的命名约束时，不在本文件重复展开，直接按 [`../donet-naming/SKILL.md`](../donet-naming/SKILL.md) 执行。
- 命名相关审查项默认包括：`Async` 后缀、`TryXxx` 约定、`Options` / `Dto` / `Event` 后缀、`_camelCase` 私有字段、PascalCase 文件名和测试命名。

---

<a id="sec-2-formatting"></a>
## 二、格式化与文件组织（Formatting）

### 2.1 缩进、括号与命名空间

**必须遵守**

- 使用 **4 个空格** 缩进，不使用 Tab。
- 使用 **Allman 风格**大括号。
- 单文件单命名空间默认使用 **file-scoped namespace**。
- 文件末尾保留一个换行符。

```csharp
namespace android.log.client.Sdk.NetworkSdk.Core;

public sealed class NetworkClient
{
    public async Task<string?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _repository.GetAsync(id, cancellationToken);
    }
}
```

### 2.2 using 语句

**必须遵守**

- 删除未使用的 `using`。
- 不在文件中重复声明 `ImplicitUsings` 已覆盖的命名空间。
- 仅在确有必要时使用 `global using`，并集中管理。

**推荐遵守**

- 非隐式 `using` 按 `System.*`、第三方、项目内命名空间分组，并按字母序排序。

### 2.3 空行、行宽与表达式体成员

**推荐遵守**

- 方法之间保留一个空行。
- 每行尽量不超过 **120** 列；超长时按表达式边界换行。
- 表达式体成员只用于逻辑足够简单的一行成员。

```csharp
public string GetDisplayName() => $"{Code}-{Name}";

public decimal CalculateTotal(decimal unitPrice, int quantity, decimal discount)
{
    return (unitPrice * quantity) - discount;
}
```

### 2.4 注释规则

**必须遵守**

- 新增或修改的公开类型、接口、公开方法、公开属性、异常类型，以及对外契约 `Options` / `Dto` / `Request` / `Response` / `Event`，默认必须补 XML 注释；如果签名本身不足以表达前置条件、单位、范围、回退行为或兼容约束，则视为阻塞项。
- 复杂协议、状态机、兼容分支、降级 / 回退逻辑、边界条件、缓存键与缓存层级、并发互斥、重试、非直观性能优化、Magic Number、关键变量 / 常量 / 枚举 / 局部代码块，必须补维护向注释，说明"为什么这样做、不能随意改什么、失败时会怎样"。
- 参数含义、返回值语义、可空约定、线程 / 异步约束、取值范围、单位、失败条件与回退行为，只要不能从命名直接看出，就必须在 XML 注释或紧邻实现的注释中写清楚。
- 代码改动导致原注释失真、过时、与实现不一致时，必须在同一次提交中同步修正或删除；禁止把"代码先改、注释以后补"视为可接受流程。
- `TODO` / `FIXME` / `HACK` 必须带追踪信息，如工单号、原因、计划清理时间。
- 禁止用空泛注释凑数量，例如把方法名直译成中文、逐行复述 `if` / `for` / 赋值动作，或写成对维护者没有信息增量的模板句。
- 禁止保留大段注释掉的旧代码。

**推荐遵守**

- 优先写"为什么"，而不是重复代码本身"做了什么"。
- 简短说明用 `//`，公开成员文档用 XML 注释。
- 默认使用简体中文；类型名、协议名、命令名、环境变量名、异常类型名保留英文技术名。
- 对显而易见且没有额外约束的私有样板代码，可以不注释；注释密度应集中在调用方容易误用、维护者容易误判的地方。

```csharp
// TODO(CS5-218, 2026-06): 上游协议升级后移除此兼容分支。
```

---

<a id="sec-3-language"></a>
## 三、语言特性与类型设计

### 3.1 类型选择

| 类型 | 适用场景 | 规则 |
|------|----------|------|
| **class** | 有标识、可变状态、服务对象、复杂生命周期 | 默认首选 |
| **record class** | 不可变 DTO、命令、事件、响应模型 | 优先用于值语义模型 |
| **readonly struct / readonly record struct** | 小型、不可变、值语义明显的数据 | 仅在明确收益时使用 |
| **enum** | 有限离散值 | 禁止用 magic number 代替 |

**推荐遵守**

- 服务类默认 `sealed`，除非明确需要继承。
- `partial` 仅用于生成代码、源生成器配合、或清晰的职责拆分（如 WinForms 窗体的 `Form.cs` + `Form.Designer.cs`）。
- 返回集合时优先暴露 `IReadOnlyList<T>`、`IReadOnlyDictionary<TKey, TValue>` 或 `IEnumerable<T>`，避免泄露可变内部集合。

### 3.2 不可变性与初始化

**必须遵守**

- 能表达为不可变对象时，不要设计成可变对象。
- 对必须初始化的属性，优先使用 `required`、构造函数或工厂方法明确表达。
- DTO、配置对象、消息模型优先使用 `init` 或主构造函数，而不是公开可写 setter。

```csharp
public sealed record CreateUserRequest
{
    public required string UserName { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
}
```

### 3.3 主构造函数、Record 与现代语法

**推荐遵守**

- 依赖数量少、初始化逻辑简单的服务类可以使用主构造函数。
- 当构造函数里需要复杂校验、条件分支、初始化副作用时，改用普通构造函数。
- `record` 带来的值相等语义必须是你真正想要的；不要因为语法简洁就在实体类上滥用。

```csharp
public sealed class TokenService(TimeProvider timeProvider)
{
    public DateTimeOffset GetNow() => timeProvider.GetUtcNow();
}
```

### 3.4 日期时间与领域类型

**推荐遵守**

- 表示"时间点"默认使用 `DateTimeOffset`，避免时区歧义。
- 仅表示日期时使用 `DateOnly`，仅表示时刻时使用 `TimeOnly`。
- 涉及当前时间的业务逻辑优先注入 `TimeProvider`，避免直接调用 `DateTime.Now` / `DateTime.UtcNow`，提高可测试性。

### 3.5 集合表达式与模式匹配

**推荐遵守**

- 集合表达式仅在目标类型明确、没有隐藏分配歧义时使用。
- 可读性优于 `if-else` 链时，优先使用 `switch` 表达式和模式匹配。

```csharp
List<string> roles = ["admin", "auditor"];

return status switch
{
    OrderStatus.Pending => "Pending",
    OrderStatus.Completed => "Completed",
    _ => "Unknown"
};
```

---

<a id="sec-4-null"></a>
## 四、Null 与参数校验

### 4.1 可空引用类型

**必须遵守**

- `Nullable` 必须保持启用。
- `string?`、`UserDto?` 这类类型声明代表"调用方需要处理空值"；不要把"暂时没改完"写成可空。
- 返回空集合而不是 `null` 集合。

### 4.2 参数校验与 Guard Clause

**必须遵守**

- 公共方法、边界层、反序列化入口必须显式校验参数。
- 优先使用 .NET 自带的 `ThrowIf...` Guard API。

```csharp
public static UserId Parse(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);

    return new UserId(value.Trim());
}
```

### 4.3 null 宽恕操作符 `!`

**必须遵守**

- `!` 只能用于你已能证明非空、但编译器暂时无法推断的场景。
- 若 `!` 的原因不直观，必须补一行短注释。

**禁止示例**

```csharp
var name = input!.Trim(); // 仅为压警告，未证明 input 非空
```

**可接受示例**

```csharp
// options 在宿主启动阶段已通过 ValidateOnStart 校验非空。
var connectionString = options.Value.ConnectionString!;
```

---

<a id="sec-5-exception"></a>
## 五、异常处理与日志

### 5.1 异常处理原则

**必须遵守**

- 只有在能够恢复、补充上下文、转换异常语义、或在边界层统一处理时才捕获异常。
- 保留原始调用栈，重新抛出时使用 `throw;`，不要使用 `throw ex;`。
- 使用最具体的异常类型，如 `ArgumentException`、`InvalidOperationException`、`TimeoutException`，不要一律 `Exception`。
- SDK 统一异常使用 `XxxSdkException`（如 `MySqlSdkException`），不暴露底层驱动异常给上层。

### 5.2 反模式

**禁止**

- 空 `catch`。
- 捕获 `Exception` 后直接返回默认值掩盖错误。
- 把异常当正常控制流。

```csharp
// 禁止
try
{
    return await repository.LoadAsync(id, cancellationToken);
}
catch
{
    return null;
}
```

### 5.3 结构化日志

**必须遵守**

- 统一使用结构化日志，不在日志模板中拼接长字符串。
- 日志里必须避免输出密码、令牌、连接串、完整身份证号、原始密钥等敏感信息。
- 热路径和高频日志优先使用 `[LoggerMessage]` 源生成日志。
- NetworkSdk / MySqlSdk 的日志脱敏规则必须遵守 SDK 内部约定。

```csharp
public static partial class Log
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Network request {RequestId} retry failed on attempt {Attempt}.")]
    public static partial void RequestRetryFailed(
        ILogger logger,
        string requestId,
        int attempt,
        Exception exception);
}
```

### 5.4 异常日志最小信息集

**推荐遵守**

- 操作名称
- 关键业务标识（如 `RequestId`、`UserId`）
- 结果状态
- `TraceId` / 相关链路标识
- 耗时
- 异常类型与消息

---

<a id="sec-6-async"></a>
## 六、异步、并发与资源释放

### 6.1 Async / Await 基本规则

**必须遵守**

- I/O 密集或潜在长耗时方法优先提供异步版本。
- 可取消的异步公开方法，`CancellationToken cancellationToken = default` 必须放在最后一个参数。
- `async void` 仅允许用于事件处理器。
- 禁止使用 `.Result`、`.Wait()`、`.GetAwaiter().GetResult()` 阻塞异步流程，除非在极少数同步入口且有明确注释。

```csharp
public async Task<UserDto?> GetAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
{
    return await repository.GetAsync(userId, cancellationToken);
}
```

### 6.2 `ValueTask` 与异步流

**推荐遵守**

- 只有在高频、热点、且经测量确认可以减少分配时才使用 `ValueTask`。
- 需要流式处理时优先考虑 `IAsyncEnumerable<T>`，不要把大结果集一次性读入内存。

### 6.3 资源释放

**必须遵守**

- 实现了 `IDisposable` 的对象必须使用 `using` / `using var`。
- 实现了 `IAsyncDisposable` 的对象必须使用 `await using`。
- 从池中租借的对象（如 `ArrayPool<T>`）必须在 `finally` 中归还。
- 自绘控件的旧 `Region` / `Pen` / `Brush` 必须 `Dispose` 后再赋新值。

```csharp
await using var stream = await storage.OpenWriteAsync(path, cancellationToken);
await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
```

### 6.4 并发控制

**必须遵守**

- `lock` 中禁止 `await`。
- 需要异步互斥时使用 `SemaphoreSlim`、`Channel<T>`、`AsyncLock` 等异步友好方案。
- 简单计数器、状态切换优先使用 `Interlocked`。
- 线程安全使用 `lock(Sync)` + 快照释放锁模式，避免 UI 死锁。

**推荐遵守**

- 并发共享状态越少越好；优先选择不可变快照、消息传递、分区处理。
- 使用 `ConcurrentDictionary` 前先确认是否真的需要并发写；只读场景优先不可变集合或预构建缓存。

### 6.5 `ConfigureAwait(false)` 的使用边界

**推荐遵守**

- 在 WinForms 桌面应用中，UI 线程代码不需要 `ConfigureAwait(false)`。
- 在可复用库代码（如 `Sdk/` 下的代码）中，应使用 `ConfigureAwait(false)` 避免捕获 UI 同步上下文。

---

<a id="sec-7-data"></a>
## 七、数据访问、序列化与配置

### 7.1 数据库访问

**必须遵守**

- 数据访问必须使用参数化查询，禁止字符串拼接 SQL。
- 连接晚打开、早释放；事务边界必须显式。
- I/O 方法优先使用异步 API，并向下传递 `CancellationToken`。
- 数据库访问统一通过 `MySqlSdk` 封装，不要在业务层散落 SQL 细节。

```csharp
const string sql = """
    SELECT id, name
    FROM users
    WHERE id = @Id;
    """;

var user = await mySqlClient.QuerySingleOrDefaultAsync<UserDto>(
    sql,
    new { Id = id },
    cancellationToken: cancellationToken);
```

**推荐遵守**

- 批量操作、N+1 查询、全表扫描风险必须在 PR 中说明或优化。
- MySqlSdk 集成测试使用临时表 `_sdk_test_temp`，不写入数据库正式表。

### 7.2 JSON 序列化

**推荐遵守**

- 默认使用 `System.Text.Json`；只有在兼容性、特性缺失、或历史负担明确存在时才考虑其他方案。
- `JsonSerializerOptions` 集中配置，不要到处 new。
- 对外契约模型不要直接暴露数据库实体。

### 7.3 配置管理

**必须遵守**

- 配置统一走 Options 模式或 `ConfigStoreSdk`，不直接在业务代码中散落读取原始配置键。
- Options 类必须具备默认值、必填项说明与启动期校验。
- 敏感配置不进入仓库，统一通过环境变量、Secret Manager、部署平台密钥服务或专用密钥库注入。
- MySqlSdk 测试连接字符串通过环境变量 `MYSQLSDK_TEST_CONNSTR` 配置，禁止硬编码。

```csharp
public sealed class MySqlSdkOptions
{
    public const string SectionName = "MySqlSdk";

    public required string ConnectionString { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
}
```

### 7.4 依赖注入

**必须遵守**

- 默认使用构造函数注入，禁止 Service Locator。
- 生命周期必须与依赖行为匹配；不要在单例中直接依赖作用域服务。
- DI 注册扩展方法命名采用 `AddXxx`（如 `AddNetworkClient`、`AddMySqlSdk`），集中放在各 SDK 的 `Extensions/` 目录。

**推荐遵守**

- 当前项目尚未引入 `HostBuilder` 或 DI 容器；当未来引入时，各 SDK 的 DI 扩展方法已预置就绪。

---

<a id="sec-8-test"></a>
## 八、测试规范

### 8.1 测试命名与结构

**推荐遵守**

- 测试项目命名为 `android.log.client.Tests`。
- 测试文件放 `Tests/` 目录，由测试项目通过 `<Compile Include>` 引用，不复制源文件到测试项目目录。
- 测试方法命名使用 `Method_State_Expected` 或 `Should_DoSomething_When_Condition`。
- 单个测试聚焦一个行为，遵循 AAA（Arrange / Act / Assert）。

### 8.2 可测试性

**必须遵守**

- 时间、随机数、外部 I/O、系统时钟、环境变量等可变依赖必须可替换。
- 涉及当前时间的业务逻辑优先依赖 `TimeProvider`。
- 异步测试必须真正 `await`，不能靠 `Thread.Sleep` 等待。
- static 管理器必须提供 `ResetForTesting()` 方法。

### 8.3 覆盖要求

**推荐遵守**

- 新增业务逻辑必须带单元测试。
- 异常路径、空值路径、边界条件、重试分支、取消分支至少覆盖关键路径。
- 集成测试只覆盖跨边界行为，不重复单元测试细节。

### 8.4 测试隔离

**必须遵守**

- 测试四重隔离：`[Collection]` 串行化 + `ResetForTesting` 清状态 + `Dispose` 清理 + Guid Key 防冲突。
- MySqlSdk 集成测试使用临时表 `_sdk_test_temp`。
- MySqlSdk 集成测试需本地 MySQL 实例；连接字符串通过环境变量 `MYSQLSDK_TEST_CONNSTR` 配置。

---

<a id="sec-9-security"></a>
## 九、安全与性能

### 9.1 安全规范

**必须遵守**

- 所有外部输入都视为不可信，必须做格式、长度、范围或白名单校验。
- 任何文件路径、URL、SQL、命令参数都必须经过边界校验或框架安全 API 处理。
- 日志、异常、监控事件中禁止泄露敏感数据。
- NetworkSdk 的日志脱敏规则必须遵守。

### 9.2 性能规范

**推荐遵守**

- 没有测量就不要做复杂优化。
- 普通业务代码可正常使用 LINQ；仅在热点循环、序列化、协议解析、批量处理等场景再考虑手写循环或 `Span<T>`。
- `ArrayPool<T>`、`Memory<T>`、`Span<T>`、`CollectionsMarshal` 等高级优化只在已定位瓶颈时使用。
- 机器可读格式化统一使用 `CultureInfo.InvariantCulture`，避免区域设置引发性能和正确性问题。

### 9.3 字符串与集合

**推荐遵守**

- 高频拼接、多步构建文本使用 `StringBuilder` 或更适合的 API。
- 小型固定集合可用集合表达式；对外返回集合时避免暴露内部可变容器。
- 需要只读高频查找时，可评估使用预计算缓存或不可变集合。

---

<a id="sec-10-tooling"></a>
## 十、工具、分析器与 CI

### 10.1 必备工具链

**必须遵守**

- 本地和 CI 至少执行：
    - `dotnet build`
    - `dotnet test`
    - `dotnet format --verify-no-changes`

### 10.2 分析器与格式化

**推荐遵守**

- 优先使用 SDK 自带分析器 + `.editorconfig` + `dotnet format` 作为第一层规范落地。
- 第三方分析器（StyleCop、Meziantou、Roslynator、xUnit Analyzer）按需引入，并在根目录统一配置。

```ini
[*.cs]
dotnet_sort_system_directives_first = true
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_when_type_is_apparent = true:suggestion
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.CA2000.severity = warning
dotnet_diagnostic.CA2016.severity = warning
```

### 10.3 PR 与 CI 要求

**必须遵守**

- 编译、测试、分析器必须通过后才能合并。
- `NoWarn`、`pragma warning disable`、规则降级必须说明原因和计划清理时间。
- 修改公共契约、序列化格式、数据库结构、并发行为时，必须补充测试或迁移说明。

---

<a id="sec-11-review"></a>
## 十一、代码审查检查清单（Code Review Checklist）

### 11.1 基础规范

- 是否符合 `net8.0-windows`、`Nullable`、`ImplicitUsings` 基线？
- 是否引入了不必要的第三方依赖或重复轮子？
- 命名、目录、文件拆分是否清晰？
- 新增文件是否放对了目录（`Sdk/`、`Components/Atomic/`、`Components/Composite/`、`Tests/`）？

### 11.2 正确性与可维护性

- 空值、边界条件、异常路径是否覆盖？
- 是否存在吞异常、隐藏副作用、共享可变状态？
- 是否把新语法用在真正提升可读性的地方？

### 11.3 异步与并发

- 是否正确传递 `CancellationToken`？
- 是否存在同步阻塞异步、`async void` 滥用、`lock` 内 `await`？
- 是否引入了不必要的并发复杂度？

### 11.4 安全与性能

- 是否存在 SQL 注入、路径穿越、敏感信息泄漏风险？
- 是否有明显的 N+1、重复序列化、重复分配、热点 LINQ 问题？
- 性能优化是否基于测量，而不是猜测？

### 11.5 测试与日志

- 新增逻辑是否有测试？
- 日志是否结构化、字段是否足够排障、是否避免敏感信息？
- 是否补充了必要的迁移说明、配置说明或运维注意事项？

### 11.6 注释与文档一致性

- 新增或修改的公开 API、关键变量 / 常量 / 枚举、协议 / 兼容 / 并发 / 缓存 / 性能关键路径，是否补充了准确注释？
- XML 注释、行内注释、`TODO` / `FIXME` / `HACK` 是否说明了边界、约束、原因和追踪信息，而不是空泛复述代码？
- 是否同步清理了失真、过时、误导或被整段注释掉的旧说明？

### 11.7 控件与绘制

- 自绘控件是否走 `OnPaint` 离屏绘制，而非依赖默认样式？
- 颜色是否使用 `SystemColors` 或项目语义常量，而非硬编码 `Color.FromArgb(...)` / `Color.Xxx`？

---

<a id="sec-12-reference"></a>
## 十二、参考资源

- Microsoft Learn: C# 语言版本与 TFM 映射
- Microsoft Learn: What's new in .NET 8
- Microsoft Learn: What's new in C# 12
- Microsoft Learn: Common C# code conventions
- Microsoft Learn: C# identifier naming rules and conventions

> 本仓库当前 `global.json` 基线为 `10.0.100`，并启用了 `latestFeature`。
> 本规范中的 `.NET 8 / C# 12` 结论，应以仓库锁定版本和目标框架为准，而不是以本地 IDE 的"最新语法提示"为准。
