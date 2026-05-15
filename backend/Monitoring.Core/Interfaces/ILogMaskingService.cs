namespace Monitoring.Core.Interfaces;

public interface ILogMaskingService
{
    string MaskSensitiveData(string message);
}
