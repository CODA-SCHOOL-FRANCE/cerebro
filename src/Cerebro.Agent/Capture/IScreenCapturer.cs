using Cerebro.Shared.Capture;
using CSharpFunctionalExtensions;

namespace Cerebro.Agent.Capture;

public interface IScreenCapturer
{
    Result<byte[], CaptureError> Capture();
}
