// The application uses the custom, UI-agnostic message bus from Arbor.HttpClient.Core.Messaging.
// ReactiveUI also ships an IMessageBus/MessageBus (which this application does not use), so these
// aliases make the unqualified names resolve to our bus across the project and avoid CS0104
// ambiguity in view models that also import ReactiveUI.
global using IMessageBus = Arbor.HttpClient.Core.Messaging.IMessageBus;
global using MessageBus = Arbor.HttpClient.Core.Messaging.MessageBus;
