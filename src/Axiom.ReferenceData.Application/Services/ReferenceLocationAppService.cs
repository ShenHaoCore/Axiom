// 地点档案应用服务实现（Application 层）。
// 唯一键检查、并发令牌协调、ETag 解析——全部放在这里。
// Outbox 消息发布由 Infrastructure 层的 Repository 实现内部处理，保证事务一致性。
using Axiom.ReferenceData.Application.Dtos;
using Axiom.ReferenceData.Application.ObjectMapping;
using Axiom.ReferenceData.Domain.Entities;
using Axiom.ReferenceData.Domain.Exceptions;
using Axiom.ReferenceData.Domain.Repositories;
using Axiom.SharedKernel.Models;

namespace Axiom.ReferenceData.Application.Services;

public sealed class ReferenceLocationAppService(IReferenceLocationRepository repository)
    : IReferenceLocationAppService
{
    public async Task<PagedResponse<ReferenceLocationDto>> ListAsync(
        ReferenceLocationType type,
        ReferenceLocationListQuery query,
        CancellationToken ct = default)
    {
        var paged = await repository.ListByTypeAsync(
            type,
            query.Code,
            query.Name,
            query.Page,
            query.PageSize,
            ct);

        return new PagedResponse<ReferenceLocationDto>(
            paged.Items.Select(ReferenceLocationMapper.ToDto).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize);
    }

    public async Task<(ReferenceLocationDto? Dto, Guid? ETag)> GetAsync(
        ReferenceLocationType type,
        Guid id,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsNoTrackingAsync(id, ct);
        if (entity is null || entity.Type != type) return (null, null);
        return (ReferenceLocationMapper.ToDto(entity), entity.ConcurrencyToken);
    }

    public async Task<(ReferenceLocationDto Dto, Guid ETag, Uri LocationUri)> CreateAsync(
        ReferenceLocationType type,
        UpsertReferenceLocationInput input,
        CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(input.Code);
        if (await repository.CodeExistsAsync(type, normalizedCode, excludedId: null, ct))
        {
            throw new ReferenceLocationCodeConflictException(normalizedCode, (int)type);
        }

        var entity = ReferenceLocationMapper.ToEntity(type, input);
        await repository.AddAsync(entity, ct);

        var dto = ReferenceLocationMapper.ToDto(entity);
        var location = new Uri($"/api/reference-data/{TypeToPlural(type).ToLowerInvariant()}/{entity.Id}", UriKind.Relative);
        return (dto, entity.ConcurrencyToken, location);
    }

    public async Task<(ReferenceLocationDto Dto, Guid ETag)> UpdateAsync(
        ReferenceLocationType type,
        Guid id,
        UpsertReferenceLocationInput input,
        Guid expectedETag,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceLocationNotFoundException(id);
        if (entity.Type != type)
            throw new ReferenceLocationNotFoundException(id);

        var normalizedCode = NormalizeCode(input.Code);
        if (await repository.CodeExistsAsync(type, normalizedCode, excludedId: id, ct))
        {
            throw new ReferenceLocationCodeConflictException(normalizedCode, (int)type);
        }

        entity.Code = normalizedCode;
        entity.Name = input.Name.Trim();
        entity.CountryCode = NormalizeCode(input.CountryCode);
        entity.City = NormalizeNullable(input.City);
        entity.Latitude = input.Latitude;
        entity.Longitude = input.Longitude;

        await repository.UpdateWithETagAsync(entity, expectedETag, ct);

        var dto = ReferenceLocationMapper.ToDto(entity);
        return (dto, entity.ConcurrencyToken);
    }

    public async Task DeleteAsync(
        ReferenceLocationType type,
        Guid id,
        Guid expectedETag,
        CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct)
            ?? throw new ReferenceLocationNotFoundException(id);
        if (entity.Type != type)
            throw new ReferenceLocationNotFoundException(id);

        await repository.RemoveWithETagAsync(entity, expectedETag, ct);
    }

    // ---------- 私有辅助方法 ----------

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TypeToPlural(ReferenceLocationType type) => type switch
    {
        ReferenceLocationType.Airport => "airports",
        ReferenceLocationType.Seaport => "seaports",
        ReferenceLocationType.RailStation => "rail-stations",
        _ => type.ToString()
    };
}
