using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Activity;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Features;
using Nakama.Api.Modules.Tasks.Domain;
using Nakama.Api.Modules.Tasks.Infrastructure;

namespace Nakama.Api.Modules.Tasks.Features;

public sealed record AttachmentUploaderResponse(Guid Id, string FullName);
public sealed record TaskAttachmentResponse(Guid Id, string FileName, string ContentType, long SizeBytes, AttachmentUploaderResponse UploadedBy, DateTimeOffset CreatedAt);

internal static class TaskAttachmentsEndpoints
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = [".pdf"],
        ["image/png"] = [".png"],
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["text/plain"] = [".txt"],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"]
    };
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Task Attachments").RequireAuthorization(Policies.AuthenticatedUser);
        group.MapGet("/{taskId:guid}/attachments", List);
        group.MapPost("/{taskId:guid}/attachments", Upload).DisableAntiforgery();
        group.MapGet("/{taskId:guid}/attachments/{attachmentId:guid}/download", Download);
        group.MapDelete("/{taskId:guid}/attachments/{attachmentId:guid}", Delete);
    }
    private static IResult Problem(string type, string title, int status) => Results.Problem(type: $"https://nakama/errors/{type}", title: title, statusCode: status);
    private static async Task<WorkTask?> AccessibleTask(Guid taskId, NakamaDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(x => x.Id == taskId, ct);
        return task is not null && await ProjectAccess.CanAccessAsync(db, task.ProjectId, current, ct) ? task : null;
    }
    private static async Task<IResult> Upload(Guid taskId, IFormFile? file, NakamaDbContext db, IClock clock, IActivityRecorder activity, ICurrentUser current, IAttachmentStorage storage, IOptions<AttachmentOptions> options, CancellationToken ct)
    {
        var task = await AccessibleTask(taskId, db, current, ct); if (task is null) return Problem("attachment-forbidden", "No puedes adjuntar archivos en esta tarea.", 403);
        var uploader = await db.Users.SingleOrDefaultAsync(x => x.Id == current.UserId, ct); if (uploader is null || !uploader.IsActive) return Problem("attachment-forbidden", "No puedes adjuntar archivos en esta tarea.", 403);
        if (file is null || file.Length <= 0) return Problem("attachment-file-empty", "Selecciona un archivo con contenido.", 400);
        if (file.Length > options.Value.MaxFileSizeBytes) return Problem("attachment-file-too-large", "El archivo supera el tamaño máximo permitido.", 400);
        var original = Path.GetFileName(file.FileName); var extension = Path.GetExtension(original); if (string.IsNullOrWhiteSpace(original) || !Allowed.TryGetValue(file.ContentType, out var extensions) || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return Problem("attachment-file-type-not-allowed", "Este tipo de archivo no está permitido.", 400);
        var stored = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        try { await using var input = file.OpenReadStream(); await storage.SaveAsync(stored, input, ct); }
        catch { return Problem("attachment-storage-failure", "No se pudo guardar el archivo.", 500); }
        try
        {
            var attachment = TaskAttachment.Create(task.Id, current.UserId, original, stored, file.ContentType, file.Length, clock); db.TaskAttachments.Add(attachment); activity.Record(task.ProjectId, task.Id, ActivityType.AttachmentAdded, new { attachmentId = attachment.Id, fileName = attachment.OriginalFileName }); await db.SaveChangesAsync(ct); return Results.Created($"/api/tasks/{task.Id}/attachments/{attachment.Id}", Response(attachment, uploader));
        }
        catch
        {
            try { await storage.DeleteAsync(stored, ct); } catch { }
            return Problem("attachment-storage-failure", "No se pudo registrar el archivo.", 500);
        }
    }
    private static async Task<IResult> List(Guid taskId, NakamaDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (await AccessibleTask(taskId, db, current, ct) is null) return Problem("attachment-forbidden", "No puedes ver los archivos de esta tarea.", 403);
        var rows = await (from attachment in db.TaskAttachments.AsNoTracking() join user in db.Users.AsNoTracking() on attachment.UploadedByUserId equals user.Id where attachment.TaskId == taskId orderby attachment.CreatedAt descending select new { attachment, user }).ToListAsync(ct);
        return Results.Ok(rows.Select(x => Response(x.attachment, x.user)));
    }
    private static async Task<IResult> Download(Guid taskId, Guid attachmentId, NakamaDbContext db, ICurrentUser current, IAttachmentStorage storage, CancellationToken ct)
    {
        if (await AccessibleTask(taskId, db, current, ct) is null) return Problem("attachment-forbidden", "No puedes descargar archivos de esta tarea.", 403);
        var attachment = await db.TaskAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId && x.TaskId == taskId, ct); if (attachment is null) return Problem("attachment-not-found", "No existe el archivo.", 404);
        try { var stream = await storage.OpenReadAsync(attachment.StoredFileName, ct); return stream is null ? Problem("attachment-not-found", "No existe el archivo.", 404) : Results.File(stream, attachment.ContentType, attachment.OriginalFileName, enableRangeProcessing: true); } catch { return Problem("attachment-storage-failure", "No se pudo abrir el archivo.", 500); }
    }
    private static async Task<IResult> Delete(Guid taskId, Guid attachmentId, NakamaDbContext db, IActivityRecorder activity, ICurrentUser current, IAttachmentStorage storage, CancellationToken ct)
    {
        var task = await AccessibleTask(taskId, db, current, ct); if (task is null) return Problem("attachment-forbidden", "No puedes eliminar archivos en esta tarea.", 403);
        var attachment = await db.TaskAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.TaskId == taskId, ct); if (attachment is null) return Problem("attachment-not-found", "No existe el archivo.", 404); if (attachment.UploadedByUserId != current.UserId && !await CurrentUserAccess.IsActiveAdminAsync(db, current, ct)) return Problem("attachment-forbidden", "No puedes eliminar este archivo.", 403);
        try { await storage.DeleteAsync(attachment.StoredFileName, ct); } catch { return Problem("attachment-storage-failure", "No se pudo eliminar el archivo.", 500); }
        db.TaskAttachments.Remove(attachment); activity.Record(task.ProjectId, task.Id, ActivityType.AttachmentDeleted, new { attachmentId, fileName = attachment.OriginalFileName });
        try { await db.SaveChangesAsync(ct); return Results.NoContent(); } catch { return Problem("attachment-storage-failure", "No se pudo eliminar el registro del archivo.", 500); }
    }
    private static TaskAttachmentResponse Response(TaskAttachment attachment, User user) => new(attachment.Id, attachment.OriginalFileName, attachment.ContentType, attachment.SizeBytes, new(user.Id, user.FullName), attachment.CreatedAt);
}
