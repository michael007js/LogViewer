---
name: "donet-naming"
description: ".NET / C# 命名规范执行版。Invoke when working on .NET 8 / net8.0-windows / C# 12 projects and need enforceable naming rules for files, namespaces, types, members, async APIs, DTOs, options, tests, and android.log.client project conventions."
version: "1.0.0"
updatedAt: "2026-06-01"
tags: [命名, C#, .NET, PascalCase, camelCase, 异步, 布尔值, DTO, Options, 测试]
---

# .NET / C# 命名规范（执行版）

适用范围：`.NET 8`、`net8.0-windows`、`C# 12`、`android.log.client`。

## 0. 适用原则

**必须遵守**

- 本技能只定义"命名规范"，不定义架构、分层、异常策略、API 风格、EF Core、Result 模式等非命名规则。
- 若仓库中的 `.editorconfig`、分析器、源生成器、序列化契约或框架约束与本文冲突，以仓库实际规则为准。
- 公开 API、序列化契约、数据库映射相关的命名变更，必须在 PR 中明确说明兼容性影响。

**联动约定**

- 非命名类团队规范，统一参考 [`../dotnet-guidelines/SKILL.md`](../dotnet-guidelines/SKILL.md)。
- `dotnet-guidelines` 中涉及命名的摘要，应与本文件保持一致；命名规则变更时，先改本文件，再同步摘要。
- 调用本技能时，必须同时加载 `rtk` 公共前置技能，不得跳过。`rtk` 负责提供本仓库统一的 shell 命令规范、文件读取策略、工作目录约束、验证方式和项目内执行习惯；不先加载 `rtk`，后续搜索、校验和命令风格就容易偏离仓库约定。

---

## 一、总则

### 1.1 必须遵守

- 文件名使用 **PascalCase**，并与主类型名一致。
- 每个文件默认只放一个公开类型；生成代码除外。
- 命名空间使用 **PascalCase**，并与目录结构基本对应。
- 类、结构体、record、接口、委托、枚举、特性统一使用 **PascalCase**。
- 接口必须使用 `I` 前缀。
- 公开成员、受保护成员、属性、方法、事件统一使用 **PascalCase**。
- 私有字段统一使用 **`_camelCase`**。
- 局部变量、参数、lambda 参数统一使用 **camelCase**。
- 异步方法必须使用 `Async` 后缀；同步方法禁止带 `Async` 后缀。
- 布尔值命名必须能直接表达真假语义；默认优先使用 `is`、`has`、`can`、`should` 前缀。
- 集合变量必须使用复数名。
- `TryXxx` 模式必须返回 `bool`，并通过 `out` 参数返回结果。
- Attribute 类型必须以 `Attribute` 结尾。
- 异常类型必须以 `Exception` 结尾。
- 扩展方法容器类必须以 `Extensions` 结尾。
- 配置类必须以 `Options` 结尾。
- 团队自定义 DTO 默认必须以 `Dto` 结尾；第三方契约模型、EF 投影模型、内部匿名响应模型、历史兼容类型除外，但必须在 PR 中说明原因。
- 输入输出契约必须使用 `Request` / `Response` 后缀。
- 命令查询对象必须使用 `Command` / `Query` 后缀。
- 事件对象必须以 `Event` 结尾。
- 测试类必须以 `Tests` 结尾。
- 普通枚举使用单数名；`[Flags]` 枚举使用复数名。

### 1.2 推荐遵守

- 命名优先表达职责和语义，不为"短"牺牲可读性。
- 避免无语义缩写，除非是团队内稳定且不歧义的约定。
- 新语法不改变命名规则；主构造函数、record、集合表达式、field-backed properties 仍沿用标准 .NET 命名习惯。
- 布尔值在没有更自然、更稳定的业务语义时，优先使用 `is`、`has`、`can`、`should`；像 `enabled`、`visible`、`completed`、`exists` 这类已稳定约定的自然表达可以保留，但同一模块内必须一致。

### 1.3 快速判定表

| 场景 | 规则 | 示例 |
|------|------|------|
| 文件名 | PascalCase | `NetworkClient.cs` |
| 命名空间 | PascalCase | `android.log.client.Sdk.NetworkSdk.Core` |
| 接口 | `I` + PascalCase | `INetworkClient` |
| 私有字段 | `_camelCase` | `_logger` |
| 局部变量 / 参数 | camelCase | `userId` |
| 公开属性 / 方法 | PascalCase | `UserName`、`LoadAsync` |
| 异步方法 | `Async` 后缀 | `SaveAsync` |
| 布尔值 | 默认前缀；自然语义例外需保持一致 | `isEnabled`、`visible` |
| 集合 | 复数名 | `users` |
| 配置类 | `Options` 后缀 | `NetworkClientOptions` |
| DTO | `Dto` 后缀 | `UserDto` |
| 异常类 | `Exception` 后缀 | `MySqlSdkException` |
| 扩展类 | `Extensions` 后缀 | `NetworkServiceCollectionExtensions` |

---

## 二、推荐实践

### 2.1 类型与文件

```csharp
// 文件名: NetworkClient.cs
namespace android.log.client.Sdk.NetworkSdk.Core;

public sealed class NetworkClient
{
}
```

```text
// 推荐
LogViewer/Network/LogServer.cs
LogViewer/UI/MainForm.cs
LogViewer/Models/LogEntry.cs

// 不推荐
logviewer/network/log_server.cs
logviewer/ui/main_form.cs
```

### 2.2 类型命名

```csharp
public interface INetworkClient { }

public sealed class NetworkClient { }

public readonly record struct UserId(Guid Value);

public sealed record CreateUserRequest(string UserName);

public sealed class MySqlSdkException : Exception { }

public sealed class AuditIgnoreAttribute : Attribute { }

public static class NetworkServiceCollectionExtensions { }
```

### 2.3 成员与参数命名

```csharp
public sealed class NetworkClient(ILogger<NetworkClient> logger, INetworkClientOptions options)
{
    private readonly ILogger<NetworkClient> _logger = logger;
    private readonly INetworkClientOptions _options = options;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    public const int MaxRetryCount = 3;

    public async Task<UserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _repository.FindAsync(userId, cancellationToken);
        return user is null ? null : new UserDto(user.Id, user.UserName);
    }
}
```

### 2.4 主构造函数、record、属性

**必须遵守**

- class / struct 的主构造函数参数本质上是参数，使用 **camelCase**。
- positional record 参数会成为公开成员，使用 **PascalCase**。
- `required` / `init` / `field` 支持的属性仍按公开属性规则使用 **PascalCase**。

```csharp
public sealed class TokenService(TimeProvider timeProvider)
{
    private readonly TimeProvider _timeProvider = timeProvider;
}

public sealed record UserDto(Guid Id, string UserName, string? DisplayName);

public sealed class NetworkClientOptions
{
    public required string BaseUrl { get; init; }
    public required string ApiKey { get; init; }
}

public string NormalizedName
{
    get;
    set => field = value.Trim();
}
```

### 2.5 异步、布尔值、集合、Try 模式

```csharp
public Task SaveAsync(CancellationToken cancellationToken = default);
public ValueTask<bool> TryRefreshAsync(CancellationToken cancellationToken = default);

bool isEnabled;
bool hasPendingJobs;
bool canRetry;
bool shouldRefreshCache;

List<UserDto> users = [];
Dictionary<Guid, DeviceDto> devicesById = [];

public static bool TryParseUserId(string value, out UserId userId)
{
    ...
}
```

**布尔值补充**

- 默认写法：`isEnabled`、`hasChildren`、`canRetry`、`shouldRefreshCache`
- 可接受例外：`visible`、`enabled`、`completed`、`exists`
- 同一模块不要混用 `isVisible` 和 `visible` 这类两套风格

### 2.6 缩写与大小写

**必须遵守**

- 使用 .NET 常见大小写约定：
  - `Id`，不是 `ID`
  - `HttpClient`，不是 `HTTPClient`
  - `XmlReader`，不是 `XMLReader`
  - `DbConnection`，不是 `DBConnection`

```csharp
Guid userId;
HttpClient httpClient;
XmlReader xmlReader;
DbConnection dbConnection;
```

```csharp
// 不推荐
Guid userID;
HttpClient HTTPClient;
XmlReader XMLReader;
DbConnection DBConnection;
```

### 2.7 反例

```csharp
// 不推荐：文件名与主类型不一致
// NetworkClientImpl.cs -> class AppNetworkClient

// 不推荐：同步方法带 Async
public void SaveAsync()
{
}

// 不推荐：同一模块混用两套布尔风格
bool isVisible;
bool visible;

// 不推荐：集合使用单数名
List<UserDto> user = [];

// 不推荐：TryXxx 不返回 bool
public UserId TryParse(string value)
{
    ...
}
```

---

## 三、项目约定（android.log.client）

### 3.1 目录与命名空间

**推荐遵守**

- 命名空间 = `android.log.client.` + 目录路径（斜杠换点），目录确定后命名空间自动确定，不存在分开决策。
- 目录优先按职责分层组织：
  - `Models/` — 数据模型层（零 UI 依赖）
  - `Network/` — 通信层（零 UI 依赖）
  - `UI/` — 界面层（MainForm、JsonDetailForm、JsonTreeView、DevicePanel、SettingsDialog）
  - `Utils/` — 工具类（AdbHelper、JsonFormatter）
  - `Tests/` — 测试文件

```text
android.log.client.Sdk.NetworkSdk.Core
android.log.client.Sdk.NetworkSdk.Abstractions
android.log.client.Sdk.MySqlSdk.Extensions
android.log.client.Sdk.ConfigStoreSdk
android.log.client.Components.Atomic.Theming
android.log.client.Components.Composite
```

### 3.2 项目内固定后缀

**必须遵守**

- DI 注册扩展类使用 `ServiceCollectionExtensions` 或更具体的 `XxxServiceCollectionExtensions`。
- DI 注册方法使用 `AddXxx`。
- 日志辅助类统一使用 `Log`、`XxxLog` 或 `XxxLoggerExtensions`，同一模块内保持一致。
- 只读标识值对象优先使用 `XxxId`。
- 仓储实现使用 `Repository` 后缀，对应接口使用 `I + XxxRepository`。
- 外部服务调用封装使用 `Client` 后缀，对应接口使用 `I + XxxClient`。
- 提供型组件使用 `Provider` 后缀，对应接口使用 `I + XxxProvider`。
- 工厂类型使用 `Factory` 后缀，对应接口使用 `I + XxxFactory`。
- SDK 统一异常使用 `SdkException` 后缀（如 `MySqlSdkException`）。
- SDK 选项使用 `XxxOptions` 后缀（如 `NetworkClientOptions`、`MySqlSdkOptions`）。

```csharp
public static class NetworkServiceCollectionExtensions
{
    public static IServiceCollection AddNetworkClient(this IServiceCollection services)
    {
        ...
    }
}

public interface INetworkClient { }
public sealed class NetworkClient : INetworkClient { }

public interface IMySqlClient { }
public sealed class MySqlClient : IMySqlClient { }

public sealed class MySqlSdkException : Exception { }

public sealed class NetworkClientOptions { }
```

### 3.3 测试命名

**必须遵守**

- 测试项目命名为 `android.log.client.Tests`。
- 测试文件放 `Tests/` 目录，由测试项目通过 `<Compile Include>` 引用，不复制源文件到测试项目目录。
- 测试类命名为"被测类型 + `Tests`"。
- 测试方法使用 `Method_State_Expected` 或 `Should_DoSomething_When_Condition`。
- 测试文件名以 `.Tests.cs` 结尾（如 `NetworkClient.Tests.cs`）。

```csharp
public sealed class NetworkClientTests
{
    [Fact]
    public async Task GetByIdAsync_UserExists_ReturnsUser()
    {
    }

    [Fact]
    public async Task Should_ReturnNull_When_UserDoesNotExist()
    {
    }
}
```

### 3.4 审查与 CI 关注点

**必须遵守**

- Reviewer 必须检查新增类型、文件、测试、DTO、Options、Event、异常类、Repository、Client、Provider、Factory 是否符合后缀规则。
- Reviewer 必须检查异步方法是否遗漏 `Async` 后缀。
- Reviewer 必须检查私有字段是否统一为 `_camelCase`。
- Reviewer 必须检查命名变更是否影响公开 API、JSON 字段名、数据库映射名、消息契约。

**推荐遵守**

- 分析器、格式化规则和命名约束能自动化时尽量自动化，不依赖人工记忆。
- 命名不确定时，优先选择"团队一眼能看懂"的名字，而不是最短的名字。

---

## 四、执行清单

提交前至少自查以下问题：

- 文件名是否与主类型名一致？
- 命名空间是否与目录结构一致？
- 接口是否以 `I` 开头？
- 异步方法是否都有 `Async` 后缀？
- 布尔值是否表达了明确真假语义？若未使用 `is/has/can/should`，是否属于团队认可的自然语义例外且模块内保持一致？
- 集合是否用了复数名？
- `Options`、`Dto`、`Request`、`Response`、`Command`、`Query`、`Event`、`Exception`、`Extensions`、`Tests`、`Repository`、`Client`、`Provider`、`Factory`、`SdkException` 后缀是否正确？
- 私有字段是否统一为 `_camelCase`？
- 非 `Dto` 命名的传输模型，是否有外部契约、投影或兼容性理由，并已在 PR 说明？
- 是否出现 `userID`、`HTTPClient`、`XMLReader`、`DBConnection` 这类不符合 .NET 习惯的大小写？

> 若需求超出命名本身，例如分层依赖、异常策略、API 风格、CancellationToken 透传、Result 模式、MediatR/CQRS、XML 注释要求、`sealed` / `file-scoped namespace` / `required` / `init` 的强制性，请转到 [`../dotnet-guidelines/SKILL.md`](../dotnet-guidelines/SKILL.md) 处理。
