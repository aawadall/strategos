// CommandBus.cs
// The command topic: orders published here, delivered to subscribers a step later.
//
// The mechanism — and the four delivery rules it enforces — lives in MessageBus<T>, shared
// with the situation topic. See Assets/Scripts/Core/Messaging/MessageBus.cs; that header is
// the explanation, and this file is only the instantiation for commands.
//
// A named subclass rather than `MessageBus<Command>` at every use site, because the two
// topics are not interchangeable: publishing a report onto the command topic should be a
// compile error and not a runtime surprise, and a distinct type is what makes it one.

namespace Strategos.Commands
{
    public sealed class CommandBus : Messaging.MessageBus<Command>
    {
        protected override string TopicName => "CommandBus";
    }
}
