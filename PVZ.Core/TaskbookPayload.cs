// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Text;

namespace PVZ.Core;

public static class TaskbookPayload
{
    private static ReadOnlySpan<byte> Magic => "PVZTBK1"u8;

    public static void WriteAppended(string exePath, TaskbookDocument document)
    {
        var fileBytes = File.ReadAllBytes(exePath);
        var baseLength = TryGetPayloadStart(fileBytes, out var payloadStart) ? payloadStart : fileBytes.Length;

        var jsonBytes = Encoding.UTF8.GetBytes(TaskbookIO.Serialize(document));

        using var output = File.Create(exePath);
        output.Write(fileBytes, 0, baseLength);
        output.Write(Magic);
        output.Write(BitConverter.GetBytes(jsonBytes.Length));
        output.Write(jsonBytes);
    }

    public static bool TryReadAppended(string exePath, out TaskbookDocument? document)
    {
        document = null;
        if (!File.Exists(exePath))
            return false;

        var bytes = File.ReadAllBytes(exePath);
        if (!TryGetPayloadStart(bytes, out var payloadStart))
            return false;

        var jsonLength = BitConverter.ToInt32(bytes, payloadStart + Magic.Length);
        var jsonStart = payloadStart + Magic.Length + 4;
        if (jsonLength <= 0 || jsonStart + jsonLength != bytes.Length)
            return false;

        var json = Encoding.UTF8.GetString(bytes, jsonStart, jsonLength);
        document = TaskbookIO.Deserialize(json);
        return true;
    }

    private static bool TryGetPayloadStart(byte[] fileBytes, out int payloadStart)
    {
        payloadStart = -1;
        if (fileBytes.Length < Magic.Length + 4)
            return false;

        for (var i = fileBytes.Length - Magic.Length - 4; i >= 0; i--)
        {
            if (!fileBytes.AsSpan(i, Magic.Length).SequenceEqual(Magic))
                continue;

            var jsonLength = BitConverter.ToInt32(fileBytes, i + Magic.Length);
            var jsonStart = i + Magic.Length + 4;
            if (jsonLength <= 0 || jsonStart + jsonLength != fileBytes.Length)
                continue;

            payloadStart = i;
            return true;
        }

        return false;
    }
}
