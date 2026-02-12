using System.Text;
using MacroRecorder.Core.Domain;

namespace MacroRecorder.Core.Serialization;

public static class BinaryMacroSerializer
{
    private const string Magic = "MCR1";
    private const ushort CurrentVersion = 1;

    public static void Write(Stream stream, Macro macro)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes(Magic));
        writer.Write(CurrentVersion);
        writer.Write((ushort)0);
        writer.Write(macro.CreatedAt.UtcTicks);
        writer.Write(macro.UpdatedAt.UtcTicks);
        writer.Write(macro.Id.ToByteArray());

        var nameBytes = Encoding.UTF8.GetBytes(macro.Name);
        writer.Write(nameBytes.Length);
        writer.Write(nameBytes);

        writer.Write(macro.Actions.Count);
        foreach (var action in macro.Actions)
        {
            writer.Write((ushort)action.Type);
            using var payloadStream = new MemoryStream();
            using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
            {
                WritePayload(payloadWriter, action);
            }

            var payload = payloadStream.ToArray();
            writer.Write(payload.Length);
            writer.Write(payload);
        }

        writer.Write(0); // metadata count
    }

    public static Macro Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected magic '{magic}'.");
        }

        var version = reader.ReadUInt16();
        _ = reader.ReadUInt16();
        if (version > CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported macro version {version}.");
        }

        var created = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
        var updated = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
        var id = new Guid(reader.ReadBytes(16));

        var nameLength = reader.ReadInt32();
        var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));

        var macro = new Macro
        {
            Id = id,
            Name = name,
            CreatedAt = created,
            UpdatedAt = updated
        };

        var actionCount = reader.ReadInt32();
        for (var i = 0; i < actionCount; i++)
        {
            var actionType = (ActionType)reader.ReadUInt16();
            var payloadLength = reader.ReadInt32();
            var payloadBytes = reader.ReadBytes(payloadLength);
            using var payloadStream = new MemoryStream(payloadBytes, writable: false);
            using var payloadReader = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: true);

            macro.Actions.Add(ReadPayload(payloadReader, actionType));
        }

        if (stream.Position < stream.Length)
        {
            var metadataCount = reader.ReadInt32();
            for (var i = 0; i < metadataCount; i++)
            {
                _ = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                _ = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            }
        }

        macro.SortActions();
        return macro;
    }

    private static void WriteCommon(BinaryWriter writer, MacroAction action)
    {
        writer.Write(action.ActionId.ToByteArray());
        writer.Write(action.TimeOffsetTicks);
        writer.Write(action.DurationTicks);
    }

    private static void WritePayload(BinaryWriter writer, MacroAction action)
    {
        WriteCommon(writer, action);

        switch (action)
        {
            case MouseMoveAction mouseMove:
                writer.Write(mouseMove.X);
                writer.Write(mouseMove.Y);
                writer.Write(mouseMove.MonitorId);
                break;
            case MouseDownAction mouseDown:
                writer.Write((byte)mouseDown.Button);
                break;
            case MouseUpAction mouseUp:
                writer.Write((byte)mouseUp.Button);
                break;
            case MouseClickAction mouseClick:
                writer.Write((byte)mouseClick.Button);
                break;
            case KeyDownAction keyDown:
                writer.Write(keyDown.ScanCode);
                break;
            case KeyUpAction keyUp:
                writer.Write(keyUp.ScanCode);
                break;
            case TextInputAction textInput:
                var textBytes = Encoding.UTF8.GetBytes(textInput.Text);
                writer.Write(textBytes.Length);
                writer.Write(textBytes);
                break;
            case WaitAction:
                break;
            default:
                throw new InvalidDataException($"Unsupported action type: {action.GetType().Name}");
        }
    }

    private static MacroAction ReadPayload(BinaryReader reader, ActionType actionType)
    {
        var actionId = new Guid(reader.ReadBytes(16));
        var timeOffset = reader.ReadInt64();
        var duration = reader.ReadInt64();

        return actionType switch
        {
            ActionType.MouseMove => new MouseMoveAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                MonitorId = reader.ReadInt32()
            },
            ActionType.MouseDown => new MouseDownAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                Button = (MouseButton)reader.ReadByte()
            },
            ActionType.MouseUp => new MouseUpAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                Button = (MouseButton)reader.ReadByte()
            },
            ActionType.MouseClick => new MouseClickAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                Button = (MouseButton)reader.ReadByte()
            },
            ActionType.KeyDown => new KeyDownAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                ScanCode = reader.ReadInt32()
            },
            ActionType.KeyUp => new KeyUpAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                ScanCode = reader.ReadInt32()
            },
            ActionType.TextInput => new TextInputAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration,
                Text = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()))
            },
            ActionType.Wait => new WaitAction
            {
                ActionId = actionId,
                TimeOffsetTicks = timeOffset,
                DurationTicks = duration
            },
            _ => throw new InvalidDataException($"Unsupported action type code: {(ushort)actionType}")
        };
    }
}
