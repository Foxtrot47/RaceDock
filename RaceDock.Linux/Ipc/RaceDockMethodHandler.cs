// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.Json;
using RaceDock.Core;
using RaceDock.Core.Presentation;
using Tmds.DBus.Protocol;

namespace RaceDock.Linux.Ipc;

/// <summary>
/// The D-Bus surface the Plasma widget talks to. The payloads are JSON so the same contracts can be
/// reused by a future native front end without depending on D-Bus.
/// </summary>
internal sealed class RaceDockMethodHandler(RaceDockSession session) : IPathMethodHandler
{
    public const string ServiceName = "org.foxtrot47.RaceDock";
    public const string InterfaceName = "org.foxtrot47.RaceDock1";
    public const string ObjectPath = "/org/foxtrot47/RaceDock";

    private const string InterfaceXml = """
        <interface name="org.foxtrot47.RaceDock1">
          <method name="Ping">
            <arg name="reply" type="s" direction="out"/>
          </method>
          <method name="GetWidgetState">
            <arg name="json" type="s" direction="out"/>
          </method>
          <method name="GetFlyoutState">
            <arg name="json" type="s" direction="out"/>
          </method>
          <method name="GetSettings">
            <arg name="json" type="s" direction="out"/>
          </method>
          <method name="ApplySettings">
            <arg name="json" type="s" direction="in"/>
            <arg name="result" type="s" direction="out"/>
          </method>
          <method name="Command">
            <arg name="command" type="s" direction="in"/>
            <arg name="argument" type="s" direction="in"/>
            <arg name="result" type="s" direction="out"/>
          </method>
        </interface>
        """;

    public string Path => ObjectPath;

    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        try
        {
            if (context.IsDBusIntrospectRequest)
            {
                context.ReplyIntrospectXml(
                    [Encoding.UTF8.GetBytes(InterfaceXml)],
                    []);
                return default;
            }

            if (!string.Equals(context.Request.InterfaceAsString, InterfaceName, StringComparison.Ordinal))
            {
                context.ReplyUnknownMethodError();
                return default;
            }

            switch (context.Request.MemberAsString)
            {
                case "Ping":
                    Reply(context, "pong");
                    return default;
                case "GetWidgetState":
                    Reply(context, RaceDockJson.Serialize(session.Widget));
                    return default;
                case "GetFlyoutState":
                    Reply(context, RaceDockJson.Serialize(session.Flyout));
                    return default;
                case "GetSettings":
                    Reply(context, RaceDockJson.SerializeSettings(session.Settings));
                    return default;
                case "ApplySettings":
                case "Command":
                    return HandleAsync(context, context.Request.MemberAsString);
                default:
                    context.ReplyUnknownMethodError();
                    return default;
            }
        }
        catch (Exception exception)
        {
            context.HandleException(exception, shouldDisconnect: false);
            return default;
        }
    }

    private async ValueTask HandleAsync(MethodContext context, string member)
    {
        context.DisposesAsynchronously = true;
        try
        {
            var reader = context.Request.GetBodyReader();
            string response;
            if (member == "ApplySettings")
            {
                var json = reader.ReadString();
                try
                {
                    var settings = RaceDockJson.DeserializeSettings(json);
                    await session.ApplySettingsAsync(settings);
                    response = RaceDockJson.Serialize(new
                    {
                        ok = true,
                        settings = session.Settings
                    });
                }
                catch (JsonException exception)
                {
                    response = Error($"Settings are not valid JSON: {exception.Message}");
                }
            }
            else
            {
                var command = reader.ReadString();
                var argument = reader.ReadString();
                response = await session.CommandAsync(command, argument);
            }
            Reply(context, response);
        }
        catch (Exception exception)
        {
            context.HandleException(exception, shouldDisconnect: false);
        }
        finally
        {
            context.Dispose();
        }
    }

    private static string Error(string message) =>
        RaceDockJson.Serialize(new { ok = false, error = message });

    private static void Reply(MethodContext context, string value)
    {
        using var writer = context.CreateReplyWriter("s");
        writer.WriteString(value);
        context.Reply(writer.CreateMessage());
    }
}
