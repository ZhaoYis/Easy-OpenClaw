using System.Reflection;
using OpenClaw.Core.Client;
using OpenClaw.Core.Models;
using OpenClaw.Core.Tests.Support;
using Xunit;

namespace OpenClaw.Core.Tests;

/// <summary>
/// 通过反射枚举 <see cref="GatewayClient"/> 上所有返回 <see cref="Task{T}"/> 且结果为 <see cref="GatewayResponse"/> 的公共方法并逐一调用，
/// 以在单测中覆盖 <c>GatewayClient.Methods.cs</c> 中大量薄封装行。
/// </summary>
public sealed class GatewayClientRpcReflectionTests
{
    /// <summary>
    /// 除开放泛型 <c>SendRequestAsync&lt;T&gt;</c> 外，其余 RPC 封装在环回 WebSocket 下应全部可 await 完成。
    /// </summary>
    [Fact]
    public async Task All_declared_GatewayResponse_methods_complete_under_loopback()
    {
        var (client, _) = GatewayClientTestFactory.CreateWithSocket(c => new LoopbackWebSocket(c));

        var methods = typeof(GatewayClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.ReturnType == typeof(Task<GatewayResponse>))
            .Where(m => !(m.Name == "SendRequestAsync" && m.IsGenericMethodDefinition))
            .ToArray();

        foreach (var method in methods)
        {
            object?[] args;
            try
            {
                args = GatewayRpcArgumentFactory.Build(method);
            }
            catch (NotSupportedException)
            {
                throw new Xunit.Sdk.XunitException($"为 {method.Name} 添加 GatewayRpcArgumentFactory 映射。");
            }

            var result = method.Invoke(client, args);
            Assert.NotNull(result);
            var task = Assert.IsAssignableFrom<Task<GatewayResponse>>(result);
            await task;
        }

        var genericSend = typeof(GatewayClient).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == "SendRequestAsync" && m.IsGenericMethodDefinition);
        var closed = genericSend.MakeGenericMethod(typeof(object));
        var genTask = (Task<GatewayResponse>)closed.Invoke(client, ["reflect.rpc", new { z = 3 }, default(CancellationToken)])!;
        await genTask;
    }
}
