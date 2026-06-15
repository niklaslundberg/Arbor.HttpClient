// Resolve the unqualified message-bus names to the application's custom bus
// (Arbor.HttpClient.Core.Messaging) rather than ReactiveUI's unused IMessageBus/MessageBus,
// matching the alias used by the Desktop project under test.
global using IMessageBus = Arbor.HttpClient.Core.Messaging.IMessageBus;
global using MessageBus = Arbor.HttpClient.Core.Messaging.MessageBus;
