using System.Reflection;
using System.Text.Json;
using OpenClaw.Core.Helpers;
using OpenClaw.Core.Models;

namespace OpenClaw.Core.Tests.Support;

/// <summary>
/// 为 <see cref="OpenClaw.Core.Client.GatewayClient"/> 的各 RPC 反射调用构造最小合法实参，避免为每个方法手写样板。
/// </summary>
internal static class GatewayRpcArgumentFactory
{
    /// <summary>
    /// 按形参顺序生成 <see cref="MethodInfo.Invoke"/> 所需的实参数组。
    /// </summary>
    /// <param name="method">目标方法（通常为返回 <c>Task&lt;GatewayResponse&gt;</c> 的实例方法）</param>
    /// <returns>与形参个数一致的实参列表</returns>
    public static object?[] Build(MethodInfo method) =>
        method.GetParameters().Select(CreateArgument).ToArray();

    /// <summary>
    /// 为单个形参生成实参：优先处理 <see cref="CancellationToken"/> 与可选参数默认值，否则走强类型工厂。
    /// </summary>
    private static object? CreateArgument(ParameterInfo p)
    {
        var t = p.ParameterType;

        if (t == typeof(CancellationToken))
            return default(CancellationToken);

        if (p.HasDefaultValue)
            return p.DefaultValue;

        return CreateRequired(t);
    }

    /// <summary>
    /// 为无默认值的形参构造占位值；覆盖 GatewayClient 各 RPC 用到的所有 DTO 与基元类型。
    /// </summary>
    private static object? CreateRequired(Type t)
    {
        if (t == typeof(string))
            return "unit";
        if (t == typeof(string[]))
            return new[] { GatewayConstants.DefaultSessionKey };
        if (t == typeof(int))
            return 1;
        if (t == typeof(bool))
            return false;
        if (t == typeof(object))
            return new { x = 1 };
        if (t == typeof(JsonElement))
            return JsonSerializer.SerializeToElement(new { }, JsonDefaults.SerializerOptions);

        if (t == typeof(LogsTailParams))
            return new LogsTailParams { Limit = 10 };
        if (t == typeof(ToolsCatalogParams))
            return new ToolsCatalogParams();
        if (t == typeof(ToolsEffectiveParams))
            return new ToolsEffectiveParams { SessionKey = GatewayConstants.DefaultSessionKey };
        if (t == typeof(SkillsSearchParams))
            return new SkillsSearchParams { Query = "q" };
        if (t == typeof(SessionsMessagesKeyParams))
            return new SessionsMessagesKeyParams { Key = GatewayConstants.DefaultSessionKey };
        if (t == typeof(SessionsPreviewParams))
            return new SessionsPreviewParams { Keys = [GatewayConstants.DefaultSessionKey] };
        if (t == typeof(SessionsResolveParams))
            return new SessionsResolveParams { Key = "partial-key" };
        if (t == typeof(SessionsCreateParams))
            return new SessionsCreateParams { Title = "t" };
        if (t == typeof(SessionsSendParams))
            return new SessionsSendParams { SessionKey = GatewayConstants.DefaultSessionKey, Message = "hi" };
        if (t == typeof(SessionsSteerParams))
            return new SessionsSteerParams { SessionKey = GatewayConstants.DefaultSessionKey, Message = "steer" };
        if (t == typeof(SessionsUsageParams))
            return new SessionsUsageParams { Key = GatewayConstants.DefaultSessionKey };
        if (t == typeof(SessionsUsageTimeseriesParams))
            return new SessionsUsageTimeseriesParams { Key = GatewayConstants.DefaultSessionKey };
        if (t == typeof(SessionsUsageLogsParams))
            return new SessionsUsageLogsParams { Key = GatewayConstants.DefaultSessionKey };
        if (t == typeof(WakeParams))
            return new WakeParams { Mode = WakeScheduleMode.Now, Text = "wake" };
        if (t == typeof(CronListParams))
            return new CronListParams();
        if (t == typeof(CronRunsParams))
            return new CronRunsParams();
        if (t == typeof(SendParams))
            return new SendParams { Channel = "telegram", Account = "acc", Text = "hello" };
        if (t == typeof(ChatInjectParams))
            return new ChatInjectParams { Role = "user", Content = "injected" };

        throw new NotSupportedException($"Add a factory case for parameter type {t.FullName}");
    }
}
