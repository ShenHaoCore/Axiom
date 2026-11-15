// 业务异常基类：携带错误码与本地化参数。
// Message 保留默认语言文本，供日志与单元测试使用；对外响应由接口层按 ErrorCode 本地化。
namespace Axiom.SharedKernel.Errors;

public abstract class AxiomException : Exception
{
    protected AxiomException(string errorCode, string message, params object[] args)
        : base(message)
    {
        ErrorCode = errorCode;
        Args = args;
    }

    /// <summary>用于在资源文件中查找本地化消息的错误码。</summary>
    public string ErrorCode { get; }

    /// <summary>传给资源消息模板的格式化参数。</summary>
    public object[] Args { get; }
}
