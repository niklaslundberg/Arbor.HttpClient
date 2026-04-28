// Strongly typed resource accessors for Strings.resx.
// This file is maintained in source control and may be edited intentionally.
#nullable enable

using System.Globalization;
using System.Resources;

namespace Arbor.HttpClient.Desktop.Localization;

/// <summary>A strongly-typed resource class for looking up localized strings.</summary>
[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class Strings
{
    private static ResourceManager? _resourceManager;

    /// <summary>Returns the cached ResourceManager instance used by this class.</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "Arbor.HttpClient.Desktop.Localization.Strings",
                typeof(Strings).Assembly);
            return _resourceManager;
        }
    }

    /// <summary>Overrides the current thread's CurrentUICulture for all resource lookups.</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    public static CultureInfo? Culture { get; set; }

    private static string Get(string key)
    {
        var value = ResourceManager.GetString(key, Culture);
#if DEBUG
        if (value is null)
        {
            throw new InvalidOperationException(
                $"Resource string '{key}' not found in {ResourceManager.BaseName}. " +
                "Add the missing key to Strings.resx.");
        }
        return value;
#else
        return value ?? key;
#endif
    }

    // ═══════════════════════════════ Main Menu ═══════════════════════════════

    /// <summary>_File</summary>
    public static string MenuFile => Get(nameof(MenuFile));

    /// <summary>Import _OpenAPI…</summary>
    public static string MenuImportOpenApi => Get(nameof(MenuImportOpenApi));

    /// <summary>E_xit</summary>
    public static string MenuExit => Get(nameof(MenuExit));

    /// <summary>_View</summary>
    public static string MenuView => Get(nameof(MenuView));

    /// <summary>_History</summary>
    public static string MenuHistory => Get(nameof(MenuHistory));

    /// <summary>_Collections</summary>
    public static string MenuCollections => Get(nameof(MenuCollections));

    /// <summary>_Scheduled Jobs</summary>
    public static string MenuScheduledJobs => Get(nameof(MenuScheduledJobs));

    /// <summary>_Options</summary>
    public static string MenuOptions => Get(nameof(MenuOptions));

    /// <summary>_Environments</summary>
    public static string MenuEnvironments => Get(nameof(MenuEnvironments));

    /// <summary>Coo_kies</summary>
    public static string MenuCookies => Get(nameof(MenuCookies));

    /// <summary>Lo_gs</summary>
    public static string MenuLogs => Get(nameof(MenuLogs));

    /// <summary>_Layout</summary>
    public static string MenuLayout => Get(nameof(MenuLayout));

    /// <summary>_Help</summary>
    public static string MenuHelp => Get(nameof(MenuHelp));

    /// <summary>_About</summary>
    public static string MenuAbout => Get(nameof(MenuAbout));

    /// <summary>_Diagnostics</summary>
    public static string MenuDiagnostics => Get(nameof(MenuDiagnostics));

    // ═══════════════════════════════ Main Toolbar ═══════════════════════════════

    /// <summary>Arbor Http Client</summary>
    public static string AppTitle => Get(nameof(AppTitle));

    /// <summary>Env:</summary>
    public static string ToolbarEnvLabel => Get(nameof(ToolbarEnvLabel));

    /// <summary>Options</summary>
    public static string ToolbarOptions => Get(nameof(ToolbarOptions));

    /// <summary>Environments</summary>
    public static string ToolbarEnvironments => Get(nameof(ToolbarEnvironments));

    /// <summary>Import OpenAPI</summary>
    public static string ToolbarImportOpenApi => Get(nameof(ToolbarImportOpenApi));

    /// <summary>🍪 Cookies</summary>
    public static string ToolbarCookies => Get(nameof(ToolbarCookies));

    /// <summary>📋 Logs</summary>
    public static string ToolbarLogs => Get(nameof(ToolbarLogs));

    /// <summary>Restore</summary>
    public static string DraftRestore => Get(nameof(DraftRestore));

    /// <summary>Discard</summary>
    public static string DraftDiscard => Get(nameof(DraftDiscard));

    // ═══════════════════════════════ Activity Bar ═══════════════════════════════

    /// <summary>Collections</summary>
    public static string ActivityBarCollections => Get(nameof(ActivityBarCollections));

    /// <summary>Environments</summary>
    public static string ActivityBarEnvironments => Get(nameof(ActivityBarEnvironments));

    /// <summary>Options</summary>
    public static string ActivityBarOptions => Get(nameof(ActivityBarOptions));

    /// <summary>Cookies</summary>
    public static string ActivityBarCookies => Get(nameof(ActivityBarCookies));

    /// <summary>Logs</summary>
    public static string ActivityBarLogs => Get(nameof(ActivityBarLogs));

    /// <summary>Import OpenAPI</summary>
    public static string ActivityBarImportOpenApi => Get(nameof(ActivityBarImportOpenApi));

    /// <summary>About</summary>
    public static string ActivityBarAbout => Get(nameof(ActivityBarAbout));

    // ═══════════════════════════════ Request View ═══════════════════════════════

    /// <summary>Type</summary>
    public static string RequestTypeLabel => Get(nameof(RequestTypeLabel));

    /// <summary>Request name</summary>
    public static string RequestNamePlaceholder => Get(nameof(RequestNamePlaceholder));

    /// <summary>Enter request URL</summary>
    public static string RequestUrlPlaceholder => Get(nameof(RequestUrlPlaceholder));

    /// <summary>⚡ This request targets the local demo server which is not running.</summary>
    public static string RequestDemoBannerText => Get(nameof(RequestDemoBannerText));

    /// <summary>Start server</summary>
    public static string RequestDemoBannerStartServer => Get(nameof(RequestDemoBannerStartServer));

    /// <summary>Follow redirects</summary>
    public static string RequestFollowRedirects => Get(nameof(RequestFollowRedirects));

    // Request tabs

    /// <summary>Query</summary>
    public static string TabQuery => Get(nameof(TabQuery));

    /// <summary>Body</summary>
    public static string TabBody => Get(nameof(TabBody));

    /// <summary>GraphQL</summary>
    public static string TabGraphQL => Get(nameof(TabGraphQL));

    /// <summary>WebSocket</summary>
    public static string TabWebSocket => Get(nameof(TabWebSocket));

    /// <summary>SSE</summary>
    public static string TabSse => Get(nameof(TabSse));

    /// <summary>gRPC</summary>
    public static string TabGrpc => Get(nameof(TabGrpc));

    /// <summary>Headers</summary>
    public static string TabHeaders => Get(nameof(TabHeaders));

    /// <summary>Auth</summary>
    public static string TabAuth => Get(nameof(TabAuth));

    /// <summary>Preview</summary>
    public static string TabPreview => Get(nameof(TabPreview));

    /// <summary>Notes</summary>
    public static string TabNotes => Get(nameof(TabNotes));

    // Query tab

    /// <summary>Query parameters</summary>
    public static string QueryParametersLabel => Get(nameof(QueryParametersLabel));

    /// <summary>+ Add Query</summary>
    public static string QueryParametersAddButton => Get(nameof(QueryParametersAddButton));

    /// <summary>Key</summary>
    public static string QueryKeyPlaceholder => Get(nameof(QueryKeyPlaceholder));

    /// <summary>Value</summary>
    public static string QueryValuePlaceholder => Get(nameof(QueryValuePlaceholder));

    /// <summary>Description</summary>
    public static string QueryDescriptionPlaceholder => Get(nameof(QueryDescriptionPlaceholder));

    // Body tab

    /// <summary>Request Body</summary>
    public static string RequestBodyLabel => Get(nameof(RequestBodyLabel));

    /// <summary>Open in editor</summary>
    public static string RequestBodyOpenInEditor => Get(nameof(RequestBodyOpenInEditor));

    // GraphQL tab

    /// <summary>Query / Mutation</summary>
    public static string GraphQlQueryLabel => Get(nameof(GraphQlQueryLabel));

    /// <summary>Variables (JSON)</summary>
    public static string GraphQlVariablesLabel => Get(nameof(GraphQlVariablesLabel));

    /// <summary>Operation name</summary>
    public static string GraphQlOperationNameLabel => Get(nameof(GraphQlOperationNameLabel));

    /// <summary>(optional)</summary>
    public static string GraphQlOperationNamePlaceholder => Get(nameof(GraphQlOperationNamePlaceholder));

    /// <summary>Introspect Schema</summary>
    public static string GraphQlIntrospect => Get(nameof(GraphQlIntrospect));

    // WebSocket tab

    /// <summary>● Connected</summary>
    public static string WsConnected => Get(nameof(WsConnected));

    /// <summary>○ Disconnected</summary>
    public static string WsDisconnected => Get(nameof(WsDisconnected));

    /// <summary>Message to send…</summary>
    public static string WsMessagePlaceholder => Get(nameof(WsMessagePlaceholder));

    /// <summary>Send</summary>
    public static string WsSend => Get(nameof(WsSend));

    /// <summary>Messages</summary>
    public static string WsMessages => Get(nameof(WsMessages));

    /// <summary>Clear</summary>
    public static string WsClear => Get(nameof(WsClear));

    // SSE tab

    /// <summary>Events</summary>
    public static string SseEvents => Get(nameof(SseEvents));

    /// <summary>Clear</summary>
    public static string SseClear => Get(nameof(SseClear));

    // gRPC tab

    /// <summary>gRPC Unary Request</summary>
    public static string GrpcTitle => Get(nameof(GrpcTitle));

    /// <summary>⚠ .proto import required</summary>
    public static string GrpcWarningTitle => Get(nameof(GrpcWarningTitle));

    /// <summary>gRPC calls require a compiled Protocol Buffer schema. ...</summary>
    public static string GrpcDescription => Get(nameof(GrpcDescription));

    // Headers tab

    /// <summary>Headers</summary>
    public static string HeadersLabel => Get(nameof(HeadersLabel));

    /// <summary>+ Add Header</summary>
    public static string HeadersAddButton => Get(nameof(HeadersAddButton));

    /// <summary>Content-Type</summary>
    public static string HeadersContentTypeLabel => Get(nameof(HeadersContentTypeLabel));

    /// <summary>e.g. application/vnd.api+json</summary>
    public static string HeadersContentTypePlaceholder => Get(nameof(HeadersContentTypePlaceholder));

    /// <summary>Key</summary>
    public static string HeadersKeyPlaceholder => Get(nameof(HeadersKeyPlaceholder));

    /// <summary>Value</summary>
    public static string HeadersValuePlaceholder => Get(nameof(HeadersValuePlaceholder));

    /// <summary>Description</summary>
    public static string HeadersDescriptionPlaceholder => Get(nameof(HeadersDescriptionPlaceholder));

    // Auth tab

    /// <summary>Type</summary>
    public static string AuthTypeLabel => Get(nameof(AuthTypeLabel));

    /// <summary>Token</summary>
    public static string AuthTokenLabel => Get(nameof(AuthTokenLabel));

    /// <summary>Bearer token</summary>
    public static string AuthBearerTokenPlaceholder => Get(nameof(AuthBearerTokenPlaceholder));

    /// <summary>Username</summary>
    public static string AuthUsernameLabel => Get(nameof(AuthUsernameLabel));

    /// <summary>Username</summary>
    public static string AuthUsernamePlaceholder => Get(nameof(AuthUsernamePlaceholder));

    /// <summary>Password</summary>
    public static string AuthPasswordLabel => Get(nameof(AuthPasswordLabel));

    /// <summary>Password</summary>
    public static string AuthPasswordPlaceholder => Get(nameof(AuthPasswordPlaceholder));

    /// <summary>API key</summary>
    public static string AuthApiKeyLabel => Get(nameof(AuthApiKeyLabel));

    /// <summary>API key</summary>
    public static string AuthApiKeyPlaceholder => Get(nameof(AuthApiKeyPlaceholder));

    /// <summary>Access token</summary>
    public static string AuthAccessTokenLabel => Get(nameof(AuthAccessTokenLabel));

    /// <summary>OAuth 2 access token</summary>
    public static string AuthOAuth2Placeholder => Get(nameof(AuthOAuth2Placeholder));

    // Preview tab

    /// <summary>Rendered request (as sent)</summary>
    public static string PreviewLabel => Get(nameof(PreviewLabel));

    // Notes tab

    /// <summary>Request notes (Markdown supported)</summary>
    public static string NotesLabel => Get(nameof(NotesLabel));

    /// <summary>Document the purpose, expected responses, known quirks…</summary>
    public static string NotesFreeformPlaceholder => Get(nameof(NotesFreeformPlaceholder));

    // ═══════════════════════════════ Response View ═══════════════════════════════

    /// <summary>Response headers</summary>
    public static string ResponseHeadersLabel => Get(nameof(ResponseHeadersLabel));

    /// <summary>📋 Copy body</summary>
    public static string ResponseCopyBody => Get(nameof(ResponseCopyBody));

    /// <summary>💾 Save as file</summary>
    public static string ResponseSaveAsFile => Get(nameof(ResponseSaveAsFile));

    /// <summary>⌨ Copy as cURL</summary>
    public static string ResponseCopyAsCurl => Get(nameof(ResponseCopyAsCurl));

    /// <summary>Binary response</summary>
    public static string ResponseBinaryResponse => Get(nameof(ResponseBinaryResponse));

    /// <summary>Save and Open</summary>
    public static string ResponseSaveAndOpen => Get(nameof(ResponseSaveAndOpen));

    /// <summary>Raw</summary>
    public static string ResponseTabRaw => Get(nameof(ResponseTabRaw));

    // ═══════════════════════════════ Options ═══════════════════════════════

    /// <summary>HTTP</summary>
    public static string OptionsNavHttp => Get(nameof(OptionsNavHttp));

    /// <summary>Scheduled Jobs</summary>
    public static string OptionsNavScheduledJobs => Get(nameof(OptionsNavScheduledJobs));

    /// <summary>Look &amp; Feel</summary>
    public static string OptionsNavLookAndFeel => Get(nameof(OptionsNavLookAndFeel));

    /// <summary>Diagnostics</summary>
    public static string OptionsNavDiagnostics => Get(nameof(OptionsNavDiagnostics));

    /// <summary>HTTP version</summary>
    public static string OptionsHttpVersionLabel => Get(nameof(OptionsHttpVersionLabel));

    /// <summary>TLS version</summary>
    public static string OptionsTlsVersionLabel => Get(nameof(OptionsTlsVersionLabel));

    /// <summary>⚠ TLS 1.0 and 1.1 are cryptographically broken. Use only for testing against legacy servers.</summary>
    public static string OptionsTlsWarning => Get(nameof(OptionsTlsWarning));

    /// <summary>Follow redirects</summary>
    public static string OptionsFollowRedirects => Get(nameof(OptionsFollowRedirects));

    /// <summary>Enable HTTP diagnostics</summary>
    public static string OptionsEnableHttpDiagnostics => Get(nameof(OptionsEnableHttpDiagnostics));

    /// <summary>Default content type (body requests)</summary>
    public static string OptionsDefaultContentType => Get(nameof(OptionsDefaultContentType));

    /// <summary>Default URL for new requests</summary>
    public static string OptionsDefaultUrl => Get(nameof(OptionsDefaultUrl));

    /// <summary>Demo Server</summary>
    public static string OptionsDemoServerTitle => Get(nameof(OptionsDemoServerTitle));

    /// <summary>An embedded Kestrel server that provides /echo, /sse, /ws, and /status endpoints for the 'Localhost Demo' collection.</summary>
    public static string OptionsDemoServerDescription => Get(nameof(OptionsDemoServerDescription));

    /// <summary>Port</summary>
    public static string OptionsDemoServerPort => Get(nameof(OptionsDemoServerPort));

    /// <summary>Start</summary>
    public static string OptionsDemoServerStart => Get(nameof(OptionsDemoServerStart));

    /// <summary>Start demo server</summary>
    public static string OptionsDemoServerStartLabel => Get(nameof(OptionsDemoServerStartLabel));

    /// <summary>Stop</summary>
    public static string OptionsDemoServerStop => Get(nameof(OptionsDemoServerStop));

    /// <summary>Stop demo server</summary>
    public static string OptionsDemoServerStopLabel => Get(nameof(OptionsDemoServerStopLabel));

    /// <summary>● Running</summary>
    public static string OptionsDemoServerRunning => Get(nameof(OptionsDemoServerRunning));

    /// <summary>○ Stopped</summary>
    public static string OptionsDemoServerStopped => Get(nameof(OptionsDemoServerStopped));

    /// <summary>Auto-start scheduled jobs on launch</summary>
    public static string OptionsScheduledJobsAutoStart => Get(nameof(OptionsScheduledJobsAutoStart));

    /// <summary>Default interval for new jobs</summary>
    public static string OptionsScheduledJobsDefaultInterval => Get(nameof(OptionsScheduledJobsDefaultInterval));

    /// <summary>seconds</summary>
    public static string OptionsScheduledJobsSeconds => Get(nameof(OptionsScheduledJobsSeconds));

    /// <summary>Theme</summary>
    public static string OptionsTheme => Get(nameof(OptionsTheme));

    /// <summary>Font family</summary>
    public static string OptionsFontFamily => Get(nameof(OptionsFontFamily));

    /// <summary>Font size</summary>
    public static string OptionsFontSize => Get(nameof(OptionsFontSize));

    /// <summary>Collect unhandled exceptions</summary>
    public static string OptionsCollectExceptions => Get(nameof(OptionsCollectExceptions));

    /// <summary>When enabled, unhandled exceptions are stored locally so you can review and optionally report them as GitHub issues. No data is sent automatically.</summary>
    public static string OptionsCollectExceptionsDescription => Get(nameof(OptionsCollectExceptionsDescription));

    /// <summary>View Collected Exceptions…</summary>
    public static string OptionsViewCollectedExceptions => Get(nameof(OptionsViewCollectedExceptions));

    /// <summary>Import JSON</summary>
    public static string OptionsImportJson => Get(nameof(OptionsImportJson));

    /// <summary>Export JSON</summary>
    public static string OptionsExportJson => Get(nameof(OptionsExportJson));

    /// <summary>Close</summary>
    public static string OptionsClose => Get(nameof(OptionsClose));

    // ═══════════════════════════════ Environments ═══════════════════════════════

    /// <summary>Manage environments</summary>
    public static string EnvironmentsTitle => Get(nameof(EnvironmentsTitle));

    /// <summary>+ New Environment</summary>
    public static string EnvironmentsNewButton => Get(nameof(EnvironmentsNewButton));

    /// <summary>Environment name</summary>
    public static string EnvironmentsNameLabel => Get(nameof(EnvironmentsNameLabel));

    /// <summary>Environment name</summary>
    public static string EnvironmentsNamePlaceholder => Get(nameof(EnvironmentsNamePlaceholder));

    /// <summary>Active</summary>
    public static string EnvironmentsActiveColumn => Get(nameof(EnvironmentsActiveColumn));

    /// <summary>Variable key</summary>
    public static string EnvironmentsVariableKeyColumn => Get(nameof(EnvironmentsVariableKeyColumn));

    /// <summary>Variable value</summary>
    public static string EnvironmentsVariableValueColumn => Get(nameof(EnvironmentsVariableValueColumn));

    /// <summary>Variable key</summary>
    public static string EnvironmentsVariableKeyPlaceholder => Get(nameof(EnvironmentsVariableKeyPlaceholder));

    /// <summary>Variable value</summary>
    public static string EnvironmentsVariableValuePlaceholder => Get(nameof(EnvironmentsVariableValuePlaceholder));

    /// <summary>Edit</summary>
    public static string EnvironmentsEditButton => Get(nameof(EnvironmentsEditButton));

    /// <summary>Delete</summary>
    public static string EnvironmentsDeleteButton => Get(nameof(EnvironmentsDeleteButton));

    /// <summary>+ Add Variable</summary>
    public static string EnvironmentsAddVariable => Get(nameof(EnvironmentsAddVariable));

    /// <summary>Export JSON</summary>
    public static string EnvironmentsExportJson => Get(nameof(EnvironmentsExportJson));

    /// <summary>Color (optional):</summary>
    public static string EnvironmentsColorLabel => Get(nameof(EnvironmentsColorLabel));

    /// <summary>∅ none</summary>
    public static string EnvironmentsColorNone => Get(nameof(EnvironmentsColorNone));

    /// <summary>Show warning banner</summary>
    public static string EnvironmentsShowWarningBanner => Get(nameof(EnvironmentsShowWarningBanner));

    /// <summary>⚠ ACTIVE ENVIRONMENT:</summary>
    public static string EnvironmentsWarningBannerText => Get(nameof(EnvironmentsWarningBannerText));

    /// <summary>Red — Production / live</summary>
    public static string EnvironmentsColorAccessibleNameRed => Get(nameof(EnvironmentsColorAccessibleNameRed));

    /// <summary>Amber — Staging / pre-prod</summary>
    public static string EnvironmentsColorAccessibleNameAmber => Get(nameof(EnvironmentsColorAccessibleNameAmber));

    /// <summary>Green — Development / local</summary>
    public static string EnvironmentsColorAccessibleNameGreen => Get(nameof(EnvironmentsColorAccessibleNameGreen));

    /// <summary>Blue — QA / test</summary>
    public static string EnvironmentsColorAccessibleNameBlue => Get(nameof(EnvironmentsColorAccessibleNameBlue));

    /// <summary>Purple — Demo / sandbox</summary>
    public static string EnvironmentsColorAccessibleNamePurple => Get(nameof(EnvironmentsColorAccessibleNamePurple));

    /// <summary>Sensitive</summary>
    public static string EnvironmentsVariableSensitiveColumn => Get(nameof(EnvironmentsVariableSensitiveColumn));

    /// <summary>Mark as sensitive — value will be masked in the UI</summary>
    public static string EnvironmentsVariableSensitiveTooltip => Get(nameof(EnvironmentsVariableSensitiveTooltip));

    /// <summary>Expires (UTC)</summary>
    public static string EnvironmentsVariableExpiresColumn => Get(nameof(EnvironmentsVariableExpiresColumn));

    /// <summary>e.g. 2026-12-31T23:59:00Z</summary>
    public static string EnvironmentsVariableExpiresPlaceholder => Get(nameof(EnvironmentsVariableExpiresPlaceholder));

    /// <summary>Reveal / hide sensitive value</summary>
    public static string EnvironmentsVariableRevealTooltip => Get(nameof(EnvironmentsVariableRevealTooltip));

    /// <summary>Remove variable</summary>
    public static string EnvironmentsVariableRemoveAccessibleName => Get(nameof(EnvironmentsVariableRemoveAccessibleName));

    // ═══════════════════════════════ Log Panel / Log Window ═══════════════════════════════

    /// <summary>Live Log</summary>
    public static string LogPanelTitle => Get(nameof(LogPanelTitle));

    /// <summary>Auto-scroll</summary>
    public static string LogPanelAutoScroll => Get(nameof(LogPanelAutoScroll));

    /// <summary>Scheduled</summary>
    public static string LogPanelTabScheduled => Get(nameof(LogPanelTabScheduled));

    /// <summary>HTTP Diagnostics</summary>
    public static string LogPanelTabHttpDiagnostics => Get(nameof(LogPanelTabHttpDiagnostics));

    /// <summary>HTTP Requests</summary>
    public static string LogPanelTabHttpRequests => Get(nameof(LogPanelTabHttpRequests));

    /// <summary>Debug</summary>
    public static string LogPanelTabDebug => Get(nameof(LogPanelTabDebug));

    // ═══════════════════════════════ About Window ═══════════════════════════════

    /// <summary>Arbor HTTP Client</summary>
    public static string AboutTitle => Get(nameof(AboutTitle));

    /// <summary>Source:</summary>
    public static string AboutSourceLabel => Get(nameof(AboutSourceLabel));

    /// <summary>Close</summary>
    public static string AboutClose => Get(nameof(AboutClose));

    // ═══════════════════════════════ Cookie Jar ═══════════════════════════════

    /// <summary>Cookie Jar</summary>
    public static string CookieJarTitle => Get(nameof(CookieJarTitle));

    /// <summary>Refresh</summary>
    public static string CookieJarRefresh => Get(nameof(CookieJarRefresh));

    /// <summary>Clear All</summary>
    public static string CookieJarClearAll => Get(nameof(CookieJarClearAll));

    /// <summary>Add Cookie</summary>
    public static string CookieJarAddCookieTitle => Get(nameof(CookieJarAddCookieTitle));

    /// <summary>Name</summary>
    public static string CookieJarNamePlaceholder => Get(nameof(CookieJarNamePlaceholder));

    /// <summary>Value</summary>
    public static string CookieJarValuePlaceholder => Get(nameof(CookieJarValuePlaceholder));

    /// <summary>Domain (e.g. example.com)</summary>
    public static string CookieJarDomainPlaceholder => Get(nameof(CookieJarDomainPlaceholder));

    /// <summary>Add</summary>
    public static string CookieJarAddButton => Get(nameof(CookieJarAddButton));

    // ═══════════════════════════════ Diagnostics Window ═══════════════════════════════

    /// <summary>Unhandled Exceptions</summary>
    public static string DiagnosticsTitle => Get(nameof(DiagnosticsTitle));

    /// <summary>Review collected exceptions below. ...</summary>
    public static string DiagnosticsDescription => Get(nameof(DiagnosticsDescription));

    /// <summary>No unhandled exceptions have been collected.</summary>
    public static string DiagnosticsNoEntries => Get(nameof(DiagnosticsNoEntries));

    /// <summary>Stack trace</summary>
    public static string DiagnosticsStackTrace => Get(nameof(DiagnosticsStackTrace));

    /// <summary>Report on GitHub</summary>
    public static string DiagnosticsReportOnGitHub => Get(nameof(DiagnosticsReportOnGitHub));

    /// <summary>Dismiss</summary>
    public static string DiagnosticsDismiss => Get(nameof(DiagnosticsDismiss));

    /// <summary>Clear All</summary>
    public static string DiagnosticsClearAll => Get(nameof(DiagnosticsClearAll));

    /// <summary>Close</summary>
    public static string DiagnosticsClose => Get(nameof(DiagnosticsClose));

    // ═══════════════════════════════ Layout Management ═══════════════════════════════

    /// <summary>Window Layout</summary>
    public static string LayoutTitle => Get(nameof(LayoutTitle));

    /// <summary>Saved layouts:</summary>
    public static string LayoutSavedLayouts => Get(nameof(LayoutSavedLayouts));

    /// <summary>Save As New</summary>
    public static string LayoutSaveAsNew => Get(nameof(LayoutSaveAsNew));

    /// <summary>Save to Selected</summary>
    public static string LayoutSaveToSelected => Get(nameof(LayoutSaveToSelected));

    /// <summary>Remove Selected</summary>
    public static string LayoutRemoveSelected => Get(nameof(LayoutRemoveSelected));

    /// <summary>Restore Default</summary>
    public static string LayoutRestoreDefault => Get(nameof(LayoutRestoreDefault));

    // ═══════════════════════════════ Left Panel ═══════════════════════════════

    /// <summary>History</summary>
    public static string LeftPanelHistory => Get(nameof(LeftPanelHistory));

    /// <summary>Collections</summary>
    public static string LeftPanelCollections => Get(nameof(LeftPanelCollections));

    /// <summary>Scheduled</summary>
    public static string LeftPanelScheduled => Get(nameof(LeftPanelScheduled));

    /// <summary>Search history…</summary>
    public static string LeftPanelSearchHistory => Get(nameof(LeftPanelSearchHistory));

    /// <summary>Copy as cURL</summary>
    public static string LeftPanelCopyAsCurl => Get(nameof(LeftPanelCopyAsCurl));

    /// <summary>+ New</summary>
    public static string LeftPanelNewCollection => Get(nameof(LeftPanelNewCollection));

    /// <summary>Collection name</summary>
    public static string LeftPanelCollectionNamePlaceholder => Get(nameof(LeftPanelCollectionNamePlaceholder));

    /// <summary>+ Add request</summary>
    public static string LeftPanelAddRequest => Get(nameof(LeftPanelAddRequest));

    /// <summary>Import OpenAPI</summary>
    public static string LeftPanelImportOpenApi => Get(nameof(LeftPanelImportOpenApi));

    /// <summary>🗑 Delete</summary>
    public static string LeftPanelDeleteCollection => Get(nameof(LeftPanelDeleteCollection));

    /// <summary>Search requests…</summary>
    public static string LeftPanelSearchRequests => Get(nameof(LeftPanelSearchRequests));

    /// <summary>Sort:</summary>
    public static string LeftPanelSortLabel => Get(nameof(LeftPanelSortLabel));

    /// <summary>Default</summary>
    public static string LeftPanelSortDefault => Get(nameof(LeftPanelSortDefault));

    /// <summary>Name</summary>
    public static string LeftPanelSortName => Get(nameof(LeftPanelSortName));

    /// <summary>Method</summary>
    public static string LeftPanelSortMethod => Get(nameof(LeftPanelSortMethod));

    /// <summary>Path</summary>
    public static string LeftPanelSortPath => Get(nameof(LeftPanelSortPath));

    /// <summary>Show:</summary>
    public static string LeftPanelShowLabel => Get(nameof(LeftPanelShowLabel));

    /// <summary>Name+Path</summary>
    public static string LeftPanelShowNameAndPath => Get(nameof(LeftPanelShowNameAndPath));

    /// <summary>Name</summary>
    public static string LeftPanelShowName => Get(nameof(LeftPanelShowName));

    /// <summary>Path</summary>
    public static string LeftPanelShowPath => Get(nameof(LeftPanelShowPath));

    /// <summary>URL</summary>
    public static string LeftPanelShowUrl => Get(nameof(LeftPanelShowUrl));

    /// <summary>🌿 Tree</summary>
    public static string LeftPanelTreeView => Get(nameof(LeftPanelTreeView));

    /// <summary>+ Add Job</summary>
    public static string LeftPanelAddJob => Get(nameof(LeftPanelAddJob));

    /// <summary>Job name</summary>
    public static string LeftPanelJobNamePlaceholder => Get(nameof(LeftPanelJobNamePlaceholder));

    /// <summary>URL</summary>
    public static string LeftPanelJobUrlPlaceholder => Get(nameof(LeftPanelJobUrlPlaceholder));

    /// <summary>Body (optional)</summary>
    public static string LeftPanelJobBodyPlaceholder => Get(nameof(LeftPanelJobBodyPlaceholder));

    /// <summary>Interval (s)</summary>
    public static string LeftPanelIntervalLabel => Get(nameof(LeftPanelIntervalLabel));

    /// <summary>Auto-start</summary>
    public static string LeftPanelAutoStart => Get(nameof(LeftPanelAutoStart));

    /// <summary>Follow redirects</summary>
    public static string LeftPanelJobFollowRedirects => Get(nameof(LeftPanelJobFollowRedirects));

    /// <summary>Show in web view</summary>
    public static string LeftPanelJobShowInWebView => Get(nameof(LeftPanelJobShowInWebView));

    /// <summary>Last:</summary>
    public static string LeftPanelJobLastLabel => Get(nameof(LeftPanelJobLastLabel));

    /// <summary>▶ Start</summary>
    public static string LeftPanelJobStart => Get(nameof(LeftPanelJobStart));

    /// <summary>■ Stop</summary>
    public static string LeftPanelJobStop => Get(nameof(LeftPanelJobStop));

    /// <summary>🌐 Web view</summary>
    public static string LeftPanelJobWebView => Get(nameof(LeftPanelJobWebView));

    /// <summary>🗑 Delete</summary>
    public static string LeftPanelJobDelete => Get(nameof(LeftPanelJobDelete));

    // ═══════════════════════════════ WebView Window ═══════════════════════════════

    /// <summary>Web View</summary>
    public static string WebViewWindowTitle => Get(nameof(WebViewWindowTitle));

    /// <summary>Go</summary>
    public static string WebViewGo => Get(nameof(WebViewGo));

    /// <summary>Go back</summary>
    public static string WebViewGoBack => Get(nameof(WebViewGoBack));

    /// <summary>Go forward</summary>
    public static string WebViewGoForward => Get(nameof(WebViewGoForward));

    /// <summary>Refresh</summary>
    public static string WebViewRefresh => Get(nameof(WebViewRefresh));

    /// <summary>Refresh page</summary>
    public static string WebViewRefreshPage => Get(nameof(WebViewRefreshPage));

    /// <summary>Enter a URL and press Enter or click Go</summary>
    public static string WebViewUrlTooltip => Get(nameof(WebViewUrlTooltip));

    /// <summary>URL</summary>
    public static string WebViewUrlLabel => Get(nameof(WebViewUrlLabel));

    /// <summary>Navigate to URL</summary>
    public static string WebViewNavigate => Get(nameof(WebViewNavigate));

    /// <summary>Navigate to the URL</summary>
    public static string WebViewNavigateTooltip => Get(nameof(WebViewNavigateTooltip));

    // ═══════════════════════════════ Window titles ═══════════════════════════════

    /// <summary>Arbor HTTP Client</summary>
    public static string MainWindowTitle => Get(nameof(MainWindowTitle));

    /// <summary>About Arbor HTTP Client</summary>
    public static string AboutWindowTitle => Get(nameof(AboutWindowTitle));

    /// <summary>Diagnostics — Unhandled Exceptions</summary>
    public static string DiagnosticsWindowTitle => Get(nameof(DiagnosticsWindowTitle));

    /// <summary>Application Log</summary>
    public static string LogWindowTitle => Get(nameof(LogWindowTitle));

    /// <summary>Options</summary>
    public static string OptionsWindowTitle => Get(nameof(OptionsWindowTitle));

    // ═══════════════════════════════ Request method badges ═══════════════════════════════

    /// <summary>POST</summary>
    public static string RequestMethodPost => Get(nameof(RequestMethodPost));

    /// <summary>WS</summary>
    public static string RequestMethodWebSocket => Get(nameof(RequestMethodWebSocket));

    /// <summary>GET</summary>
    public static string RequestMethodGet => Get(nameof(RequestMethodGet));

    /// <summary>gRPC</summary>
    public static string RequestMethodGrpc => Get(nameof(RequestMethodGrpc));
}
