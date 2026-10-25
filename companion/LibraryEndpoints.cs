namespace TabletDeck;

internal static class LibraryEndpoints
{
    // CancellationToken prevents these handlers binding as RequestDelegate,
    // whose Task return value would be discarded instead of serialized.
    internal static void MapSoundpadLibrary(this WebApplication app, Func<bool,CancellationToken,Task<SoundpadLibraryResult>> read)
    {
        app.MapGet("/api/soundpad/library",async (CancellationToken ct)=>Results.Ok(await read(false,ct)));
        app.MapPost("/api/soundpad/library",async (CancellationToken ct)=>Results.Ok(await read(true,ct)));
    }
}
