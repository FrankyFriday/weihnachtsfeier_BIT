using System.Text.Json;

namespace WeihnachtsfeierBit.ApiService.Contracts;

public sealed record AdminLoginRequest(string Username, string Password);
public sealed record CreateAdminAccountRequest(string Username, string Password);
public sealed record AttachAdminToProjectRequest(string Username);
public sealed record ProjectResponse(Guid Id, string Name);
public sealed record ProjectOverviewResponse(Guid Id, string Name, int ShowCount, int ParticipantCount, int TeamCount);
public sealed record CreateProjectRequest(string Name);
public sealed record RenameProjectRequest(string Name);
public sealed record ParticipantResponse(Guid Id, int Number, string? Name, Guid? TeamId);
public sealed record CreateParticipantRequest(int Number, string? Name, Guid? TeamId);
public sealed record UpdateParticipantRequest(string? Name, Guid? TeamId, int? Number);
public sealed record GenerateParticipantsRequest(int Count, int Digits, Guid? TeamId);
public sealed record GenerateParticipantsResponse(int CreatedCount, ParticipantResponse[] Participants);
public sealed record RandomizeTeamsRequest(bool OnlyUnassigned);
public sealed record RandomizeTeamsResponse(int AssignedCount);
public sealed record TeamResponse(Guid Id, string DisplayName, string Color, int Points);
public sealed record CreateTeamRequest(string DisplayName, string Color);
public sealed record UpdateTeamRequest(string DisplayName, string Color, int Points);
public sealed record UpdateTeamPointsRequest(int Points);
public sealed record CreateShowRequest(string Name);
public sealed record ShowResponse(Guid Id, string Name);
public sealed record AdminShowResponse(Guid Id, string Name, int ProjectCount);
public sealed record LinkShowRequest(Guid ShowId);
public sealed record SlideRequest(string Type, int Position, JsonElement Parameters);
public sealed record SlideResponse(Guid Id, Guid ShowId, string Type, int Position, JsonElement Parameters);
public sealed record ReorderSlidesRequest(Guid[] SlideIds);
public sealed record SessionResponse(Guid Id, string AccessToken, DateTime ExpiresAtUtc, string Role, Guid? ProjectId, string DisplayName);
public sealed record SessionSummary(Guid Id, Guid UserId, DateTime CreatedAtUtc, DateTime LastActivityAtUtc, DateTime ExpiresAtUtc, string? UserAgent);
public sealed record ParticipantLoginRequest(int Number);
public sealed record ApiError(string Message);
public sealed record CreateTestEntryRequest(string? Name);

public sealed record GameInstanceResponse(Guid Id, Guid ProjektId, string AccessCode, DateTime CreatedAtUtc);
public sealed record ResolveCodeResponse(Guid GameInstanceId, Guid ProjektId, string ProjectName, string Code);
public sealed record LoginByCodeRequest(string Code, int Number);

public sealed record StartSessionShowRequest(
    Guid ShowId,
    string Mode,
    string? NavigationMode,
    bool? IsHidden,
    DateTime? ScheduledStartUtc,
    bool? AutoStartOnSchedule);

public sealed record CreateSpontaneousEventRequest(
    string SlideType,
    string Mode,
    JsonElement Parameters,
    string? NavigationMode,
    bool? IsHidden,
    DateTime? ScheduledStartUtc,
    bool? AutoStartOnSchedule);

public sealed record UpdateSessionEventRequest(
    int? CurrentSlideIndex,
    string? Mode,
    bool? IsHidden,
    string? Status);

public sealed record UpdateEventModeRequest(string Mode);
public sealed record SubmitResponseRequest(JsonElement ResponseData);
public sealed record SetCanvasEventRequest(Guid? EventId);

public sealed record ChatGroupSummary(
    Guid Id,
    string Name,
    string Type,
    Guid? AssociatedTeamId,
    int MemberCount,
    string? LastMessageText,
    DateTime? LastMessageAtUtc,
    int UnreadCount);

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ChatGroupId,
    Guid? SenderParticipantId,
    Guid? SenderAdminId,
    string SenderName,
    string? TeamColor,
    string MessageText,
    bool IsAdminSender,
    DateTime SentAtUtc);

public sealed record SendChatMessageRequest(string MessageText);
public sealed record CreateCustomGroupRequest(string Name, Guid[] ParticipantIds);
public sealed record ChatContactDto(Guid Id, string Name, string Kind);
