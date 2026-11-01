// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Net.Http;

namespace PVZ.Core;

/// <summary>
/// Ошибка связи с сервером PVZ: нет интернета, сервер выключен, истёк таймаут и т. п.
/// Сообщение уже сформулировано по-русски и готово к показу преподавателю или ученику.
/// </summary>
public sealed class ServerUnreachableException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public static ServerUnreachableException CreateTransport(HttpRequestException exception, string? serverUrl)
    {
        var target = FormatTarget(serverUrl);
        var message = exception.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError =>
                $"Не удалось найти сервер{target} в интернете. Проверьте подключение к сети и адрес сервера.",
            HttpRequestError.ConnectionError =>
                $"Не удалось подключиться к серверу{target}. Возможно, он выключен или пропал интернет.",
            HttpRequestError.SecureConnectionError =>
                $"Не удалось установить защищённое соединение с сервером{target}. Проверьте интернет и сертификат сервера.",
            _ => $"Нет связи с сервером{target}. Проверьте подключение к интернету и попробуйте ещё раз.",
        };
        return new ServerUnreachableException(message, exception);
    }

    public static ServerUnreachableException CreateTimeout(string? serverUrl, Exception? innerException = null) =>
        new(
            $"Сервер{FormatTarget(serverUrl)} не ответил вовремя. Проверьте подключение к интернету и попробуйте ещё раз.",
            innerException);

    private static string FormatTarget(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return "";
        return Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? $" «{uri.Host}»"
            : "";
    }
}
