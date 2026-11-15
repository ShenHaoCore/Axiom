// 输入校验错误的中间表示：Application 层产出错误码与参数，接口层负责本地化。
namespace Axiom.SharedKernel.Errors;

public sealed class ValidationError(string errorCode, params object[] args)
{
    public string ErrorCode { get; } = errorCode;

    public object[] Args { get; } = args;
}
