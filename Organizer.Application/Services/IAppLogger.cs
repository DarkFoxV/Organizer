using System;

namespace Organizer.Application.Services;

public interface IAppLogger
{
    void Info(string message);

    void Error(string message, Exception exception);
}
