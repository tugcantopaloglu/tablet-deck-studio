# Targeted media controls

Spotify controls resolve the selected Spotify Windows media session and invoke that session's transport API. They never send Windows media keys or use Windows' current/preferred media session. A browser playing a video concurrently is a separate session. If Spotify does not advertise a session, the UI must show that state; it must not redirect the controls to the browser.

The implementation in `companion/MediaSessions.cs` uses `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.GetSessions()`, `SourceAppUserModelId`, media properties, timeline properties and playback capabilities. The host targets `net10.0-windows10.0.19041.0` to reference these Windows APIs. No Spotify Web API account, application launch, injected shortcut, browser inspection, or external service is involved.

## Host contract

Create one `using var mediaSessions = new MediaSessions()` for the companion lifetime. Routes retain normal paired-client authentication, origin checks and PC-admin boundaries.

| Route | Request | Result |
| --- | --- | --- |
| `GET /api/computer/media` | None | `await mediaSessions.Snapshot(ct)` |
| `POST /api/computer/media/action` | `MediaSessionActionRequest` below | `await mediaSessions.Execute(request,ct)` returns `{ok:true}` |

Before starting a POST, the integrating host must verify Computer controls are enabled and the request's `presetId` matches the currently active preset. It must also use the existing shared action/maintenance/held-key guards and `ActionExecution` lifetime, so media controls cannot race an ordinary board action or an update. Register these two routes explicitly in the native proxy allowlists; do not broaden the proxy to arbitrary API routes. The component's own nonqueued write gate adds a second concurrency check around media writes.

The exact public API is:

```csharp
public MediaSessions();
public Task<MediaSessionsSnapshot> Snapshot(CancellationToken ct = default);
public Task<object> Execute(MediaSessionActionRequest request, CancellationToken ct = default);
public void Dispose();

public sealed record MediaSessionActionRequest(
    string PresetId, string SessionId, string ContextToken,
    string Operation, double? PositionMs = null);
```

Supported presets are `spotify` and `youtube`. Operations are `toggle`, `previous`, `next` and `seek`. A position is required only for `seek`; its unit is milliseconds relative to the displayed track's timeline start. The backend validates finite numeric values, duration and the player's current seekable range, then converts the chosen position to absolute Windows timeline ticks. `toggle` resolves to the selected session's explicit Play or Pause operation using fresh playback state and capabilities.

The snapshot is:

```json
{
  "sessions": [{
    "id": "opaque 32-character session identity",
    "source": "Spotify.exe",
    "kind": "spotify",
    "title": "Current track title",
    "artist": "Current artist",
    "album": "Current album",
    "artwork": "data:image/png;base64,...",
    "positionMs": 30000,
    "durationMs": 180000,
    "playing": true,
    "canPlay": true,
    "canPause": true,
    "canNext": true,
    "canPrevious": true,
    "canSeek": true,
    "contextToken": "opaque 32-character track identity"
  }],
  "error": null,
  "warning": null
}
```

`MediaSessionsSnapshot` has `MediaSessionInfo[] Sessions`, nullable `string Error`, and nullable `string Warning`. `MediaSessionInfo` has exactly the fields above. If one player fails but usable players remain, the response preserves those sessions with `error:null` and a readable `warning`; their controls remain usable. If every player's metadata fails, or platform initialization/enumeration fails, the response has an empty sessions array and an `error`. An ordinary empty enumeration has neither error nor warning. This distinction preserves the native bridge's existing rule that a truthy `error` rejects a response; partial success never requires weakening that rule. Nothing starts playback as a side effect of a snapshot, and no fake/sample track is returned.

## Session selection and stale requests

The opaque session ID is tied to a live Windows session object's identity, rather than its source name or whichever player Windows considers current. Every write enumerates live sessions again, resolves the selected object, reads fresh media metadata/capabilities, and validates the per-session track token. A disconnected or replaced object rejects the old ID even when the replacement has the same application source. The backend never tries another session after a failure or a declined command.

Track tokens incorporate bounded title/artist/album, timeline bounds and the session's `MediaPropertiesChanged` revision. Progress and Play/Pause changes do not themselves change the token. Accepted Previous/Next actions immediately invalidate old controls even before the player publishes its new metadata. A track change during the metadata read rejects that read; a stale action invalidates the cache so an immediate refresh can obtain current controls. A seek slider should retain its token from drag start and send it with the final chosen position. It must not silently replace that token after the user has dragged on a different track.

Windows applications remain responsible for the accuracy and timing of their media properties and for honoring their own session transport calls. The API provides no atomic operation that binds a seek to an immutable application-level track ID. A player that changes tracks after validation but before honoring the operation may still have an application-level race. The backend narrows that interval and rejects observable stale state; it does not claim control over application internals.

Exact source matching is intentionally conservative. Spotify supports `Spotify.exe` and `SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`, compared without case sensitivity. The installed Microsoft Store Spotify manifest was inspected read-only and confirmed the package family and `Spotify` application ID. Similar-looking identifiers are not accepted as Spotify.

Browser kind supports exact source IDs `Chrome`, `chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe`, `opera.exe` and `vivaldi.exe`, compared without case sensitivity. `Chrome` was observed directly in a read-only probe on this PC. Other source IDs remain `other` until explicitly supported. A browser session does not prove a website is YouTube; the user must select the intended browser player explicitly. Display its source and actual title to support that choice. Do not infer its website from a song/video title or artist.

GSMTC does not expose per-session application-volume control or a process ID. Any optional matching to the existing app mixer must use an exact known source-to-app mapping and a uniquely matching current mixer app. Such a slider controls the whole application, including other browser tabs, and must be labelled accordingly. Do not fall back to system volume or another process.

Windows' reported timeline position is current as of `LastUpdatedTime`, which can remain unchanged between transport events. For a playing track with a known duration, the native reader estimates the current position as the reported position plus elapsed UTC time multiplied by the reported `PlaybackRate` (1 means normal speed). It clamps the result to track start/end. Paused playback, unknown/live duration, missing/zero/non-finite rate, an unset or future timestamp, or an overflowing estimate retain the reported position. This is a bounded display estimate, not a seek operation. Position estimates do not enter the track fingerprint or change control tokens. Focused clock/rate/boundary tests run without a Windows player or playback changes.

## Bounds and read behavior

Snapshots cache for 500 ms; concurrent reads share one in-flight request. Each read/write has a five-second platform timeout. Writes reject concurrent requests rather than queueing playback commands. The snapshot includes at most 16 live sessions. Title is limited to 512 characters; artist and album to 256; source display to 240. Control characters are removed. Artwork is optional, embedded as PNG/JPEG only, and limited to 96 KiB encoded file bytes and dimensions no greater than 2048×2048. Unsupported, oversized or unreadable artwork is omitted. No remote artwork URL is fetched.

## Verification

Run `dotnet run --project tests/media-sessions/MediaSessionTests.csproj --configuration Release`. The tests link the production implementation and its WinRT declarations but use injected fake players for all playback operations. They verify concurrent Spotify/browser targeting, exact source matching, metadata/progress/artwork, stale track/replaced session rejection, live capabilities/seek ranges, cache isolation, shared in-flight reads, nonqueued writes and player refusal. They never change real playback.

An explicit read-only probe is available with `dotnet run --project tests/media-sessions/MediaSessionTests.csproj --configuration Release -- --probe`. It reads Windows metadata twice, reports whether live session IDs stay stable across enumerations, and omits artwork base64 from diagnostic output. It never calls `Execute`, opens an application, or changes playback. The probe on this PC found a paused Chrome video plus a Riot Client session; no Spotify session was advertised at that time. Repeated enumeration preserved the live session IDs.

The project security rule requires `snyk_code_scan` for new first-party code. That tool was absent from the enabled inventory during this implementation; the integrating agent must run it when available and fix/rescan new findings. Compilation and the fake-player tests do not replace the required scan.

## Primary references

- [Microsoft: enumerate system media sessions and optionally control playback](https://devblogs.microsoft.com/oldnewthing/20231108-00/?p=108980): session enumeration, metadata and timeline access.
- [GlobalSystemMediaTransportControlsSessionManager.GetSessions](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager.getsessions): enumerates the available sessions.
- [SourceAppUserModelId](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.sourceappusermodelid): source application identity.
- [TryGetMediaPropertiesAsync](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trygetmediapropertiesasync): media properties for a particular session.
- [GetTimelineProperties](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.gettimelineproperties): timeline state for that session.
- [Position](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties.position), [LastUpdatedTime](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessiontimelineproperties.lastupdatedtime), and [PlaybackRate](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionplaybackinfo.playbackrate): timestamped position and nullable playback rate used for the bounded display estimate.
- [TryChangePlaybackPositionAsync](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeplaybackpositionasync): seek position uses ticks.
- [Session transport methods](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession): explicit Play/Pause/Previous/Next operations and their boolean acceptance result.
