namespace OpenClaw.Core.SignalR;

/// <summary>
/// 绑定配置节 <c>OpenClawSignalR:Jwt</c> 的 JWT 校验选项（与 <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions"/> 常见字段对应）。
/// </summary>
public sealed class OpenClawSignalRJwtOptions
{
    /// <summary>元数据地址（如 IdP）；与 <c>IssuerSigningKey</c> 二选一或同时配置，取决于校验方式。</summary>
    public string? Authority { get; set; }

    /// <summary>受众。</summary>
    public string? Audience { get; set; }

    public string? Issuer { get; set; }

    /// <summary>对称密钥 Base64（用于测试或简单场景）；生产可改用 Authority + 元数据。</summary>
    public string? SigningKeyBase64 { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;
}
