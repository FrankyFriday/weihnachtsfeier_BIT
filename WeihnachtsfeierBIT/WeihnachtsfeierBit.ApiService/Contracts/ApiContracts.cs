using System.Text.Json;

namespace WeihnachtsfeierBit.ApiService.Contracts;

public sealed record CreateAdminAccountRequest(string Username, string Password);
public sealed record AttachAdminToProjectRequest(string Username);
public sealed record ParticipantLoginRequest(int Number);
public sealed record AdminLoginRequest(string Username, string Password);
public sealed record SessionResponse(Guid SessionId, string AccessToken, DateTime ExpiresAtUtc, string Role, Guid? ProjectId, string DisplayName);
public sealed record ParticipantResponse(Guid Id, int Number, string? Name, Guid? TeamId);
public sealed record CreateParticipantRequest(int Number, string? Name, Guid? TeamId);
public sealed record UpdateParticipantRequest(string? Name, Guid? TeamId, int? Number);
public sealed record TeamResponse(Guid Id, string DisplayName, string Color, int Points);
public sealed record CreateTeamRequest(string DisplayName, string Color);
public sealed record UpdateTeamRequest(string DisplayName, string Color, int Points);
public sealed record ShowResponse(Guid Id, string Name);
public sealed record AdminShowResponse(Guid Id, string Name, int ProjectCount);
public sealed record CreateShowRequest(string Name);
public sealed record LinkShowToProjectRequest(Guid ShowId);
public sealed record SlideRequest(string Type, int Position, JsonElement Parameters);
public sealed record SlideResponse(Guid Id, Guid ShowId, string Type, int Position, JsonElement Parameters);
public sealed record SessionSummary(Guid Id, Guid UserId, DateTime CreatedAtUtc, DateTime LastActivityAtUtc, DateTime ExpiresAtUtc, string? UserAgent);
public sealed record ApiError(string Message);
public sealed record CreateTestEntryRequest(string? Name);